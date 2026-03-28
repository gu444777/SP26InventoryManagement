using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<WarehouseLookupDto>> GetActiveWarehousesAsync(int actorUserId, CancellationToken ct);

    Task<PagedResult<UserListItemDto>> SearchUsersAsync(UserSearchCriteria criteria, int actorUserId, CancellationToken ct);

    Task<PagedResult<StaffWarehouseAssignmentItemDto>> GetStaffWarehouseAssignmentsAsync(
        StaffWarehouseAssignmentSearchCriteria criteria,
        int actorUserId,
        CancellationToken ct);

    Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, int actorUserId, CancellationToken ct);

    Task<OperationResult> AssignOrChangeStaffWarehouseAsync(
        int staffUserId,
        int warehouseId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct);

    Task<OperationResult> SetUserRolesAsync(
        int targetUserId,
        IReadOnlyCollection<int> roleIds,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct);

    Task<ResetPasswordResult> ResetPasswordAsync(
        int targetUserId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct);

    Task<OperationResult> DeactivateUserAsync(
        int targetUserId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct);

    Task<OperationResult> ReactivateUserAsync(
        int targetUserId,
        byte[] expectedUserRowVersion,
        int actorUserId,
        CancellationToken ct);
}
