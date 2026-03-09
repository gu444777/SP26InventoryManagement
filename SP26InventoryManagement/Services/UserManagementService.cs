using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;

namespace SP26InventoryManagement.Services;

public class UserManagementService : IUserManagementService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;

    public UserManagementService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
    }

    public Task<PagedResult<UserListItemDto>> SearchUsersAsync(UserSearchCriteria criteria, CancellationToken ct)
    {
        return _userRepository.SearchAsync(criteria, ct);
    }

    public async Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, int actorUserId, CancellationToken ct)
    {
        string username = request.Username.Trim();
        string fullName = request.FullName.Trim();
        string? email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        string? phoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        IReadOnlyCollection<int> roleIds = request.RoleIds.Distinct().ToArray();

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

        string generatedPassword = _passwordHasher.GenerateRandomPassword();

        var user = new User
        {
            Username = username,
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = _passwordHasher.Hash(generatedPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        };

        try
        {
            await _userRepository.AddAsync(user, ct);
            await _userRoleRepository.SyncRolesAsync(user.UserId, roleIds, actorUserId, ct);
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
            details: new { user.Username, RoleIds = roleIds },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return CreateUserResult.Success(user.UserId, generatedPassword);
    }

    public async Task<OperationResult> SetUserRolesAsync(int targetUserId, IReadOnlyCollection<int> roleIds, int actorUserId, CancellationToken ct)
    {
        IReadOnlyCollection<int> normalizedRoleIds = roleIds.Distinct().ToArray();
        var targetUser = await _userRepository.GetByIdWithRolesAsync(targetUserId, ct);
        if (targetUser is null)
        {
            return OperationResult.Failure("Target user not found.");
        }

        if (normalizedRoleIds.Count > 0)
        {
            IReadOnlyList<Role> roles = await _roleRepository.GetActiveRolesByIdsAsync(normalizedRoleIds, ct);
            if (roles.Count != normalizedRoleIds.Count)
            {
                return OperationResult.Failure("One or more selected roles are invalid or inactive.");
            }
        }

        int? adminRoleId = await _roleRepository.GetRoleIdByCodeAsync("ADMIN", ct);
        if (adminRoleId.HasValue && actorUserId == targetUserId)
        {
            bool hasAdminBefore = targetUser.UserRoleUsers.Any(userRole => userRole.RoleId == adminRoleId.Value);
            bool hasAdminAfter = normalizedRoleIds.Contains(adminRoleId.Value);

            if (hasAdminBefore && !hasAdminAfter)
            {
                return OperationResult.Failure("You cannot remove ADMIN role from your own account.");
            }
        }

        RoleSyncResult roleSyncResult;
        try
        {
            roleSyncResult = await _userRoleRepository.SyncRolesAsync(targetUserId, normalizedRoleIds, actorUserId, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("Role update conflict. Please refresh and retry.");
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Failed to update user roles due to a database update conflict.");
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

        return OperationResult.Success();
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(int targetUserId, int actorUserId, CancellationToken ct)
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (targetUser is null)
        {
            return ResetPasswordResult.Failure("Target user not found.");
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
            return ResetPasswordResult.Failure("Password reset conflict. Please refresh and retry.");
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

    public async Task<OperationResult> DeactivateUserAsync(int targetUserId, int actorUserId, CancellationToken ct)
    {
        if (targetUserId == actorUserId)
        {
            return OperationResult.Failure("You cannot deactivate your own account.");
        }

        var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (targetUser is null)
        {
            return OperationResult.Failure("Target user not found.");
        }

        if (!targetUser.IsActive)
        {
            return OperationResult.Success();
        }

        try
        {
            targetUser.IsActive = false;
            targetUser.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(targetUser, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("Deactivate conflict. Please refresh and retry.");
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
            details: new { targetUser.Username },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return OperationResult.Success();
    }

    public async Task<OperationResult> ReactivateUserAsync(int targetUserId, int actorUserId, CancellationToken ct)
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (targetUser is null)
        {
            return OperationResult.Failure("Target user not found.");
        }

        if (targetUser.IsActive)
        {
            return OperationResult.Success();
        }

        try
        {
            targetUser.IsActive = true;
            targetUser.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(targetUser, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("Reactivate conflict. Please refresh and retry.");
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
}
