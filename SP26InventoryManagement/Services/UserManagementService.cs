using Microsoft.EntityFrameworkCore;
using System.Data;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;

namespace SP26InventoryManagement.Services;

public class UserManagementService : IUserManagementService
{
    private const string AdminRoleCode = "ADMIN";
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string ActiveAdminGuardMessage = "Operation denied. System must always have at least one active ADMIN.";
    private const string ConcurrencyConflictPrefix = "Concurrency conflict.";

    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;
    private readonly ISessionValidationService _sessionValidationService;
    private readonly Sp26inventoryManagementDbContext _dbContext;

    public UserManagementService(
        Sp26inventoryManagementDbContext dbContext,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService,
        ISessionValidationService sessionValidationService)
    {
        _dbContext = dbContext;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _sessionValidationService = sessionValidationService;
    }

    public async Task<IReadOnlyList<WarehouseLookupDto>> GetActiveWarehousesAsync(int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        return await _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.IsActive)
            .OrderBy(warehouse => warehouse.WarehouseCode)
            .Select(warehouse => new WarehouseLookupDto
            {
                WarehouseId = warehouse.WarehouseId,
                WarehouseCode = warehouse.WarehouseCode,
                WarehouseName = warehouse.WarehouseName
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<UserListItemDto>> SearchUsersAsync(UserSearchCriteria criteria, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        return await _userRepository.SearchAsync(criteria, ct);
    }

    public async Task<PagedResult<StaffWarehouseAssignmentItemDto>> GetStaffWarehouseAssignmentsAsync(
        StaffWarehouseAssignmentSearchCriteria criteria,
        int actorUserId,
        CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        int staffRoleId = await ResolveActiveRoleIdByCodeAsync(StaffRoleCode, ct);

        int pageNumber = criteria.PageNumber <= 0 ? 1 : criteria.PageNumber;
        int pageSize = criteria.PageSize <= 0 ? 20 : criteria.PageSize;

        IQueryable<User> query = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.UserRoleUsers.Any(userRole => userRole.RoleId == staffRoleId && userRole.Role.IsActive));

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            string keyword = criteria.SearchText.Trim();
            query = query.Where(user =>
                EF.Functions.Like(user.Username, $"%{keyword}%") ||
                EF.Functions.Like(user.FullName, $"%{keyword}%"));
        }

        if (criteria.IsActive.HasValue)
        {
            query = query.Where(user => user.IsActive == criteria.IsActive.Value);
        }

        int totalCount = await query.CountAsync(ct);

        var users = await query
            .OrderBy(user => user.Username)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new
            {
                user.UserId,
                user.Username,
                user.FullName,
                user.IsActive,
                user.RowVersion
            })
            .ToListAsync(ct);

        int[] userIds = users.Select(user => user.UserId).ToArray();

        Dictionary<int, (int WarehouseId, string WarehouseDisplay)> assignmentMap =
            await _dbContext.UserWarehouseAssignments
                .AsNoTracking()
                .Where(assignment => userIds.Contains(assignment.UserId))
                .Select(assignment => new
                {
                    assignment.UserId,
                    assignment.WarehouseId,
                    assignment.Warehouse.WarehouseCode,
                    assignment.Warehouse.WarehouseName
                })
                .ToDictionaryAsync(
                    item => item.UserId,
                    item => (item.WarehouseId, $"{item.WarehouseCode} - {item.WarehouseName}"),
                    ct);

        IReadOnlyList<StaffWarehouseAssignmentItemDto> items = users
            .Select(user =>
            {
                bool hasAssignment = assignmentMap.TryGetValue(user.UserId, out var assignment);
                return new StaffWarehouseAssignmentItemDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    FullName = user.FullName,
                    IsActive = user.IsActive,
                    RowVersion = user.RowVersion.ToArray(),
                    CurrentWarehouseId = hasAssignment ? assignment.WarehouseId : null,
                    CurrentWarehouseDisplay = hasAssignment ? assignment.WarehouseDisplay : "Unassigned"
                };
            })
            .ToList();

        return new PagedResult<StaffWarehouseAssignmentItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<OperationResult> AssignOrChangeStaffWarehouseAsync(
        int staffUserId,
        int warehouseId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return authorization;
        }

        if (warehouseId <= 0)
        {
            return OperationResult.Failure("Warehouse is required.");
        }

        int staffRoleId;
        try
        {
            staffRoleId = await ResolveActiveRoleIdByCodeAsync(StaffRoleCode, ct);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult.Failure(ex.Message);
        }

        bool warehouseExists = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(warehouse => warehouse.WarehouseId == warehouseId && warehouse.IsActive, ct);
        if (!warehouseExists)
        {
            return OperationResult.Failure("Selected warehouse is invalid or inactive.");
        }

        int? oldWarehouseId = null;
        string? oldWarehouseDisplay = null;
        string? newWarehouseDisplay = null;

        try
        {
            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var staffUser = await GetUserWithRolesForUpdateAsync(staffUserId, ct);
            if (staffUser is null)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure("Target user not found.");
            }

            if (!IsUserRowVersionMatch(staffUser, expectedUserRowVersion))
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure(BuildConcurrencyConflictMessage("User data changed. Please refresh and retry."));
            }

            bool hasStaffRole = HasActiveRole(staffUser, staffRoleId);
            if (!hasStaffRole)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure("Access denied. Target user does not have WAREHOUSE_STAFF role.");
            }

            UserWarehouseAssignment? assignment = await _dbContext.UserWarehouseAssignments
                .FirstOrDefaultAsync(item => item.UserId == staffUserId, ct);

            if (assignment is not null)
            {
                oldWarehouseId = assignment.WarehouseId;
            }

            if (oldWarehouseId.HasValue && oldWarehouseId.Value == warehouseId)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Success();
            }

            if (assignment is null)
            {
                _dbContext.UserWarehouseAssignments.Add(new UserWarehouseAssignment
                {
                    UserId = staffUserId,
                    WarehouseId = warehouseId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = actorUserId,
                    RowVersion = Array.Empty<byte>()
                });
            }
            else
            {
                assignment.WarehouseId = warehouseId;
                assignment.AssignedAt = DateTime.UtcNow;
                assignment.AssignedByUserId = actorUserId;
            }

            staffUser.AuthVersion += 1;
            staffUser.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            Dictionary<int, string> warehouseDisplayMap = await _dbContext.Warehouses
                .AsNoTracking()
                .Where(warehouse =>
                    warehouse.WarehouseId == warehouseId ||
                    (oldWarehouseId.HasValue && warehouse.WarehouseId == oldWarehouseId.Value))
                .ToDictionaryAsync(
                    warehouse => warehouse.WarehouseId,
                    warehouse => $"{warehouse.WarehouseCode} - {warehouse.WarehouseName}",
                    ct);

            if (oldWarehouseId.HasValue)
            {
                warehouseDisplayMap.TryGetValue(oldWarehouseId.Value, out oldWarehouseDisplay);
            }

            warehouseDisplayMap.TryGetValue(warehouseId, out newWarehouseDisplay);

            await dbTransaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(BuildConcurrencyConflictMessage("Warehouse assignment changed by another action. Please refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Failed to update staff warehouse due to a database update conflict.");
        }

        await _auditLogService.LogAsync(
            actionType: "ASSIGN_STAFF_WAREHOUSE",
            entityName: "Users",
            entityId: staffUserId.ToString(),
            userId: actorUserId,
            isSuccess: true,
            severity: "INFO",
            details: new
            {
                OldWarehouseId = oldWarehouseId,
                OldWarehouse = oldWarehouseDisplay,
                NewWarehouseId = warehouseId,
                NewWarehouse = newWarehouseDisplay
            },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return OperationResult.Success();
    }

    public async Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return CreateUserResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        string username = request.Username.Trim();
        string fullName = request.FullName.Trim();
        string? email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        string? phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        IReadOnlyCollection<int> roleIds = request.RoleIds.Distinct().ToArray();
        int? warehouseId = request.WarehouseId;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName))
        {
            return CreateUserResult.Failure("Username and full name are required.");
        }

        if (roleIds.Count == 0)
        {
            return CreateUserResult.Failure("At least one role is required.");
        }

        if (await _userRepository.UsernameExistsAsync(username, ct))
        {
            return CreateUserResult.Failure("Username already exists.");
        }

        if (!string.IsNullOrWhiteSpace(email) && await _userRepository.EmailExistsAsync(email, null, ct))
        {
            return CreateUserResult.Failure("Email already exists.");
        }

        IReadOnlyList<Role> roles = await _roleRepository.GetActiveRolesByIdsAsync(roleIds, ct);
        if (roles.Count != roleIds.Count)
        {
            return CreateUserResult.Failure("One or more selected roles are invalid or inactive.");
        }

        bool isStaffUser = roles.Any(role => string.Equals(role.RoleCode, StaffRoleCode, StringComparison.OrdinalIgnoreCase));
        if (isStaffUser && (!warehouseId.HasValue || warehouseId.Value <= 0))
        {
            return CreateUserResult.Failure("Warehouse is required for WAREHOUSE_STAFF.");
        }

        if (warehouseId.HasValue)
        {
            bool warehouseExists = await _dbContext.Warehouses
                .AsNoTracking()
                .AnyAsync(warehouse => warehouse.WarehouseId == warehouseId.Value && warehouse.IsActive, ct);
            if (!warehouseExists)
            {
                return CreateUserResult.Failure("Selected warehouse is invalid or inactive.");
            }
        }

        string generatedPassword = _passwordHasher.GenerateRandomPassword();

        var user = new User
        {
            Username = username,
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = _passwordHasher.Hash(generatedPassword),
            IsActive = true,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actorUserId,
            RowVersion = Array.Empty<byte>()
        };

        try
        {
            await _userRepository.AddAsync(user, ct);
            await _userRoleRepository.SyncRolesAsync(user.UserId, roleIds, actorUserId, ct);

            if (isStaffUser)
            {
                _dbContext.UserWarehouseAssignments.Add(new UserWarehouseAssignment
                {
                    UserId = user.UserId,
                    WarehouseId = warehouseId!.Value,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = actorUserId,
                    RowVersion = Array.Empty<byte>()
                });

                await _dbContext.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return CreateUserResult.Failure("Data changed while creating user. Please refresh and retry.");
        }
        catch (DbUpdateException)
        {
            return CreateUserResult.Failure("Failed to create user due to a database update conflict.");
        }

        await _auditLogService.LogAsync(
            actionType: "CREATE_USER",
            entityName: "Users",
            entityId: user.UserId.ToString(),
            userId: actorUserId,
            isSuccess: true,
            severity: "INFO",
            details: new { user.Username, RoleIds = roleIds, WarehouseId = warehouseId },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return CreateUserResult.Success(user.UserId, generatedPassword);
    }

    public async Task<OperationResult> SetUserRolesAsync(
        int targetUserId,
        IReadOnlyCollection<int> roleIds,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return authorization;
        }

        IReadOnlyCollection<int> normalizedRoleIds = roleIds.Distinct().ToArray();
        if (normalizedRoleIds.Count > 0)
        {
            IReadOnlyList<Role> roles = await _roleRepository.GetActiveRolesByIdsAsync(normalizedRoleIds, ct);
            if (roles.Count != normalizedRoleIds.Count)
            {
                return OperationResult.Failure("One or more selected roles are invalid or inactive.");
            }
        }

        int? adminRoleId = await _roleRepository.GetRoleIdByCodeAsync(AdminRoleCode, ct);
        if (!adminRoleId.HasValue)
        {
            return OperationResult.Failure("ADMIN role is not configured.");
        }

        int staffRoleId;
        try
        {
            staffRoleId = await ResolveActiveRoleIdByCodeAsync(StaffRoleCode, ct);
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult.Failure(ex.Message);
        }

        RoleSyncResult roleSyncResult;
        bool assignmentRemovedByRoleChange = false;
        try
        {
            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var targetUser = await GetUserWithRolesForUpdateAsync(targetUserId, ct);
            if (targetUser is null)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure("Target user not found.");
            }

            if (!IsUserRowVersionMatch(targetUser, expectedUserRowVersion))
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure(BuildConcurrencyConflictMessage("User data changed. Please refresh and retry."));
            }

            bool hasAdminBefore = targetUser.IsActive && HasActiveAdminRole(targetUser, adminRoleId.Value);
            bool hasAdminAfter = targetUser.IsActive && normalizedRoleIds.Contains(adminRoleId.Value);
            bool hasStaffBefore = HasActiveRole(targetUser, staffRoleId);
            bool hasStaffAfter = normalizedRoleIds.Contains(staffRoleId);

            if (actorUserId == targetUserId && hasAdminBefore && !hasAdminAfter)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure("You cannot remove ADMIN role from your own account.");
            }

            if (hasAdminBefore && !hasAdminAfter)
            {
                int adminCountBefore = await CountActiveAdminsAsync(adminRoleId.Value, ct);
                if (adminCountBefore <= 1)
                {
                    await dbTransaction.RollbackAsync(ct);
                    return OperationResult.Failure(ActiveAdminGuardMessage);
                }
            }

            roleSyncResult = await SyncUserRolesInCurrentContextAsync(targetUserId, normalizedRoleIds, actorUserId, ct);

            if (hasStaffBefore && !hasStaffAfter)
            {
                UserWarehouseAssignment? assignment = await _dbContext.UserWarehouseAssignments
                    .FirstOrDefaultAsync(item => item.UserId == targetUserId, ct);
                if (assignment is not null)
                {
                    _dbContext.UserWarehouseAssignments.Remove(assignment);
                    assignmentRemovedByRoleChange = true;
                }
            }

            bool roleChanged = roleSyncResult.AddedRoleIds.Count > 0 || roleSyncResult.RemovedRoleIds.Count > 0;
            if (roleChanged || assignmentRemovedByRoleChange)
            {
                targetUser.AuthVersion += 1;
                targetUser.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }

            if (hasAdminBefore && !hasAdminAfter)
            {
                int adminCountAfter = await CountActiveAdminsAsync(adminRoleId.Value, ct);
                if (adminCountAfter <= 0)
                {
                    await dbTransaction.RollbackAsync(ct);
                    return OperationResult.Failure(ActiveAdminGuardMessage);
                }
            }

            await dbTransaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(BuildConcurrencyConflictMessage("Role update was modified by another action. Please refresh and retry."));
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Failure($"Failed to update user roles due to a database update conflict. {GetMostSpecificErrorMessage(ex)}");
        }

        Dictionary<int, string> addedRoleCodeMap = await _roleRepository.GetRoleCodeMapAsync(roleSyncResult.AddedRoleIds, ct);
        Dictionary<int, string> removedRoleCodeMap = await _roleRepository.GetRoleCodeMapAsync(roleSyncResult.RemovedRoleIds, ct);

        if (roleSyncResult.AddedRoleIds.Count > 0)
        {
            await _auditLogService.LogAsync(
                actionType: "ASSIGN_ROLE",
                entityName: "Users",
                entityId: targetUserId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new { RoleIds = roleSyncResult.AddedRoleIds, RoleCodes = addedRoleCodeMap.Values.ToArray() },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);
        }

        if (roleSyncResult.RemovedRoleIds.Count > 0)
        {
            await _auditLogService.LogAsync(
                actionType: "REMOVE_ROLE",
                entityName: "Users",
                entityId: targetUserId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new { RoleIds = roleSyncResult.RemovedRoleIds, RoleCodes = removedRoleCodeMap.Values.ToArray() },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);
        }

        if (assignmentRemovedByRoleChange)
        {
            await _auditLogService.LogAsync(
                actionType: "REMOVE_STAFF_WAREHOUSE",
                entityName: "Users",
                entityId: targetUserId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new { Reason = "STAFF_ROLE_REMOVED" },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);
        }

        return OperationResult.Success();
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(
        int targetUserId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return ResetPasswordResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (targetUser is null)
        {
            return ResetPasswordResult.Failure("Target user not found.");
        }

        if (!IsUserRowVersionMatch(targetUser, expectedUserRowVersion))
        {
            return ResetPasswordResult.Failure(BuildConcurrencyConflictMessage("User data changed. Please refresh and retry."));
        }

        string generatedPassword = _passwordHasher.GenerateRandomPassword();

        try
        {
            targetUser.PasswordHash = _passwordHasher.Hash(generatedPassword);
            targetUser.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(targetUser, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ResetPasswordResult.Failure(BuildConcurrencyConflictMessage("Password reset changed by another action. Please refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return ResetPasswordResult.Failure("Failed to reset password due to a database update conflict.");
        }

        await _auditLogService.LogAsync(
            actionType: "RESET_PASSWORD",
            entityName: "Users",
            entityId: targetUser.UserId.ToString(),
            userId: actorUserId,
            isSuccess: true,
            severity: "WARN",
            details: new { TargetUsername = targetUser.Username },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return ResetPasswordResult.Success(generatedPassword);
    }

    public async Task<OperationResult> DeactivateUserAsync(
        int targetUserId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return authorization;
        }

        if (targetUserId == actorUserId)
        {
            return OperationResult.Failure("You cannot deactivate your own account.");
        }

        int? adminRoleId = await _roleRepository.GetRoleIdByCodeAsync(AdminRoleCode, ct);
        if (!adminRoleId.HasValue)
        {
            return OperationResult.Failure("ADMIN role is not configured.");
        }

        string? targetUsername = null;
        try
        {
            await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var targetUser = await GetUserWithRolesForUpdateAsync(targetUserId, ct);
            if (targetUser is null)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure("Target user not found.");
            }

            if (!IsUserRowVersionMatch(targetUser, expectedUserRowVersion))
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Failure(BuildConcurrencyConflictMessage("User data changed. Please refresh and retry."));
            }

            if (!targetUser.IsActive)
            {
                await dbTransaction.RollbackAsync(ct);
                return OperationResult.Success();
            }

            bool isActiveAdmin = HasActiveAdminRole(targetUser, adminRoleId.Value);
            if (isActiveAdmin)
            {
                int adminCountBefore = await CountActiveAdminsAsync(adminRoleId.Value, ct);
                if (adminCountBefore <= 1)
                {
                    await dbTransaction.RollbackAsync(ct);
                    return OperationResult.Failure(ActiveAdminGuardMessage);
                }
            }

            targetUser.IsActive = false;
            targetUser.AuthVersion += 1;
            targetUser.UpdatedAt = DateTime.UtcNow;
            targetUsername = targetUser.Username;
            await _dbContext.SaveChangesAsync(ct);

            if (isActiveAdmin)
            {
                int adminCountAfter = await CountActiveAdminsAsync(adminRoleId.Value, ct);
                if (adminCountAfter <= 0)
                {
                    await dbTransaction.RollbackAsync(ct);
                    return OperationResult.Failure(ActiveAdminGuardMessage);
                }
            }

            await dbTransaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(BuildConcurrencyConflictMessage("Deactivate was modified by another action. Please refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Failed to deactivate user due to a database update conflict.");
        }

        await _auditLogService.LogAsync(
            actionType: "DEACTIVATE_USER",
            entityName: "Users",
            entityId: targetUserId.ToString(),
            userId: actorUserId,
            isSuccess: true,
            severity: "WARN",
            details: new { TargetUsername = targetUsername },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return OperationResult.Success();
    }

    public async Task<OperationResult> ReactivateUserAsync(
        int targetUserId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct)
    {
        OperationResult authorization = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, AdminRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return authorization;
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (targetUser is null)
        {
            return OperationResult.Failure("Target user not found.");
        }

        if (!IsUserRowVersionMatch(targetUser, expectedUserRowVersion))
        {
            return OperationResult.Failure(BuildConcurrencyConflictMessage("User data changed. Please refresh and retry."));
        }

        if (targetUser.IsActive)
        {
            return OperationResult.Success();
        }

        try
        {
            targetUser.IsActive = true;
            targetUser.AuthVersion += 1;
            targetUser.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(targetUser, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure(BuildConcurrencyConflictMessage("Reactivate was modified by another action. Please refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Failed to reactivate user due to a database update conflict.");
        }

        await _auditLogService.LogAsync(
            actionType: "REACTIVATE_USER",
            entityName: "Users",
            entityId: targetUserId.ToString(),
            userId: actorUserId,
            isSuccess: true,
            severity: "INFO",
            details: new { targetUser.Username },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return OperationResult.Success();
    }

    private Task<User?> GetUserWithRolesForUpdateAsync(int userId, CancellationToken ct)
    {
        return _dbContext.Users
            .Include(user => user.UserRoleUsers)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.UserId == userId, ct);
    }

    private async Task<int> CountActiveAdminsAsync(int adminRoleId, CancellationToken ct)
    {
        return await _dbContext.Users
            .Where(user => user.IsActive)
            .CountAsync(user => user.UserRoleUsers.Any(userRole =>
                userRole.RoleId == adminRoleId &&
                userRole.Role.IsActive), ct);
    }

    private static bool IsUserRowVersionMatch(User user, byte[] expectedUserRowVersion)
    {
        if (expectedUserRowVersion.Length == 0 || user.RowVersion.Length == 0)
        {
            return false;
        }

        return user.RowVersion.SequenceEqual(expectedUserRowVersion);
    }

    private static string BuildConcurrencyConflictMessage(string detail)
    {
        return $"{ConcurrencyConflictPrefix} {detail}";
    }

    private async Task<int> ResolveActiveRoleIdByCodeAsync(string roleCode, CancellationToken ct)
    {
        int? roleId = await _roleRepository.GetRoleIdByCodeAsync(roleCode, ct);
        if (!roleId.HasValue)
        {
            throw new InvalidOperationException($"Role '{roleCode}' is not configured.");
        }

        return roleId.Value;
    }

    private async Task<RoleSyncResult> SyncUserRolesInCurrentContextAsync(
        int userId,
        IReadOnlyCollection<int> targetRoleIds,
        int actorUserId,
        CancellationToken ct)
    {
        HashSet<int> normalizedRoleIds = targetRoleIds.Distinct().ToHashSet();

        List<UserRole> existingRoles = await _dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .ToListAsync(ct);

        HashSet<int> existingRoleIdsInDb = existingRoles
            .Select(userRole => userRole.RoleId)
            .ToHashSet();

        var localEntries = _dbContext.ChangeTracker
            .Entries<UserRole>()
            .Where(entry => entry.Entity.UserId == userId && entry.State != EntityState.Detached)
            .ToList();

        foreach (var localEntry in localEntries)
        {
            bool existsInDb = existingRoleIdsInDb.Contains(localEntry.Entity.RoleId);
            if (localEntry.State == EntityState.Added)
            {
                // Remove stale local inserts that would conflict with DB or are no longer requested.
                if (existsInDb || !normalizedRoleIds.Contains(localEntry.Entity.RoleId))
                {
                    localEntry.State = EntityState.Detached;
                }
            }
            else if (localEntry.State == EntityState.Deleted &&
                     existsInDb &&
                     normalizedRoleIds.Contains(localEntry.Entity.RoleId))
            {
                // Keep requested role.
                localEntry.State = EntityState.Unchanged;
            }
        }

        IReadOnlyCollection<int> addedRoleIds = normalizedRoleIds.Except(existingRoleIdsInDb).ToList();
        List<UserRole> removedRoles = existingRoles.Where(userRole => !normalizedRoleIds.Contains(userRole.RoleId)).ToList();

        if (removedRoles.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(removedRoles);
        }

        DateTime now = DateTime.UtcNow;
        foreach (int roleId in addedRoleIds)
        {
            bool alreadyTracked = _dbContext.ChangeTracker
                .Entries<UserRole>()
                .Any(entry =>
                    entry.Entity.UserId == userId &&
                    entry.Entity.RoleId == roleId &&
                    entry.State != EntityState.Detached &&
                    entry.State != EntityState.Deleted);
            if (alreadyTracked)
            {
                continue;
            }

            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = now,
                AssignedByUserId = actorUserId
            });
        }

        return new RoleSyncResult
        {
            AddedRoleIds = addedRoleIds,
            RemovedRoleIds = removedRoles.Select(userRole => userRole.RoleId).ToList()
        };
    }

    private static string GetMostSpecificErrorMessage(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private static bool HasActiveRole(User user, int roleId)
    {
        return user.UserRoleUsers.Any(userRole =>
            userRole.RoleId == roleId &&
            userRole.Role.IsActive);
    }

    private static bool HasActiveAdminRole(User user, int adminRoleId)
    {
        return HasActiveRole(user, adminRoleId);
    }
}
