namespace SP26InventoryManagement.DTOs;

public class TransferDetailLineDto
{
    public long TransferOrderLineId { get; init; }

    public int LineNo { get; init; }

    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal RequestedQty { get; init; }

    public decimal DispatchedQty { get; init; }

    public decimal ReceivedQty { get; init; }

    public IReadOnlyList<TransferDetailLotDto> Lots { get; init; } = Array.Empty<TransferDetailLotDto>();
}
