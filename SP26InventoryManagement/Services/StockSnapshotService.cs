using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class StockSnapshotService : IStockSnapshotService
{
    private readonly Sp26inventoryManagementDbContext _dbContext;
    private readonly ISessionValidationService _sessionValidationService;

    public StockSnapshotService(
        Sp26inventoryManagementDbContext dbContext,
        ISessionValidationService sessionValidationService)
    {
        _dbContext = dbContext;
        _sessionValidationService = sessionValidationService;
    }

    public async Task<IReadOnlyList<StockSnapshotDto>> GetCurrentStockSnapshotAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.VwCurrentStockSnapshots
            .AsNoTracking()
            .OrderBy(snapshot => snapshot.WarehouseCode)
            .ThenBy(snapshot => snapshot.Sku)
            .ThenBy(snapshot => snapshot.LotCode)
            .Select(snapshot => new StockSnapshotDto
            {
                WarehouseId = snapshot.WarehouseId,
                WarehouseCode = snapshot.WarehouseCode,
                WarehouseName = snapshot.WarehouseName,
                ProductId = snapshot.ProductId,
                Sku = snapshot.Sku,
                ProductName = snapshot.ProductName,
                ProductLotId = snapshot.ProductLotId,
                LotCode = snapshot.LotCode,
                ReceivedDate = snapshot.ReceivedDate,
                ExpiryDate = snapshot.ExpiryDate,
                UnitCost = snapshot.UnitCost,
                OnHandQty = snapshot.OnHandQty,
                AllocatedQty = snapshot.AllocatedQty,
                AvailableQty = snapshot.AvailableQty ?? 0m,
                LastMovementAt = snapshot.LastMovementAt
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
