using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IStockSnapshotService
{
    Task<IReadOnlyList<StockSnapshotDto>> GetCurrentStockSnapshotAsync(CancellationToken ct);
}
