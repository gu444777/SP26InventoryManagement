using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class GrossProfitReportService : IGrossProfitReportService
{
    private const string IssueTransactionType = "ISSUE";
    private const string DocumentStatusPosted = "POSTED";

    private readonly Sp26inventoryManagementDbContext _dbContext;
    private readonly ISessionValidationService _sessionValidationService;

    public GrossProfitReportService(
        Sp26inventoryManagementDbContext dbContext,
        ISessionValidationService sessionValidationService)
    {
        _dbContext = dbContext;
        _sessionValidationService = sessionValidationService;
    }

    public async Task<IReadOnlyList<GrossProfitReportRowDto>> GetGrossProfitReportAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.StockTransactionLines
            .AsNoTracking()
            .Where(line =>
                line.Transaction.TransactionType == IssueTransactionType &&
                line.Transaction.DocumentStatus == DocumentStatusPosted)
            .OrderByDescending(line => line.Transaction.TransactionDate)
            .ThenByDescending(line => line.Transaction.PostedAt)
            .ThenByDescending(line => line.TransactionId)
            .ThenBy(line => line.LineNo)
            .Select(line => new GrossProfitReportRowDto
            {
                TransactionId = line.TransactionId,
                TransactionNo = line.Transaction.TransactionNo,
                TransactionDate = line.Transaction.TransactionDate,
                PostedAt = line.Transaction.PostedAt,
                WarehouseId = line.Transaction.WarehouseId,
                WarehouseCode = line.Transaction.Warehouse.WarehouseCode,
                WarehouseName = line.Transaction.Warehouse.WarehouseName,
                CustomerName = line.Transaction.Customer != null ? line.Transaction.Customer.CustomerName : string.Empty,
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                Sku = line.Product.Sku,
                ProductName = line.Product.ProductName,
                ProductLotId = line.ProductLotId,
                LotCode = line.ProductLot.LotCode,
                Qty = line.Qty,
                UnitCost = line.UnitCost,
                UnitPrice = line.UnitPrice,
                SalesAmount = line.LineAmount ?? decimal.Round(line.Qty * (line.UnitPrice ?? line.UnitCost), 2, MidpointRounding.AwayFromZero),
                CogsAmount = line.CogsAmount ?? decimal.Round(line.Qty * line.UnitCost, 2, MidpointRounding.AwayFromZero),
                GrossProfitAmount =
                    (line.LineAmount ?? decimal.Round(line.Qty * (line.UnitPrice ?? line.UnitCost), 2, MidpointRounding.AwayFromZero))
                    - (line.CogsAmount ?? decimal.Round(line.Qty * line.UnitCost, 2, MidpointRounding.AwayFromZero)),
                GrossMarginPct =
                    (line.LineAmount ?? decimal.Round(line.Qty * (line.UnitPrice ?? line.UnitCost), 2, MidpointRounding.AwayFromZero)) <= 0
                        ? 0m
                        : decimal.Round(
                            (((line.LineAmount ?? decimal.Round(line.Qty * (line.UnitPrice ?? line.UnitCost), 2, MidpointRounding.AwayFromZero))
                              - (line.CogsAmount ?? decimal.Round(line.Qty * line.UnitCost, 2, MidpointRounding.AwayFromZero)))
                             / (line.LineAmount ?? decimal.Round(line.Qty * (line.UnitPrice ?? line.UnitCost), 2, MidpointRounding.AwayFromZero))) * 100m,
                            2,
                            MidpointRounding.AwayFromZero)
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
