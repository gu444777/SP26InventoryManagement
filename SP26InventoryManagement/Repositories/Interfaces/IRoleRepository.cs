using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public interface IRoleRepository
{
    Task<IReadOnlyList<RoleOptionDto>> GetActiveRolesAsync(CancellationToken ct);

    Task<IReadOnlyList<Role>> GetActiveRolesByIdsAsync(IReadOnlyCollection<int> roleIds, CancellationToken ct);

    Task<int?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct);

    Task<Dictionary<int, string>> GetRoleCodeMapAsync(IReadOnlyCollection<int> roleIds, CancellationToken ct);
}
