using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IReceiptService
{
    Task<IReadOnlyList<WarehouseLookupDto>> GetActiveWarehousesAsync(CancellationToken ct);

    Task<IReadOnlyList<ProductLookupDto>> GetActiveProductsAsync(CancellationToken ct);

    Task<IReadOnlyList<SupplierLookupDto>> GetActiveSuppliersAsync(CancellationToken ct);

    Task<CreateReceiptResult> CreateReceiptAsync(ReceiptRequestDto request, int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<DraftReceiptHeaderDto>> GetDraftReceiptsAsync(CancellationToken ct);

    Task<IReadOnlyList<DraftReceiptLineDto>> GetDraftReceiptLinesAsync(long transactionId, CancellationToken ct);

    Task<PostReceiptResult> PostReceiptAsync(long transactionId, int actorUserId, CancellationToken ct);

    Task<CancelReceiptResult> CancelDraftReceiptAsync(long transactionId, int actorUserId, CancellationToken ct);
}
