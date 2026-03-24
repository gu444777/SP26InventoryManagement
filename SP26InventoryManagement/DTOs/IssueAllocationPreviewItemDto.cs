namespace SP26InventoryManagement.DTOs;

public class IssueAllocationPreviewItemDto
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long ProductLotId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public decimal AvailableQtyBeforeAllocation { get; init; }

    public decimal AllocatedQty { get; init; }

    public decimal UnitCost { get; init; }

    public decimal? UnitPrice { get; init; }

    public decimal CogsAmount { get; init; }

    public decimal LineAmount { get; init; }

    public string AllocationRule { get; init; } = string.Empty;

    public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";
}
