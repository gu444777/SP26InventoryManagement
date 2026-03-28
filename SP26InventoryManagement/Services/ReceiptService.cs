using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class ReceiptService : IReceiptService
{
    private const string ReceiptTransactionType = "RECEIPT";
    private const string DocumentStatusDraft = "DRAFT";
    private const string DocumentStatusPosted = "POSTED";
    private const string DocumentStatusCancelled = "CANCELLED";
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string ManagerRoleCode = "MANAGER";
    private const string AdminRoleCode = "ADMIN";

    private readonly Sp26inventoryManagementDbContext _dbContext;
    private readonly ISessionValidationService _sessionValidationService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IAuditLogService _auditLogService;

    public ReceiptService(
        Sp26inventoryManagementDbContext dbContext,
        ISessionValidationService sessionValidationService,
        CurrentUserContext currentUserContext,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _sessionValidationService = sessionValidationService;
        _currentUserContext = currentUserContext;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyList<WarehouseLookupDto>> GetActiveWarehousesAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.IsActive)
            .OrderBy(warehouse => warehouse.WarehouseCode)
            .Select(warehouse => new WarehouseLookupDto
            {
                WarehouseId = warehouse.WarehouseId,
                WarehouseCode = warehouse.WarehouseCode,
                WarehouseName = warehouse.WarehouseName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductLookupDto>> GetActiveProductsAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Sku)
            .Select(product => new ProductLookupDto
            {
                ProductId = product.ProductId,
                Sku = product.Sku,
                ProductName = product.ProductName,
                BaseUom = product.BaseUom
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierLookupDto>> GetActiveSuppliersAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.IsActive)
            .OrderBy(supplier => supplier.SupplierCode)
            .Select(supplier => new SupplierLookupDto
            {
                SupplierId = supplier.SupplierId,
                SupplierCode = supplier.SupplierCode,
                SupplierName = supplier.SupplierName
            })
            .ToListAsync(ct);
    }

    public async Task<CreateReceiptResult> CreateReceiptAsync(ReceiptRequestDto request, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, StaffRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return CreateReceiptResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        NormalizedLineResult normalizedLineResult = NormalizeLines(request.Lines);
        if (!normalizedLineResult.IsSuccess)
        {
            return CreateReceiptResult.Failure(normalizedLineResult.ErrorMessage ?? "Invalid receipt lines.");
        }

        if (request.WarehouseId <= 0)
        {
            return CreateReceiptResult.Failure("Warehouse is required.");
        }

        bool warehouseExists = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(warehouse => warehouse.WarehouseId == request.WarehouseId && warehouse.IsActive, ct);
        if (!warehouseExists)
        {
            return CreateReceiptResult.Failure("Selected warehouse is invalid or inactive.");
        }

        if (request.SupplierId.HasValue)
        {
            bool supplierExists = await _dbContext.Suppliers
                .AsNoTracking()
                .AnyAsync(supplier => supplier.SupplierId == request.SupplierId.Value && supplier.IsActive, ct);
            if (!supplierExists)
            {
                return CreateReceiptResult.Failure("Selected supplier is invalid or inactive.");
            }
        }

        IReadOnlyCollection<int> productIds = normalizedLineResult.Lines.Select(line => line.ProductId).Distinct().ToArray();
        int activeProductCount = await _dbContext.Products
            .AsNoTracking()
            .CountAsync(product => productIds.Contains(product.ProductId) && product.IsActive, ct);
        if (activeProductCount != productIds.Count)
        {
            return CreateReceiptResult.Failure("One or more selected products are invalid or inactive.");
        }

        _dbContext.ChangeTracker.Clear();

        DateTime now = DateTime.UtcNow;
        DateTime transactionDate = request.TransactionDate == default ? now : request.TransactionDate;
        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            string transactionNo = await GenerateReceiptTransactionNoAsync(transactionDate, ct);

            StockTransaction transaction = new()
            {
                TransactionNo = transactionNo,
                TransactionType = ReceiptTransactionType,
                DocumentStatus = DocumentStatusDraft,
                WarehouseId = request.WarehouseId,
                TransactionDate = transactionDate,
                SupplierId = request.SupplierId,
                ReferenceType = "PURCHASE",
                ReferenceNo = NormalizeNullableText(request.ReferenceNo),
                Remarks = NormalizeNullableText(request.Remarks),
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                TotalAmount = RoundMoney(normalizedLineResult.Lines.Sum(line => line.LineAmount ?? 0m)),
                RowVersion = Array.Empty<byte>()
            };

            _dbContext.StockTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync(ct);

            List<StockTransactionLine> lines = [];
            int lineNo = 1;

            foreach (NormalizedReceiptLine line in normalizedLineResult.Lines)
            {
                ProductLot productLot = await GetOrCreateDraftLotAsync(request.WarehouseId, request.SupplierId, line, now, ct);

                lines.Add(new StockTransactionLine
                {
                    TransactionId = transaction.TransactionId,
                    LineNo = lineNo++,
                    ProductId = line.ProductId,
                    ProductLotId = productLot.ProductLotId,
                    Qty = line.Qty,
                    UnitCost = line.UnitCost,
                    UnitPrice = null,
                    LineAmount = line.LineAmount,
                    CogsAmount = null,
                    ReceivedDateSnapshot = line.ReceivedDate,
                    ExpiryDateSnapshot = line.ExpiryDate,
                    Notes = "RECEIPT_DRAFT",
                    CreatedAt = now,
                    RowVersion = Array.Empty<byte>()
                });
            }

            _dbContext.StockTransactionLines.AddRange(lines);
            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CREATE_RECEIPT",
                entityName: "StockTransactions",
                entityId: transaction.TransactionId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    transaction.TransactionNo,
                    transaction.WarehouseId,
                    transaction.SupplierId,
                    LineCount = lines.Count,
                    transaction.TotalAmount
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return CreateReceiptResult.Success(transaction.TransactionId, transaction.TransactionNo);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CreateReceiptResult.Failure("Receipt creation failed because data changed during save. Please retry.");
        }
        catch (DbUpdateException ex)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CreateReceiptResult.Failure($"Receipt creation failed due to a database update conflict. {ex.GetBaseException().Message}");
        }
    }

    public async Task<IReadOnlyList<DraftReceiptHeaderDto>> GetDraftReceiptsAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.StockTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TransactionType == ReceiptTransactionType &&
                transaction.DocumentStatus == DocumentStatusDraft)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Select(transaction => new DraftReceiptHeaderDto
            {
                TransactionId = transaction.TransactionId,
                TransactionNo = transaction.TransactionNo,
                WarehouseId = transaction.WarehouseId,
                WarehouseName = transaction.Warehouse.WarehouseName,
                SupplierName = transaction.Supplier != null ? transaction.Supplier.SupplierName : null,
                TransactionDate = transaction.TransactionDate,
                TotalAmount = transaction.TotalAmount,
                CreatedBy = transaction.CreatedByUser.Username,
                CreatedAt = transaction.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DraftReceiptLineDto>> GetDraftReceiptLinesAsync(long transactionId, CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.StockTransactionLines
            .AsNoTracking()
            .Where(line =>
                line.TransactionId == transactionId &&
                line.Transaction.TransactionType == ReceiptTransactionType &&
                line.Transaction.DocumentStatus == DocumentStatusDraft)
            .OrderBy(line => line.LineNo)
            .Select(line => new DraftReceiptLineDto
            {
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                Sku = line.Product.Sku,
                ProductName = line.Product.ProductName,
                ProductLotId = line.ProductLotId,
                LotCode = line.ProductLot.LotCode,
                Qty = line.Qty,
                UnitCost = line.UnitCost,
                LineAmount = line.LineAmount,
                ReceivedDateSnapshot = line.ReceivedDateSnapshot,
                ExpiryDateSnapshot = line.ExpiryDateSnapshot
            })
            .ToListAsync(ct);
    }

    public async Task<PostReceiptResult> PostReceiptAsync(long transactionId, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, ManagerRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return PostReceiptResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            DateTime now = DateTime.UtcNow;

            StockTransaction? receipt = await _dbContext.StockTransactions
                .Include(transaction => transaction.StockTransactionLines)
                    .ThenInclude(line => line.ProductLot)
                .FirstOrDefaultAsync(transaction => transaction.TransactionId == transactionId, ct);

            if (receipt is null)
            {
                return PostReceiptResult.Failure("Receipt document not found.");
            }

            if (!string.Equals(receipt.TransactionType, ReceiptTransactionType, StringComparison.OrdinalIgnoreCase))
            {
                return PostReceiptResult.Failure("Selected document is not a receipt transaction.");
            }

            if (string.Equals(receipt.DocumentStatus, DocumentStatusPosted, StringComparison.OrdinalIgnoreCase))
            {
                return PostReceiptResult.Success(receipt.TransactionId, receipt.TransactionNo, receipt.PostedAt ?? now);
            }

            if (!string.Equals(receipt.DocumentStatus, DocumentStatusDraft, StringComparison.OrdinalIgnoreCase))
            {
                return PostReceiptResult.Failure($"Receipt must be in {DocumentStatusDraft} status before posting.");
            }

            if (receipt.StockTransactionLines.Count == 0)
            {
                return PostReceiptResult.Failure("Draft receipt has no lines to post.");
            }

            List<StockTransactionLine> orderedLines = receipt.StockTransactionLines
                .OrderBy(line => line.LineNo)
                .ToList();

            foreach (StockTransactionLine line in orderedLines)
            {
                ProductLot lot = line.ProductLot;

                if (lot.WarehouseId != receipt.WarehouseId || lot.ProductId != line.ProductId)
                {
                    return PostReceiptResult.Failure($"Line {line.LineNo} references an invalid lot.");
                }

                lot.InitialQty = decimal.Round(lot.InitialQty + line.Qty, 3);
                lot.RemainingQty = decimal.Round(lot.RemainingQty + line.Qty, 3);
                lot.Status = "ACTIVE";
                lot.UpdatedAt = now;

                StockBalance? stockBalance = await _dbContext.StockBalances
                    .FirstOrDefaultAsync(balance =>
                        balance.WarehouseId == receipt.WarehouseId &&
                        balance.ProductId == line.ProductId &&
                        balance.ProductLotId == lot.ProductLotId,
                        ct);

                if (stockBalance is null)
                {
                    stockBalance = new StockBalance
                    {
                        WarehouseId = receipt.WarehouseId,
                        ProductId = line.ProductId,
                        ProductLotId = lot.ProductLotId,
                        OnHandQty = decimal.Round(line.Qty, 3),
                        AllocatedQty = 0m,
                        LastMovementAt = now,
                        UpdatedAt = now,
                        RowVersion = Array.Empty<byte>()
                    };

                    _dbContext.StockBalances.Add(stockBalance);
                }
                else
                {
                    stockBalance.OnHandQty = decimal.Round(stockBalance.OnHandQty + line.Qty, 3);
                    stockBalance.LastMovementAt = now;
                    stockBalance.UpdatedAt = now;
                }

                line.Notes = "RECEIPT_POSTED";
            }

            receipt.DocumentStatus = DocumentStatusPosted;
            receipt.PostedByUserId = actorUserId;
            receipt.PostedAt = now;
            receipt.UpdatedAt = now;
            receipt.TotalAmount = RoundMoney(receipt.StockTransactionLines.Sum(line => line.LineAmount ?? (line.Qty * line.UnitCost)));

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "POST_RECEIPT",
                entityName: "StockTransactions",
                entityId: receipt.TransactionId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    receipt.TransactionNo,
                    receipt.WarehouseId,
                    receipt.SupplierId,
                    LineCount = receipt.StockTransactionLines.Count,
                    receipt.TotalAmount
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return PostReceiptResult.Success(receipt.TransactionId, receipt.TransactionNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return PostReceiptResult.Failure("Posting failed because data was modified by another user. Please refresh and retry.");
        }
        catch (DbUpdateException ex)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return PostReceiptResult.Failure($"Posting failed due to a database update conflict. {ex.GetBaseException().Message}");
        }
    }

    public async Task<CancelReceiptResult> CancelDraftReceiptAsync(long transactionId, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, ManagerRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return CancelReceiptResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();

        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            StockTransaction? receipt = await _dbContext.StockTransactions
                .Include(transaction => transaction.StockTransactionLines)
                .FirstOrDefaultAsync(transaction => transaction.TransactionId == transactionId, ct);

            if (receipt is null)
            {
                return CancelReceiptResult.Failure("Receipt document not found.");
            }

            if (!string.Equals(receipt.TransactionType, ReceiptTransactionType, StringComparison.OrdinalIgnoreCase))
            {
                return CancelReceiptResult.Failure("Selected document is not a receipt transaction.");
            }

            if (string.Equals(receipt.DocumentStatus, DocumentStatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                return CancelReceiptResult.Success(receipt.TransactionId, receipt.TransactionNo, receipt.UpdatedAt ?? DateTime.UtcNow);
            }

            if (!string.Equals(receipt.DocumentStatus, DocumentStatusDraft, StringComparison.OrdinalIgnoreCase))
            {
                return CancelReceiptResult.Failure($"Receipt must be in {DocumentStatusDraft} status before cancellation.");
            }

            DateTime now = DateTime.UtcNow;

            receipt.DocumentStatus = DocumentStatusCancelled;
            receipt.PostedByUserId = null;
            receipt.PostedAt = null;
            receipt.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CANCEL_RECEIPT",
                entityName: "StockTransactions",
                entityId: receipt.TransactionId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    receipt.TransactionNo,
                    receipt.WarehouseId,
                    receipt.SupplierId,
                    LineCount = receipt.StockTransactionLines.Count
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return CancelReceiptResult.Success(receipt.TransactionId, receipt.TransactionNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CancelReceiptResult.Failure("Cancellation failed because data was modified by another user. Please refresh and retry.");
        }
        catch (DbUpdateException ex)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CancelReceiptResult.Failure($"Cancellation failed due to a database update conflict. {ex.GetBaseException().Message}");
        }
    }

    private async Task<ProductLot> GetOrCreateDraftLotAsync(
        int warehouseId,
        int? supplierId,
        NormalizedReceiptLine line,
        DateTime now,
        CancellationToken ct)
    {
        ProductLot? existingLot = await _dbContext.ProductLots
            .FirstOrDefaultAsync(productLot =>
                productLot.WarehouseId == warehouseId &&
                productLot.ProductId == line.ProductId &&
                productLot.LotCode == line.LotCode,
                ct);

        if (existingLot is not null)
        {
            if (existingLot.ExpiryDate != line.ExpiryDate || existingLot.ReceivedDate != line.ReceivedDate)
            {
                throw new DbUpdateException($"Lot '{line.LotCode}' already exists with different date information.");
            }

            if (existingLot.SupplierId != supplierId)
            {
                throw new DbUpdateException($"Lot '{line.LotCode}' already exists with a different supplier.");
            }

            if (existingLot.UnitCost != line.UnitCost)
            {
                throw new DbUpdateException($"Lot '{line.LotCode}' already exists with a different unit cost.");
            }

            return existingLot;
        }

        ProductLot newLot = new()
        {
            WarehouseId = warehouseId,
            ProductId = line.ProductId,
            LotCode = line.LotCode,
            ReceivedDate = line.ReceivedDate,
            ExpiryDate = line.ExpiryDate,
            UnitCost = line.UnitCost,
            InitialQty = 0m,
            RemainingQty = 0m,
            SupplierId = supplierId,
            Status = "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>()
        };

        _dbContext.ProductLots.Add(newLot);
        await _dbContext.SaveChangesAsync(ct);
        return newLot;
    }

    private async Task<string> GenerateReceiptTransactionNoAsync(DateTime transactionDate, CancellationToken ct)
    {
        string dateToken = transactionDate.ToString("yyyyMMdd");

        for (int attempt = 0; attempt < 5; attempt++)
        {
            string candidate = $"RECEIPT-{dateToken}-{DateTime.UtcNow:HHmmssfff}";
            bool exists = await _dbContext.StockTransactions
                .AsNoTracking()
                .AnyAsync(transaction => transaction.TransactionNo == candidate, ct);

            if (!exists)
            {
                return candidate;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), ct);
        }

        return $"RECEIPT-{dateToken}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
    }

    private Task<IDbContextTransaction> BeginTransactionAsync(bool serializable, CancellationToken ct)
    {
        if (serializable && _dbContext.Database.IsRelational())
        {
            return _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        }

        return _dbContext.Database.BeginTransactionAsync(ct);
    }

    private async Task<OperationResult> EnsureRoleAsync(int actorUserId, string requiredRoleCode, CancellationToken ct)
    {
        OperationResult sessionValidation = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, null, ct);
        if (!sessionValidation.IsSuccess)
        {
            return sessionValidation;
        }

        if (_currentUserContext.IsInRole(requiredRoleCode) || _currentUserContext.IsInRole(AdminRoleCode))
        {
            return OperationResult.Success();
        }

        return OperationResult.Failure($"Access denied. Role '{requiredRoleCode}' is required.");
    }

    private async Task EnsureCurrentSessionOrThrowAsync(CancellationToken ct)
    {
        OperationResult sessionValidation = await _sessionValidationService.EnsureCurrentSessionAsync(null, ct);
        if (!sessionValidation.IsSuccess)
        {
            throw new UnauthorizedAccessException(sessionValidation.ErrorMessage ?? "Session expired.");
        }
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static NormalizedLineResult NormalizeLines(IReadOnlyCollection<ReceiptRequestLineDto> inputLines)
    {
        if (inputLines is null || inputLines.Count == 0)
        {
            return NormalizedLineResult.Failure("At least one receipt line is required.");
        }

        List<NormalizedReceiptLine> normalizedLines = [];

        foreach (ReceiptRequestLineDto line in inputLines)
        {
            string lotCode = line.LotCode.Trim();
            if (line.ProductId <= 0 || string.IsNullOrWhiteSpace(lotCode) || line.Qty <= 0 || line.UnitCost < 0)
            {
                return NormalizedLineResult.Failure("Receipt lines must include valid product, lot, quantity, and unit cost.");
            }

            if (line.ExpiryDate.HasValue && line.ExpiryDate.Value < line.ReceivedDate)
            {
                return NormalizedLineResult.Failure($"Lot '{lotCode}' has expiry date earlier than received date.");
            }

            normalizedLines.Add(new NormalizedReceiptLine
            {
                ProductId = line.ProductId,
                LotCode = lotCode.ToUpperInvariant(),
                Qty = decimal.Round(line.Qty, 3),
                UnitCost = decimal.Round(line.UnitCost, 4),
                ReceivedDate = line.ReceivedDate,
                ExpiryDate = line.ExpiryDate,
                LineAmount = RoundMoney(line.Qty * line.UnitCost)
            });
        }

        return NormalizedLineResult.Success(normalizedLines);
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class NormalizedReceiptLine
    {
        public int ProductId { get; init; }

        public string LotCode { get; init; } = string.Empty;

        public decimal Qty { get; init; }

        public decimal UnitCost { get; init; }

        public DateOnly ReceivedDate { get; init; }

        public DateOnly? ExpiryDate { get; init; }

        public decimal? LineAmount { get; init; }
    }

    private sealed class NormalizedLineResult
    {
        public bool IsSuccess { get; private init; }

        public string? ErrorMessage { get; private init; }

        public List<NormalizedReceiptLine> Lines { get; private init; } = [];

        public static NormalizedLineResult Success(List<NormalizedReceiptLine> lines)
        {
            return new NormalizedLineResult
            {
                IsSuccess = true,
                Lines = lines
            };
        }

        public static NormalizedLineResult Failure(string errorMessage)
        {
            return new NormalizedLineResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
