using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public class UserRoleRepository : IUserRoleRepository
{
    private readonly Sp26inventoryManagementDbContext _dbContext;

    public UserRoleRepository(Sp26inventoryManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<int>> GetRoleIdsByUserIdAsync(int userId, CancellationToken ct)
    {
        return await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.RoleId)
            .ToListAsync(ct);
    }

    public async Task<RoleSyncResult> SyncRolesAsync(int userId, IReadOnlyCollection<int> targetRoleIds, int actorUserId, CancellationToken ct)
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
            bool roleExistsInDb = existingRoleIdsInDb.Contains(localEntry.Entity.RoleId);

            if (localEntry.State == EntityState.Added)
            {
                // Added role already exists in DB or is not requested anymore => stale local entry.
                if (roleExistsInDb || !normalizedRoleIds.Contains(localEntry.Entity.RoleId))
                {
                    localEntry.State = EntityState.Detached;
                }
            }
            else if (localEntry.State == EntityState.Deleted && normalizedRoleIds.Contains(localEntry.Entity.RoleId))
            {
                // Keep requested role in current operation.
                localEntry.State = EntityState.Unchanged;
            }
        }

        HashSet<int> existingRoleIds = existingRoles
            .Select(userRole => userRole.RoleId)
            .Concat(_dbContext.ChangeTracker
                .Entries<UserRole>()
                .Where(entry =>
                    entry.Entity.UserId == userId &&
                    entry.State != EntityState.Detached &&
                    entry.State != EntityState.Deleted)
                .Select(entry => entry.Entity.RoleId))
            .ToHashSet();

        IReadOnlyCollection<int> addedRoleIds = normalizedRoleIds.Except(existingRoleIds).ToList();
        List<UserRole> removedRoles = existingRoles.Where(userRole => !normalizedRoleIds.Contains(userRole.RoleId)).ToList();

        if (removedRoles.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(removedRoles);
        }

        foreach (int roleId in addedRoleIds)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = actorUserId
            });
        }

        await _dbContext.SaveChangesAsync(ct);

        return new RoleSyncResult
        {
            AddedRoleIds = addedRoleIds,
            RemovedRoleIds = removedRoles.Select(userRole => userRole.RoleId).ToList()
        };
    }
}
