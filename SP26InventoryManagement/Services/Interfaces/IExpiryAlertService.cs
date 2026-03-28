using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IExpiryAlertService
{
    Task<IReadOnlyList<ExpiryAlertDto>> GetExpiryAlertsAsync(CancellationToken ct);
}
