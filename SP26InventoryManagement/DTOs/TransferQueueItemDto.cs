namespace SP26InventoryManagement.DTOs;

public class TransferQueueItemDto
{
    public long TransferOrderId { get; init; }

    public string TransferNo { get; init; } = string.Empty;

    public int SourceWarehouseId { get; init; }

    public string SourceWarehouseName { get; init; } = string.Empty;

    public int DestinationWarehouseId { get; init; }

    public string DestinationWarehouseName { get; init; } = string.Empty;

    public string TransferStatus { get; init; } = string.Empty;

    public DateTime RequestDate { get; init; }

    public DateOnly? RequiredDate { get; init; }

    public string? CreatedBy { get; init; }

    public DateTime CreatedAt { get; init; }
}
