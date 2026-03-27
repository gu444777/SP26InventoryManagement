using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IIssueService
{
    Task<IReadOnlyList<WarehouseLookupDto>> GetActiveWarehousesAsync(int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<ProductLookupDto>> GetActiveProductsAsync(CancellationToken ct);

    Task<IReadOnlyList<CustomerLookupDto>> GetActiveCustomersAsync(CancellationToken ct);

    Task<decimal> GetAvailableQtyAsync(int warehouseId, int productId, DateTime transactionDate, int actorUserId, CancellationToken ct);

    Task<PreviewIssueAllocationResult> PreviewLotAllocationAsync(IssueRequestDto request, int actorUserId, CancellationToken ct);

    Task<CreateIssueResult> CreateIssueAsync(IssueRequestDto request, int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<DraftIssueHeaderDto>> GetDraftIssuesAsync(int actorUserId, CancellationToken ct);

    Task<IReadOnlyList<DraftIssueLineDto>> GetDraftIssueLinesAsync(long transactionId, int actorUserId, CancellationToken ct);

    Task<PostIssueResult> PostIssueAsync(long transactionId, int actorUserId, CancellationToken ct);

    Task<CancelIssueResult> CancelDraftIssueAsync(long transactionId, int actorUserId, CancellationToken ct);
}
