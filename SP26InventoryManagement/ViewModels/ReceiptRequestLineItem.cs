namespace SP26InventoryManagement.ViewModels;

public class ReceiptRequestLineItem
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string LotCode { get; init; } = string.Empty;

    public decimal Qty { get; init; }

    public decimal UnitCost { get; init; }

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";
}
