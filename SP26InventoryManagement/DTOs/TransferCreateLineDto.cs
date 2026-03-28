namespace SP26InventoryManagement.DTOs;

public class TransferCreateLineDto
{
    public int ProductId { get; init; }

    public decimal RequestedQty { get; init; }

    public IReadOnlyCollection<TransferLotSelectionDto> LotSelections { get; init; } = Array.Empty<TransferLotSelectionDto>();
}
