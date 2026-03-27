using Microsoft.EntityFrameworkCore;
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

    private static TestHarness CreateHarness()
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"UserManagementServiceTests-{Guid.NewGuid():N}")
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

        dbContext.Users.Add(new User
        {
            UserId = 1,
            Username = "admin",
            PasswordHash = "HASHED",
            FullName = "Admin",
            IsActive = true,
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

        dbContext.UserRoles.Add(new UserRole
        {
            UserId = 1,
            RoleId = 1,
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
