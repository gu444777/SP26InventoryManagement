using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class ExpiryAlertService : IExpiryAlertService
{
    private readonly Sp26inventoryManagementDbContext _dbContext;
    private readonly ISessionValidationService _sessionValidationService;

    public ExpiryAlertService(
        Sp26inventoryManagementDbContext dbContext,
        ISessionValidationService sessionValidationService)
    {
        _dbContext = dbContext;
        _sessionValidationService = sessionValidationService;
    }

    public async Task<IReadOnlyList<ExpiryAlertDto>> GetExpiryAlertsAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.VwExpiryAlerts
            .AsNoTracking()
            .OrderBy(alert => alert.DaysToExpiry.HasValue ? 0 : 1)
            .ThenBy(alert => alert.DaysToExpiry ?? int.MaxValue)
            .ThenBy(alert => alert.WarehouseCode)
            .ThenBy(alert => alert.Sku)
            .ThenBy(alert => alert.LotCode)
            .Select(alert => new ExpiryAlertDto
            {
                WarehouseId = alert.WarehouseId,
                WarehouseCode = alert.WarehouseCode,
                ProductId = alert.ProductId,
                Sku = alert.Sku,
                ProductName = alert.ProductName,
                ProductLotId = alert.ProductLotId,
                LotCode = alert.LotCode,
                ExpiryDate = alert.ExpiryDate,
                RemainingQty = alert.RemainingQty,
                ExpiryStatus = alert.ExpiryStatus,
                DaysToExpiry = alert.DaysToExpiry
            })
            .ToListAsync(ct);
    }

    private async Task EnsureCurrentSessionOrThrowAsync(CancellationToken ct)
    {
        OperationResult sessionValidation = await _sessionValidationService.EnsureCurrentSessionAsync(null, ct);
        if (!sessionValidation.IsSuccess)
        {
            throw new UnauthorizedAccessException(sessionValidation.ErrorMessage ?? "Session expired.");
        }
    }
}
