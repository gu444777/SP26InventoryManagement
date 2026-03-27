namespace SP26InventoryManagement.DTOs;

public class StaffWarehouseAssignmentItemDto
{
    public int UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public int? CurrentWarehouseId { get; init; }

    public string CurrentWarehouseDisplay { get; init; } = "Unassigned";
}
