using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameWithRolesAsync(string username, CancellationToken ct);

    Task<User?> GetByIdWithRolesAsync(int userId, CancellationToken ct);

    Task<User?> GetByIdAsync(int userId, CancellationToken ct);

    Task<bool> UsernameExistsAsync(string username, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, int? excludeUserId, CancellationToken ct);

    Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchCriteria criteria, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    Task UpdateAsync(User user, CancellationToken ct);
}
