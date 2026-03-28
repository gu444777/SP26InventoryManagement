using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface ITransferService
{
    Task<IReadOnlyList<WarehouseLookupDto>> GetAllowedSourceWarehousesAsync(int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<WarehouseLookupDto>> GetActiveDestinationWarehousesAsync(int sourceWarehouseId, CancellationToken ct);

    Task<IReadOnlyList<ProductLookupDto>> GetActiveProductsAsync(CancellationToken ct);

    Task<decimal> GetAvailableQtyAsync(int sourceWarehouseId, int productId, DateTime requestDate, int actorUserId, CancellationToken ct);

    Task<PreviewCreateTransferLotSuggestionResult> PreviewCreateTransferLotSuggestionAsync(
        TransferSuggestionRequestDto request,
        int actorUserId,
        CancellationToken ct);

    Task<CreateTransferResult> CreateTransferAsync(TransferCreateRequestDto request, int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<TransferQueueItemDto>> GetSourceDispatchQueueAsync(int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<TransferQueueItemDto>> GetDestinationReceiptQueueAsync(int actorUserId, CancellationToken ct);

    Task<TransferDetailDto?> GetTransferDetailAsync(long transferOrderId, int actorUserId, CancellationToken ct);

    Task<ConfirmSourceDispatchResult> ConfirmSourceDispatchAsync(long transferOrderId, int actorUserId, CancellationToken ct);

    Task<ConfirmDestinationReceiptResult> ConfirmDestinationReceiptAsync(long transferOrderId, int actorUserId, CancellationToken ct);

    Task<CancelTransferResult> CancelCreatedTransferAsync(long transferOrderId, int actorUserId, CancellationToken ct);
}
