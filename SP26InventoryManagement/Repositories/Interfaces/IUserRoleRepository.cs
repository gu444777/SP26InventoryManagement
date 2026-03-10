using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Repositories;

public interface IUserRoleRepository
{
    Task<IReadOnlyCollection<int>> GetRoleIdsByUserIdAsync(int userId, CancellationToken ct);

    Task<RoleSyncResult> SyncRolesAsync(int userId, IReadOnlyCollection<int> targetRoleIds, int actorUserId, CancellationToken ct);
}
