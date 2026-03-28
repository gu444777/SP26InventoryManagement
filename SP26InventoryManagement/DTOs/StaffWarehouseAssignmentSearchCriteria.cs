namespace SP26InventoryManagement.DTOs;

public class StaffWarehouseAssignmentSearchCriteria
{
    public string? SearchText { get; init; }

    public bool? IsActive { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
