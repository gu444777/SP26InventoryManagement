namespace SP26InventoryManagement.DTOs;

public class TransferLotSuggestionItemDto
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long SourceProductLotId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public decimal AvailableQtyBeforeAllocation { get; init; }

    public decimal SuggestedQty { get; init; }

    public decimal UnitCost { get; init; }

    public string AllocationRule { get; init; } = string.Empty;

    public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";
}
