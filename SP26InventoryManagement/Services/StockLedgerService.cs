using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class StockLedgerService : IStockLedgerService
{
    private const string DocumentStatusPosted = "POSTED";
    private const string ReceiptTransactionType = "RECEIPT";
    private const string IssueTransactionType = "ISSUE";

    private readonly Sp26inventoryManagementDbContext _dbContext;
    private readonly ISessionValidationService _sessionValidationService;

    public StockLedgerService(
        Sp26inventoryManagementDbContext dbContext,
        ISessionValidationService sessionValidationService)
    {
        _dbContext = dbContext;
        _sessionValidationService = sessionValidationService;
    }

    public async Task<IReadOnlyList<StockLedgerEntryDto>> GetStockLedgerAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.StockTransactionLines
            .AsNoTracking()
            .Where(line =>
                line.Transaction.DocumentStatus == DocumentStatusPosted &&
                (line.Transaction.TransactionType == ReceiptTransactionType ||
                 line.Transaction.TransactionType == IssueTransactionType))
            .OrderByDescending(line => line.Transaction.TransactionDate)
            .ThenByDescending(line => line.Transaction.PostedAt)
            .ThenByDescending(line => line.TransactionId)
            .ThenByDescending(line => line.LineNo)
            .Select(line => new StockLedgerEntryDto
            {
                TransactionId = line.TransactionId,
                TransactionNo = line.Transaction.TransactionNo,
                TransactionType = line.Transaction.TransactionType,
                DocumentStatus = line.Transaction.DocumentStatus,
                LineNo = line.LineNo,
                TransactionDate = line.Transaction.TransactionDate,
                PostedAt = line.Transaction.PostedAt,
                WarehouseId = line.Transaction.WarehouseId,
                WarehouseCode = line.Transaction.Warehouse.WarehouseCode,
                WarehouseName = line.Transaction.Warehouse.WarehouseName,
                ProductId = line.ProductId,
                Sku = line.Product.Sku,
                ProductName = line.Product.ProductName,
                ProductLotId = line.ProductLotId,
                LotCode = line.ProductLot.LotCode,
                ReceivedDate = line.ReceivedDateSnapshot,
                ExpiryDate = line.ExpiryDateSnapshot,
                UnitCost = line.UnitCost,
                UnitPrice = line.UnitPrice,
                QtyIn = line.Transaction.TransactionType == ReceiptTransactionType ? line.Qty : 0m,
                QtyOut = line.Transaction.TransactionType == IssueTransactionType ? line.Qty : 0m,
                SignedQty = line.Transaction.TransactionType == ReceiptTransactionType ? line.Qty : -line.Qty,
                LineAmount = line.LineAmount,
                CogsAmount = line.CogsAmount,
                ReferenceType = line.Transaction.ReferenceType ?? string.Empty,
                ReferenceNo = line.Transaction.ReferenceNo ?? string.Empty,
                CounterpartyName = line.Transaction.TransactionType == ReceiptTransactionType
                    ? (line.Transaction.Supplier != null ? line.Transaction.Supplier.SupplierName : string.Empty)
                    : (line.Transaction.Customer != null ? line.Transaction.Customer.CustomerName : string.Empty),
                Remarks = line.Transaction.Remarks ?? string.Empty,
                CreatedBy = line.Transaction.CreatedByUser.Username,
                PostedBy = line.Transaction.PostedByUser != null ? line.Transaction.PostedByUser.Username : string.Empty
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
