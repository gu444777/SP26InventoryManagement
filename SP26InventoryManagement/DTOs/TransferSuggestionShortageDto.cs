namespace SP26InventoryManagement.DTOs;

public class TransferSuggestionShortageDto
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal RequestedQty { get; init; }

    public decimal AvailableQty { get; init; }

    public decimal MissingQty => RequestedQty - AvailableQty;
}
