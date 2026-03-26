using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IStockLedgerService
{
    Task<IReadOnlyList<StockLedgerEntryDto>> GetStockLedgerAsync(CancellationToken ct);
}
