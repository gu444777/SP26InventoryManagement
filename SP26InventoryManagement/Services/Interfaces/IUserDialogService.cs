namespace SP26InventoryManagement.Services;

public interface IUserDialogService
{
    Task<bool> ShowCreateUserDialogAsync(CancellationToken ct);

    Task ShowStaffWarehouseAssignmentDialogAsync(CancellationToken ct);

    Task ShowChangePasswordDialogAsync(int userId, string username, CancellationToken ct);
}
