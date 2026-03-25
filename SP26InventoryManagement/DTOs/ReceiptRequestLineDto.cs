namespace SP26InventoryManagement.DTOs;

public class ReceiptRequestLineDto
{
    public int ProductId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public decimal Qty { get; init; }

    public decimal UnitCost { get; init; }

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }
}
