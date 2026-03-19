namespace SP26InventoryManagement.ViewModels;

public class IssueRequestLineItem
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Qty { get; init; }

    public decimal? UnitPrice { get; init; }

    public string UnitPriceDisplay => UnitPrice.HasValue ? UnitPrice.Value.ToString("N4") : "-";
}
