using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IUserManagementService
{
    Task<PagedResult<UserListItemDto>> SearchUsersAsync(UserSearchCriteria criteria, CancellationToken ct);

    Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, int actorUserId, CancellationToken ct);

    Task<OperationResult> SetUserRolesAsync(int targetUserId, IReadOnlyCollection<int> roleIds, int actorUserId, CancellationToken ct);

    Task<ResetPasswordResult> ResetPasswordAsync(int targetUserId, int actorUserId, CancellationToken ct);

    Task<OperationResult> DeactivateUserAsync(int targetUserId, int actorUserId, CancellationToken ct);

    Task<OperationResult> ReactivateUserAsync(int targetUserId, int actorUserId, CancellationToken ct);
}
