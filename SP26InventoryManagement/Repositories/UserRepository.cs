using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public class UserRepository : IUserRepository
{
    private readonly Sp26inventoryManagementDbContext _dbContext;

    public UserRepository(Sp26inventoryManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> GetByUsernameWithRolesAsync(string username, CancellationToken ct)
    {
        return _dbContext.Users
            .Include(user => user.UserRoleUsers)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.Username == username, ct);
    }

    public Task<User?> GetByIdWithRolesAsync(int userId, CancellationToken ct)
    {
        return _dbContext.Users
            .Include(user => user.UserRoleUsers)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(user => user.UserId == userId, ct);
    }

    public Task<User?> GetByIdAsync(int userId, CancellationToken ct)
    {
        return _dbContext.Users
            .FirstOrDefaultAsync(user => user.UserId == userId, ct);
    }

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct)
    {
        return _dbContext.Users.AnyAsync(user => user.Username == username, ct);
    }

    public Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken ct)
    {
        IQueryable<User> query = _dbContext.Users.Where(user => user.Email == email);

        if (excludeUserId.HasValue)
        {
            query = query.Where(user => user.UserId != excludeUserId.Value);
        }

        return query.AnyAsync(ct);
    }

    public async Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchCriteria criteria, CancellationToken ct)
    {
        int pageNumber = criteria.PageNumber <= 0 ? 1 : criteria.PageNumber;
        int pageSize = criteria.PageSize <= 0 ? 20 : criteria.PageSize;

        IQueryable<User> query = _dbContext.Users.AsNoTracking();

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

        if (criteria.RoleId.HasValue)
        {
            int roleId = criteria.RoleId.Value;
            query = query.Where(user => user.UserRoleUsers.Any(userRole => userRole.RoleId == roleId && userRole.Role.IsActive));
        }

        int totalCount = await query.CountAsync(ct);

        List<User> users = await query
            .OrderBy(user => user.Username)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(user => user.UserRoleUsers)
            .ThenInclude(userRole => userRole.Role)
            .ToListAsync(ct);

        IReadOnlyList<UserListItemDto> items = users
            .Select(user => new UserListItemDto
            {
                UserId = user.UserId,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                RowVersion = user.RowVersion.ToArray(),
                Roles = user.UserRoleUsers
                    .Where(userRole => userRole.Role.IsActive)
                    .OrderBy(userRole => userRole.Role.RoleCode)
                    .Select(userRole => new RoleOptionDto
                    {
                        RoleId = userRole.RoleId,
                        RoleCode = userRole.Role.RoleCode,
                        RoleName = userRole.Role.RoleName
                    })
                    .ToList()
            })
            .ToList();

        return new PagedResult<UserListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(ct);
    }
}
