namespace SP26InventoryManagement.DTOs;

public class DraftReceiptLineDto
{
    public int LineNo { get; init; }

    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long ProductLotId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public decimal Qty { get; init; }

    public decimal UnitCost { get; init; }

    public decimal? LineAmount { get; init; }

    public DateOnly ReceivedDateSnapshot { get; init; }

    public DateOnly? ExpiryDateSnapshot { get; init; }

    public string ExpiryDisplay => ExpiryDateSnapshot.HasValue ? ExpiryDateSnapshot.Value.ToString("yyyy-MM-dd") : "-";
}
