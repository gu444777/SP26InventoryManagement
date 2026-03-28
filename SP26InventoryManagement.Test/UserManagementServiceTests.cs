using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.Test;

public class UserManagementServiceTests
{
    [Fact]
    public async Task CreateUserAsync_ShouldFailWhenStaffRoleMissingWarehouse()
    {
        TestHarness harness = CreateHarness();

        CreateUserResult result = await harness.Service.CreateUserAsync(
            new CreateUserRequest
            {
                Username = "staff.missing.wh",
                FullName = "Staff Missing Warehouse",
                RoleIds = [2],
                WarehouseId = null
            },
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Warehouse is required", result.ErrorMessage ?? string.Empty);

        bool userExists = await harness.DbContext.Users.AnyAsync(user => user.Username == "staff.missing.wh");
        Assert.False(userExists);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldCreateWarehouseAssignmentForStaff()
    {
        TestHarness harness = CreateHarness();

        CreateUserResult result = await harness.Service.CreateUserAsync(
            new CreateUserRequest
            {
                Username = "staff.new",
                FullName = "New Staff",
                RoleIds = [2],
                WarehouseId = 1
            },
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.UserId);

        User createdUser = await harness.DbContext.Users.SingleAsync(user => user.UserId == result.UserId!.Value);
        UserWarehouseAssignment assignment = await harness.DbContext.UserWarehouseAssignments
            .SingleAsync(item => item.UserId == createdUser.UserId);
        bool hasStaffRole = await harness.DbContext.UserRoles.AnyAsync(userRole =>
            userRole.UserId == createdUser.UserId && userRole.RoleId == 2);

        Assert.Equal(1, assignment.WarehouseId);
        Assert.True(hasStaffRole);
        Assert.Equal(1, createdUser.AuthVersion);
        Assert.Contains("CREATE_USER", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldNotRequireWarehouseForNonStaffRole()
    {
        TestHarness harness = CreateHarness();

        CreateUserResult result = await harness.Service.CreateUserAsync(
            new CreateUserRequest
            {
                Username = "viewer.new",
                FullName = "New Viewer",
                RoleIds = [3],
                WarehouseId = null
            },
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.UserId);

        bool hasAssignment = await harness.DbContext.UserWarehouseAssignments
            .AnyAsync(item => item.UserId == result.UserId!.Value);

        Assert.False(hasAssignment);
    }

    [Fact]
    public async Task AssignOrChangeStaffWarehouseAsync_ShouldCreateAssignmentAndIncreaseAuthVersion_ForStaffWithoutAssignment()
    {
        TestHarness harness = CreateHarness();

        DateTime now = DateTime.UtcNow;
        harness.DbContext.Users.Add(new User
        {
            UserId = 5,
            Username = "staff.no.assignment",
            PasswordHash = "HASHED",
            FullName = "Staff No Assignment",
            IsActive = true,
            AuthVersion = 1,
            CreatedAt = now,
            RowVersion = [1]
        });
        harness.DbContext.UserRoles.Add(new UserRole
        {
            UserId = 5,
            RoleId = 2,
            AssignedAt = now,
            AssignedByUserId = 1
        });
        await harness.DbContext.SaveChangesAsync();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 5)).AuthVersion;

        OperationResult result = await harness.Service.AssignOrChangeStaffWarehouseAsync(
            staffUserId: 5,
            warehouseId: 2,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 5),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        UserWarehouseAssignment assignment = await harness.DbContext.UserWarehouseAssignments
            .SingleAsync(item => item.UserId == 5);
        User userAfter = await harness.DbContext.Users.SingleAsync(user => user.UserId == 5);

        Assert.Equal(2, assignment.WarehouseId);
        Assert.Equal(originalAuthVersion + 1, userAfter.AuthVersion);
    }

    [Fact]
    public async Task AssignOrChangeStaffWarehouseAsync_ShouldUpdateAssignmentAndIncreaseAuthVersion_ForAssignedStaff()
    {
        TestHarness harness = CreateHarness();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 4)).AuthVersion;

        OperationResult result = await harness.Service.AssignOrChangeStaffWarehouseAsync(
            staffUserId: 4,
            warehouseId: 2,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 4),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        UserWarehouseAssignment assignment = await harness.DbContext.UserWarehouseAssignments
            .SingleAsync(item => item.UserId == 4);
        User userAfter = await harness.DbContext.Users.SingleAsync(user => user.UserId == 4);

        Assert.Equal(2, assignment.WarehouseId);
        Assert.Equal(originalAuthVersion + 1, userAfter.AuthVersion);
    }

    [Fact]
    public async Task AssignOrChangeStaffWarehouseAsync_ShouldFail_ForNonStaffUser()
    {
        TestHarness harness = CreateHarness();

        OperationResult result = await harness.Service.AssignOrChangeStaffWarehouseAsync(
            staffUserId: 3,
            warehouseId: 1,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 3),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not have WAREHOUSE_STAFF role", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task AssignOrChangeStaffWarehouseAsync_ShouldFail_ForInactiveWarehouse()
    {
        TestHarness harness = CreateHarness();

        Warehouse inactiveWarehouse = await harness.DbContext.Warehouses.SingleAsync(warehouse => warehouse.WarehouseId == 2);
        inactiveWarehouse.IsActive = false;
        await harness.DbContext.SaveChangesAsync();

        OperationResult result = await harness.Service.AssignOrChangeStaffWarehouseAsync(
            staffUserId: 4,
            warehouseId: 2,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 4),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid or inactive", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldRemoveStaffAssignment_WhenStaffRoleRemoved()
    {
        TestHarness harness = CreateHarness();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 4)).AuthVersion;

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 4,
            roleIds: [3],
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 4),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        bool hasAssignment = await harness.DbContext.UserWarehouseAssignments
            .AnyAsync(item => item.UserId == 4);
        User userAfter = await harness.DbContext.Users
            .Include(user => user.UserRoleUsers)
            .SingleAsync(user => user.UserId == 4);

        Assert.False(hasAssignment);
        Assert.DoesNotContain(userAfter.UserRoleUsers, userRole => userRole.RoleId == 2);
        Assert.Contains(userAfter.UserRoleUsers, userRole => userRole.RoleId == 3);
        Assert.Equal(originalAuthVersion + 1, userAfter.AuthVersion);
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldFailWhenRemovingAdminFromLastActiveAdmin()
    {
        TestHarness harness = CreateHarness();

        User secondAdmin = await harness.DbContext.Users.SingleAsync(user => user.UserId == 2);
        secondAdmin.IsActive = false;
        secondAdmin.UpdatedAt = DateTime.UtcNow;
        await harness.DbContext.SaveChangesAsync();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 1)).AuthVersion;

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 1,
            roleIds: [3],
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 1),
            actorUserId: 2,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("at least one active ADMIN", result.ErrorMessage ?? string.Empty);

        User userAfter = await harness.DbContext.Users
            .Include(user => user.UserRoleUsers)
            .SingleAsync(user => user.UserId == 1);

        Assert.Contains(userAfter.UserRoleUsers, userRole => userRole.RoleId == 1);
        Assert.Equal(originalAuthVersion, userAfter.AuthVersion);
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldFailWhenTargetIsLastActiveAdmin()
    {
        TestHarness harness = CreateHarness();

        User secondAdmin = await harness.DbContext.Users.SingleAsync(user => user.UserId == 2);
        secondAdmin.IsActive = false;
        secondAdmin.UpdatedAt = DateTime.UtcNow;
        await harness.DbContext.SaveChangesAsync();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 1)).AuthVersion;

        OperationResult result = await harness.Service.DeactivateUserAsync(
            targetUserId: 1,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 1),
            actorUserId: 2,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("at least one active ADMIN", result.ErrorMessage ?? string.Empty);

        User userAfter = await harness.DbContext.Users.SingleAsync(user => user.UserId == 1);
        Assert.True(userAfter.IsActive);
        Assert.Equal(originalAuthVersion, userAfter.AuthVersion);
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldSucceedAndIncreaseAuthVersion_WhenAnotherActiveAdminExists()
    {
        TestHarness harness = CreateHarness();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 1)).AuthVersion;

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 1,
            roleIds: [3],
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 1),
            actorUserId: 2,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        User userAfter = await harness.DbContext.Users
            .Include(user => user.UserRoleUsers)
            .SingleAsync(user => user.UserId == 1);

        Assert.DoesNotContain(userAfter.UserRoleUsers, userRole => userRole.RoleId == 1);
        Assert.Contains(userAfter.UserRoleUsers, userRole => userRole.RoleId == 3);
        Assert.Equal(originalAuthVersion + 1, userAfter.AuthVersion);
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldSucceedAndIncreaseAuthVersion_WhenAnotherActiveAdminExists()
    {
        TestHarness harness = CreateHarness();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 1)).AuthVersion;

        OperationResult result = await harness.Service.DeactivateUserAsync(
            targetUserId: 1,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 1),
            actorUserId: 2,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        User userAfter = await harness.DbContext.Users.SingleAsync(user => user.UserId == 1);
        Assert.False(userAfter.IsActive);
        Assert.Equal(originalAuthVersion + 1, userAfter.AuthVersion);
    }

    [Fact]
    public async Task ReactivateUserAsync_ShouldIncreaseAuthVersion_WhenStatusChanges()
    {
        TestHarness harness = CreateHarness();

        User target = await harness.DbContext.Users.SingleAsync(user => user.UserId == 3);
        target.IsActive = false;
        target.UpdatedAt = DateTime.UtcNow;
        await harness.DbContext.SaveChangesAsync();
        int originalAuthVersion = target.AuthVersion;

        OperationResult result = await harness.Service.ReactivateUserAsync(
            targetUserId: 3,
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 3),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        User userAfter = await harness.DbContext.Users.SingleAsync(user => user.UserId == 3);
        Assert.True(userAfter.IsActive);
        Assert.Equal(originalAuthVersion + 1, userAfter.AuthVersion);
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldNotIncreaseAuthVersion_WhenRolesUnchanged()
    {
        TestHarness harness = CreateHarness();

        int originalAuthVersion = (await harness.DbContext.Users.SingleAsync(user => user.UserId == 1)).AuthVersion;

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 1,
            roleIds: [1],
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 1),
            actorUserId: 2,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        User userAfter = await harness.DbContext.Users.SingleAsync(user => user.UserId == 1);
        Assert.Equal(originalAuthVersion, userAfter.AuthVersion);
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldNotThrow_WhenTrackedAddedRoleAlreadyExistsInContext()
    {
        TestHarness harness = CreateHarness();

        harness.DbContext.UserRoles.Add(new UserRole
        {
            UserId = 4,
            RoleId = 3,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = 1
        });

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 4,
            roleIds: [2, 3],
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 4),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        int roleCount = await harness.DbContext.UserRoles
            .CountAsync(userRole => userRole.UserId == 4 && userRole.RoleId == 3);
        Assert.Equal(1, roleCount);
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldNotFail_WhenLocalTrackedAddedRoleDuplicatesExistingDbRole()
    {
        TestHarness harness = CreateHarness();

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 1,
            roleIds: [1],
            expectedUserRowVersion: GetUserRowVersion(harness.DbContext, 1),
            actorUserId: 2,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);

        int roleCount = await harness.DbContext.UserRoles
            .CountAsync(userRole => userRole.UserId == 1 && userRole.RoleId == 1);
        Assert.Equal(1, roleCount);
    }

    [Fact]
    public async Task SearchUsersAsync_ShouldReturnRowVersion()
    {
        TestHarness harness = CreateHarness();

        PagedResult<UserListItemDto> result = await harness.Service.SearchUsersAsync(
            new UserSearchCriteria
            {
                PageNumber = 1,
                PageSize = 20
            },
            actorUserId: 1,
            CancellationToken.None);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.NotEmpty(item.RowVersion));
    }

    [Fact]
    public async Task GetStaffWarehouseAssignmentsAsync_ShouldReturnRowVersion()
    {
        TestHarness harness = CreateHarness();

        PagedResult<StaffWarehouseAssignmentItemDto> result = await harness.Service.GetStaffWarehouseAssignmentsAsync(
            new StaffWarehouseAssignmentSearchCriteria
            {
                PageNumber = 1,
                PageSize = 20
            },
            actorUserId: 1,
            CancellationToken.None);

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, item => Assert.NotEmpty(item.RowVersion));
    }

    [Fact]
    public async Task SetUserRolesAsync_ShouldFail_WhenExpectedRowVersionIsStale()
    {
        TestHarness harness = CreateHarness();

        OperationResult result = await harness.Service.SetUserRolesAsync(
            targetUserId: 4,
            roleIds: [3],
            expectedUserRowVersion: [9],
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Concurrency conflict", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldFail_WhenExpectedRowVersionIsStale()
    {
        TestHarness harness = CreateHarness();

        OperationResult result = await harness.Service.DeactivateUserAsync(
            targetUserId: 3,
            expectedUserRowVersion: [9],
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Concurrency conflict", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ReactivateUserAsync_ShouldFail_WhenExpectedRowVersionIsStale()
    {
        TestHarness harness = CreateHarness();

        User target = await harness.DbContext.Users.SingleAsync(user => user.UserId == 3);
        target.IsActive = false;
        await harness.DbContext.SaveChangesAsync();

        OperationResult result = await harness.Service.ReactivateUserAsync(
            targetUserId: 3,
            expectedUserRowVersion: [9],
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Concurrency conflict", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenExpectedRowVersionIsStale()
    {
        TestHarness harness = CreateHarness();

        ResetPasswordResult result = await harness.Service.ResetPasswordAsync(
            targetUserId: 3,
            expectedUserRowVersion: [9],
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Concurrency conflict", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task AssignOrChangeStaffWarehouseAsync_ShouldFail_WhenExpectedRowVersionIsStale()
    {
        TestHarness harness = CreateHarness();

        OperationResult result = await harness.Service.AssignOrChangeStaffWarehouseAsync(
            staffUserId: 4,
            warehouseId: 2,
            expectedUserRowVersion: [9],
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Concurrency conflict", result.ErrorMessage ?? string.Empty);
    }

    private static byte[] GetUserRowVersion(Sp26inventoryManagementDbContext dbContext, int userId)
    {
        return dbContext.Users
            .AsNoTracking()
            .Where(user => user.UserId == userId)
            .Select(user => user.RowVersion)
            .Single()
            .ToArray();
    }

    private static TestHarness CreateHarness()
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"UserManagementServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new Sp26inventoryManagementDbContext(options);
        SeedReferenceData(dbContext);

        var auditLogService = new RecordingAuditLogService();
        var service = new UserManagementService(
            dbContext,
            new UserRepository(dbContext),
            new RoleRepository(dbContext),
            new UserRoleRepository(dbContext),
            new StubPasswordHasher(),
            auditLogService,
            new AlwaysValidSessionValidationService());

        return new TestHarness(dbContext, service, auditLogService);
    }

    private static void SeedReferenceData(Sp26inventoryManagementDbContext dbContext)
    {
        DateTime now = DateTime.UtcNow;

        dbContext.Users.AddRange(
            new User
            {
                UserId = 1,
                Username = "admin.primary",
                PasswordHash = "HASHED",
                FullName = "Primary Admin",
                IsActive = true,
                AuthVersion = 1,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 2,
                Username = "admin.secondary",
                PasswordHash = "HASHED",
                FullName = "Secondary Admin",
                IsActive = true,
                AuthVersion = 1,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 3,
                Username = "viewer.seed",
                PasswordHash = "HASHED",
                FullName = "Seed Viewer",
                IsActive = true,
                AuthVersion = 1,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 4,
                Username = "staff.seed",
                PasswordHash = "HASHED",
                FullName = "Seed Staff",
                IsActive = true,
                AuthVersion = 1,
                CreatedAt = now,
                RowVersion = [1]
            });

        dbContext.Roles.AddRange(
            new Role
            {
                RoleId = 1,
                RoleCode = "ADMIN",
                RoleName = "Admin",
                IsActive = true,
                IsSystemRole = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new Role
            {
                RoleId = 2,
                RoleCode = "WAREHOUSE_STAFF",
                RoleName = "Warehouse Staff",
                IsActive = true,
                IsSystemRole = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new Role
            {
                RoleId = 3,
                RoleCode = "VIEWER",
                RoleName = "Viewer",
                IsActive = true,
                IsSystemRole = true,
                CreatedAt = now,
                RowVersion = [1]
            });

        dbContext.UserRoles.AddRange(
            new UserRole
            {
                UserId = 1,
                RoleId = 1,
                AssignedAt = now,
                AssignedByUserId = 1
            },
            new UserRole
            {
                UserId = 2,
                RoleId = 1,
                AssignedAt = now,
                AssignedByUserId = 1
            },
            new UserRole
            {
                UserId = 3,
                RoleId = 3,
                AssignedAt = now,
                AssignedByUserId = 1
            },
            new UserRole
            {
                UserId = 4,
                RoleId = 2,
                AssignedAt = now,
                AssignedByUserId = 1
            });

        dbContext.Warehouses.AddRange(
            new Warehouse
            {
                WarehouseId = 1,
                WarehouseCode = "WH01",
                WarehouseName = "Warehouse 01",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new Warehouse
            {
                WarehouseId = 2,
                WarehouseCode = "WH02",
                WarehouseName = "Warehouse 02",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            });

        dbContext.UserWarehouseAssignments.Add(new UserWarehouseAssignment
        {
            UserId = 4,
            WarehouseId = 1,
            AssignedAt = now,
            AssignedByUserId = 1,
            RowVersion = [1]
        });

        dbContext.SaveChanges();
    }

    private sealed class TestHarness(
        Sp26inventoryManagementDbContext dbContext,
        UserManagementService service,
        RecordingAuditLogService auditLogService)
    {
        public Sp26inventoryManagementDbContext DbContext { get; } = dbContext;

        public UserManagementService Service { get; } = service;

        public RecordingAuditLogService AuditLogService { get; } = auditLogService;
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string plainPassword) => $"HASH::{plainPassword}";

        public bool Verify(string plainPassword, string storedHash) => storedHash == $"HASH::{plainPassword}";

        public string GenerateRandomPassword() => "TempPass#123";
    }

    private sealed class AlwaysValidSessionValidationService : ISessionValidationService
    {
        public Task<OperationResult> EnsureCurrentSessionAsync(string? requiredRoleCode, CancellationToken ct)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> EnsureSessionForUserAsync(
            int expectedUserId,
            string? requiredRoleCode,
            CancellationToken ct,
            bool forceRevalidation = false)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<string> ActionTypes { get; } = [];

        public Task LogAsync(
            string actionType,
            string entityName,
            string? entityId,
            int? userId,
            bool isSuccess,
            string severity,
            object? details,
            string? clientIp,
            string? clientApp,
            CancellationToken ct)
        {
            ActionTypes.Add(actionType);
            return Task.CompletedTask;
        }
    }
}
