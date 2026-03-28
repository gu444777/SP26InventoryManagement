namespace SP26InventoryManagement.DTOs;

public class TransferDetailLotDto
{
    public long TransferLotAllocationId { get; init; }

    public int LineNo { get; init; }

    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long SourceProductLotId { get; init; }

    public long? DestinationProductLotId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public DateOnly ReceivedDateSnapshot { get; init; }

    public DateOnly? ExpiryDateSnapshot { get; init; }

    public decimal UnitCost { get; init; }

    public decimal DispatchedQty { get; init; }

    public decimal ReceivedQty { get; init; }

    public string ExpiryDisplay => ExpiryDateSnapshot.HasValue ? ExpiryDateSnapshot.Value.ToString("yyyy-MM-dd") : "-";
}
