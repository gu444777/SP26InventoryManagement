namespace SP26InventoryManagement.DTOs;

public class ReceiptRequestDto
{
    public int WarehouseId { get; init; }

    public int? SupplierId { get; init; }

    public DateTime TransactionDate { get; init; }

    public string? ReferenceNo { get; init; }

    public string? Remarks { get; init; }

    public IReadOnlyCollection<ReceiptRequestLineDto> Lines { get; init; } = Array.Empty<ReceiptRequestLineDto>();
}
