using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.Test;

public class SessionValidationServiceTests
{
    [Fact]
    public async Task EnsureSessionForUserAsync_ShouldFailAndClearContext_WhenAuthVersionMismatch()
    {
        TestHarness harness = CreateHarness();

        User user = await harness.DbContext.Users.SingleAsync(item => item.UserId == 1);
        user.AuthVersion = 2;
        await harness.DbContext.SaveChangesAsync();

        harness.CurrentUserContext.SetUser(1, "admin", "Admin", ["ADMIN"], authVersion: 1);

        var result = await harness.Service.EnsureSessionForUserAsync(
            expectedUserId: 1,
            requiredRoleCode: "ADMIN",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Session invalidated because your permissions changed", result.ErrorMessage ?? string.Empty);
        Assert.False(harness.CurrentUserContext.IsAuthenticated);
        Assert.Null(harness.CurrentUserContext.UserId);
    }

    [Fact]
    public async Task EnsureSessionForUserAsync_ShouldFailAndClearContext_WhenUserInactive()
    {
        TestHarness harness = CreateHarness();

        User user = await harness.DbContext.Users.SingleAsync(item => item.UserId == 1);
        user.IsActive = false;
        await harness.DbContext.SaveChangesAsync();

        harness.CurrentUserContext.SetUser(1, "admin", "Admin", ["ADMIN"], authVersion: 1);

        var result = await harness.Service.EnsureSessionForUserAsync(
            expectedUserId: 1,
            requiredRoleCode: "ADMIN",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Session invalidated because your permissions changed", result.ErrorMessage ?? string.Empty);
        Assert.False(harness.CurrentUserContext.IsAuthenticated);
        Assert.Null(harness.CurrentUserContext.UserId);
    }

    [Fact]
    public async Task EnsureSessionForUserAsync_ShouldPassAndKeepAuthVersion_WhenFingerprintMatches()
    {
        TestHarness harness = CreateHarness();

        User user = await harness.DbContext.Users.SingleAsync(item => item.UserId == 1);
        user.AuthVersion = 3;
        await harness.DbContext.SaveChangesAsync();

        harness.CurrentUserContext.SetUser(1, "admin", "Admin", [], authVersion: 3);

        var result = await harness.Service.EnsureSessionForUserAsync(
            expectedUserId: 1,
            requiredRoleCode: "ADMIN",
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(1, harness.CurrentUserContext.UserId);
        Assert.Equal(3, harness.CurrentUserContext.AuthVersion);
        Assert.True(harness.CurrentUserContext.IsInRole("ADMIN"));
    }

    private static TestHarness CreateHarness()
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"SessionValidationServiceTests-{Guid.NewGuid():N}")
            .Options;

        var dbContext = new Sp26inventoryManagementDbContext(options);
        SeedReferenceData(dbContext);

        var currentUserContext = new CurrentUserContext();
        var service = new SessionValidationService(currentUserContext, new UserRepository(dbContext));

        return new TestHarness(dbContext, currentUserContext, service);
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
            AuthVersion = 1,
            CreatedAt = now,
            RowVersion = [1]
        });

        dbContext.Roles.Add(new Role
        {
            RoleId = 1,
            RoleCode = "ADMIN",
            RoleName = "Admin",
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

        dbContext.SaveChanges();
    }

    private sealed class TestHarness(
        Sp26inventoryManagementDbContext dbContext,
        CurrentUserContext currentUserContext,
        SessionValidationService service)
    {
        public Sp26inventoryManagementDbContext DbContext { get; } = dbContext;

        public CurrentUserContext CurrentUserContext { get; } = currentUserContext;

        public SessionValidationService Service { get; } = service;
    }
}
