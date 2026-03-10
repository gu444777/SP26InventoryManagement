using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly Sp26inventoryManagementDbContext _dbContext;

    public RoleRepository(Sp26inventoryManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RoleOptionDto>> GetActiveRolesAsync(CancellationToken ct)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.IsActive)
            .OrderBy(role => role.RoleCode)
            .Select(role => new RoleOptionDto
            {
                RoleId = role.RoleId,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Role>> GetActiveRolesByIdsAsync(IReadOnlyCollection<int> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
        {
            return Array.Empty<Role>();
        }

        return await _dbContext.Roles
            .Where(role => role.IsActive && roleIds.Contains(role.RoleId))
            .ToListAsync(ct);
    }

    public async Task<int?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct)
    {
        return await _dbContext.Roles
            .AsNoTracking()
            .Where(role => role.RoleCode == roleCode && role.IsActive)
            .Select(role => (int?)role.RoleId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Dictionary<int, string>> GetRoleCodeMapAsync(IReadOnlyCollection<int> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _dbContext.Roles
            .AsNoTracking()
            .Where(role => roleIds.Contains(role.RoleId))
            .ToDictionaryAsync(role => role.RoleId, role => role.RoleCode, ct);
    }
}
