using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IGrossProfitReportService
{
    Task<IReadOnlyList<GrossProfitReportRowDto>> GetGrossProfitReportAsync(CancellationToken ct);
}
