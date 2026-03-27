namespace SP26InventoryManagement.DTOs;

public class TransferSuggestionRequestDto
{
    public int SourceWarehouseId { get; init; }

    public int DestinationWarehouseId { get; init; }

    public DateTime RequestDate { get; init; }

    public IReadOnlyCollection<TransferSuggestionLineDto> Lines { get; init; } = Array.Empty<TransferSuggestionLineDto>();
}
