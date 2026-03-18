namespace SP26InventoryManagement.DTOs;

public class DraftIssueHeaderDto
{
    public long TransactionId { get; init; }

    public string TransactionNo { get; init; } = string.Empty;

    public int WarehouseId { get; init; }

    public string WarehouseName { get; init; } = string.Empty;

    public string? CustomerName { get; init; }

    public DateTime TransactionDate { get; init; }

    public decimal TotalAmount { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }
}
