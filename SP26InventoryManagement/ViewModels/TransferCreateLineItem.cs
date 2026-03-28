namespace SP26InventoryManagement.ViewModels;

public class TransferCreateLineItem
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal RequestedQty { get; init; }
}
