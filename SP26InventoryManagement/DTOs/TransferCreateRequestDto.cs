namespace SP26InventoryManagement.DTOs;

public class TransferCreateRequestDto
{
    public int SourceWarehouseId { get; init; }

    public int DestinationWarehouseId { get; init; }

    public DateTime RequestDate { get; init; }

    public DateOnly? RequiredDate { get; init; }

    public string? Remarks { get; init; }

    public IReadOnlyCollection<TransferCreateLineDto> Lines { get; init; } = Array.Empty<TransferCreateLineDto>();
}
