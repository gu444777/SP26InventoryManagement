using System.Data;
using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class IssueService : IIssueService
{
    private const string IssueTransactionType = "ISSUE";
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

    public IssueService(
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

    public async Task<IReadOnlyList<CustomerLookupDto>> GetActiveCustomersAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.IsActive)
            .OrderBy(customer => customer.CustomerCode)
            .Select(customer => new CustomerLookupDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName
            })
            .ToListAsync(ct);
    }

    public async Task<decimal> GetAvailableQtyAsync(int warehouseId, int productId, DateTime transactionDate, CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        if (warehouseId <= 0 || productId <= 0)
        {
            return 0;
        }

        DateOnly transactionDay = DateOnly.FromDateTime(transactionDate.Date);

        decimal? availableQty = await _dbContext.StockBalances
            .AsNoTracking()
            .Where(balance =>
                balance.WarehouseId == warehouseId &&
                balance.ProductId == productId &&
                balance.ProductLot.Status != "LOCKED" &&
                (!balance.ProductLot.ExpiryDate.HasValue || balance.ProductLot.ExpiryDate.Value >= transactionDay))
            .Select(balance => (decimal?)(balance.AvailableQty ?? (balance.OnHandQty - balance.AllocatedQty)))
            .SumAsync(ct);

        decimal safeAvailableQty = availableQty ?? 0m;
        if (safeAvailableQty <= 0)
        {
            return 0;
        }

        return decimal.Round(safeAvailableQty, 3);
    }

    public async Task<PreviewIssueAllocationResult> PreviewLotAllocationAsync(IssueRequestDto request, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, StaffRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return PreviewIssueAllocationResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        AllocationComputation computation = await ComputeAllocationAsync(request, ct);

        if (!computation.IsSuccess)
        {
            return PreviewIssueAllocationResult.Failure(
                computation.ErrorMessage ?? "Unable to preview lot allocation.",
                computation.Allocations,
                computation.Shortages,
                computation.TotalCogs,
                computation.TotalSales);
        }

        return PreviewIssueAllocationResult.Success(computation.Allocations, computation.TotalCogs, computation.TotalSales);
    }

    public async Task<CreateIssueResult> CreateIssueAsync(IssueRequestDto request, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, StaffRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return CreateIssueResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();

        if (request.CustomerId.HasValue)
        {
            bool customerExists = await _dbContext.Customers
                .AsNoTracking()
                .AnyAsync(customer => customer.CustomerId == request.CustomerId.Value && customer.IsActive, ct);
            if (!customerExists)
            {
                return CreateIssueResult.Failure("Selected customer is invalid or inactive.");
            }
        }

        DateTime now = DateTime.UtcNow;
        DateTime transactionDate = request.TransactionDate == default ? now : request.TransactionDate;
        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            AllocationComputation computation = await ComputeAllocationForReservationAsync(request, now, ct);
            if (!computation.IsSuccess)
            {
                return CreateIssueResult.Failure(computation.ErrorMessage ?? "Unable to create issue document.");
            }

            if (computation.Allocations.Count == 0)
            {
                return CreateIssueResult.Failure("No allocatable stock found for requested products.");
            }

            string transactionNo = await GenerateIssueTransactionNoAsync(transactionDate, ct);

            StockTransaction transaction = new()
            {
                TransactionNo = transactionNo,
                TransactionType = IssueTransactionType,
                DocumentStatus = DocumentStatusDraft,
                WarehouseId = request.WarehouseId,
                TransactionDate = transactionDate,
                CustomerId = request.CustomerId,
                ReferenceType = "SALES",
                ReferenceNo = NormalizeNullableText(request.ReferenceNo),
                Remarks = NormalizeNullableText(request.Remarks),
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                TotalAmount = computation.TotalSales
            };

            _dbContext.StockTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync(ct);

            List<StockTransactionLine> lines = computation.Allocations
                .OrderBy(allocation => allocation.ProductId)
                .ThenBy(allocation => allocation.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(allocation => allocation.ExpiryDate ?? DateOnly.MaxValue)
                .ThenBy(allocation => allocation.ReceivedDate)
                .ThenBy(allocation => allocation.ProductLotId)
                .Select((allocation, index) => new StockTransactionLine
                {
                    TransactionId = transaction.TransactionId,
                    LineNo = index + 1,
                    ProductId = allocation.ProductId,
                    ProductLotId = allocation.ProductLotId,
                    Qty = allocation.AllocatedQty,
                    UnitCost = allocation.UnitCost,
                    UnitPrice = allocation.UnitPrice,
                    CogsAmount = allocation.CogsAmount,
                    ReceivedDateSnapshot = allocation.ReceivedDate,
                    ExpiryDateSnapshot = allocation.ExpiryDate,
                    Notes = $"AUTO_ALLOCATED_{allocation.AllocationRule}",
                    CreatedAt = now
                })
                .ToList();

            _dbContext.StockTransactionLines.AddRange(lines);
            await _dbContext.SaveChangesAsync(ct);

            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CREATE_ISSUE",
                entityName: "StockTransactions",
                entityId: transaction.TransactionId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    transaction.TransactionNo,
                    transaction.WarehouseId,
                    transaction.CustomerId,
                    LineCount = lines.Count,
                    transaction.TotalAmount
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return CreateIssueResult.Success(transaction.TransactionId, transaction.TransactionNo);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CreateIssueResult.Failure("Stock changed while reserving lots for this draft. Please preview again and retry.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CreateIssueResult.Failure("Issue creation failed due to a database update conflict.");
        }
    }

    public async Task<IReadOnlyList<DraftIssueHeaderDto>> GetDraftIssuesAsync(CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.StockTransactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TransactionType == IssueTransactionType &&
                transaction.DocumentStatus == DocumentStatusDraft)
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .Select(transaction => new DraftIssueHeaderDto
            {
                TransactionId = transaction.TransactionId,
                TransactionNo = transaction.TransactionNo,
                WarehouseId = transaction.WarehouseId,
                WarehouseName = transaction.Warehouse.WarehouseName,
                CustomerName = transaction.Customer != null ? transaction.Customer.CustomerName : null,
                TransactionDate = transaction.TransactionDate,
                TotalAmount = transaction.TotalAmount,
                CreatedBy = transaction.CreatedByUser.Username,
                CreatedAt = transaction.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DraftIssueLineDto>> GetDraftIssueLinesAsync(long transactionId, CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.StockTransactionLines
            .AsNoTracking()
            .Where(line =>
                line.TransactionId == transactionId &&
                line.Transaction.TransactionType == IssueTransactionType &&
                line.Transaction.DocumentStatus == DocumentStatusDraft)
            .OrderBy(line => line.LineNo)
            .Select(line => new DraftIssueLineDto
            {
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                Sku = line.Product.Sku,
                ProductName = line.Product.ProductName,
                ProductLotId = line.ProductLotId,
                LotCode = line.ProductLot.LotCode,
                Qty = line.Qty,
                UnitCost = line.UnitCost,
                UnitPrice = line.UnitPrice,
                CogsAmount = line.CogsAmount,
                LineAmount = line.LineAmount,
                ReceivedDateSnapshot = line.ReceivedDateSnapshot,
                ExpiryDateSnapshot = line.ExpiryDateSnapshot
            })
            .ToListAsync(ct);
    }

    public async Task<PostIssueResult> PostIssueAsync(long transactionId, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, ManagerRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return PostIssueResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            StockTransaction? issue = await _dbContext.StockTransactions
                .Include(transaction => transaction.StockTransactionLines)
                .ThenInclude(line => line.ProductLot)
                .FirstOrDefaultAsync(transaction => transaction.TransactionId == transactionId, ct);

            if (issue is null)
            {
                return PostIssueResult.Failure("Issue document not found.");
            }

            if (!string.Equals(issue.TransactionType, IssueTransactionType, StringComparison.OrdinalIgnoreCase))
            {
                return PostIssueResult.Failure("Selected document is not an issue transaction.");
            }

            if (string.Equals(issue.DocumentStatus, DocumentStatusPosted, StringComparison.OrdinalIgnoreCase))
            {
                return PostIssueResult.Success(issue.TransactionId, issue.TransactionNo, issue.PostedAt ?? DateTime.UtcNow);
            }

            if (!string.Equals(issue.DocumentStatus, DocumentStatusDraft, StringComparison.OrdinalIgnoreCase))
            {
                return PostIssueResult.Failure($"Issue must be in {DocumentStatusDraft} status before posting.");
            }

            if (issue.StockTransactionLines.Count == 0)
            {
                return PostIssueResult.Failure("Draft issue has no lines to post.");
            }

            DateOnly transactionDate = DateOnly.FromDateTime(issue.TransactionDate.Date);
            DateTime now = DateTime.UtcNow;

            IReadOnlyCollection<long> lotIds = issue.StockTransactionLines
                .Select(line => line.ProductLotId)
                .Distinct()
                .ToArray();

            Dictionary<(int ProductId, long ProductLotId), StockBalance> stockBalanceMap = await _dbContext.StockBalances
                .Where(balance => balance.WarehouseId == issue.WarehouseId && lotIds.Contains(balance.ProductLotId))
                .ToDictionaryAsync(balance => (balance.ProductId, balance.ProductLotId), ct);

            List<StockTransactionLine> orderedLines = issue.StockTransactionLines
                .OrderBy(line => line.LineNo)
                .ToList();

            foreach (StockTransactionLine line in orderedLines)
            {
                if (line.ProductLot.WarehouseId != issue.WarehouseId || line.ProductLot.ProductId != line.ProductId)
                {
                    return PostIssueResult.Failure($"Line {line.LineNo} references an invalid lot for warehouse/product.");
                }

                if (line.ProductLot.ExpiryDate.HasValue && line.ProductLot.ExpiryDate.Value < transactionDate)
                {
                    return PostIssueResult.Failure($"Line {line.LineNo} uses expired lot '{line.ProductLot.LotCode}'.");
                }

                if (!stockBalanceMap.TryGetValue((line.ProductId, line.ProductLotId), out StockBalance? stockBalance))
                {
                    return PostIssueResult.Failure($"Stock balance not found for line {line.LineNo}.");
                }

                if (stockBalance.AllocatedQty < line.Qty)
                {
                    return PostIssueResult.Failure(
                        $"Reservation for lot '{line.ProductLot.LotCode}' is no longer sufficient. Please recreate draft issue.");
                }

                if (stockBalance.OnHandQty < line.Qty)
                {
                    return PostIssueResult.Failure(
                        $"Insufficient on-hand quantity for lot '{line.ProductLot.LotCode}'. Please recreate draft issue.");
                }

                if (line.ProductLot.RemainingQty < line.Qty)
                {
                    return PostIssueResult.Failure(
                        $"Lot '{line.ProductLot.LotCode}' no longer has enough remaining quantity. Please preview allocation again.");
                }
            }

            foreach (StockTransactionLine line in orderedLines)
            {
                StockBalance stockBalance = stockBalanceMap[(line.ProductId, line.ProductLotId)];
                stockBalance.OnHandQty -= line.Qty;
                if (stockBalance.OnHandQty < 0)
                {
                    stockBalance.OnHandQty = 0;
                }
                stockBalance.AllocatedQty -= line.Qty;
                if (stockBalance.AllocatedQty < 0)
                {
                    stockBalance.AllocatedQty = 0;
                }
                stockBalance.LastMovementAt = now;
                stockBalance.UpdatedAt = now;

                line.ProductLot.RemainingQty -= line.Qty;
                if (line.ProductLot.RemainingQty < 0)
                {
                    line.ProductLot.RemainingQty = 0;
                }
                line.ProductLot.UpdatedAt = now;
                line.ProductLot.Status = line.ProductLot.RemainingQty <= 0 ? "DEPLETED" : "ACTIVE";

                line.CogsAmount = RoundMoney(line.Qty * line.UnitCost);
            }

            issue.DocumentStatus = DocumentStatusPosted;
            issue.PostedByUserId = actorUserId;
            issue.PostedAt = now;
            issue.UpdatedAt = now;
            issue.TotalAmount = issue.StockTransactionLines
                .Sum(line => RoundMoney(line.Qty * (line.UnitPrice ?? line.UnitCost)));

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "POST_ISSUE",
                entityName: "StockTransactions",
                entityId: issue.TransactionId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    issue.TransactionNo,
                    issue.WarehouseId,
                    LineCount = issue.StockTransactionLines.Count,
                    issue.TotalAmount
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return PostIssueResult.Success(issue.TransactionId, issue.TransactionNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return PostIssueResult.Failure("Posting failed because data was modified by another user. Please refresh and retry.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return PostIssueResult.Failure("Posting failed due to a database update conflict.");
        }
    }

    public async Task<CancelIssueResult> CancelDraftIssueAsync(long transactionId, int actorUserId, CancellationToken ct)
    {
        OperationResult authorization = await EnsureRoleAsync(actorUserId, ManagerRoleCode, ct);
        if (!authorization.IsSuccess)
        {
            return CancelIssueResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        try
        {
            StockTransaction? issue = await _dbContext.StockTransactions
                .Include(transaction => transaction.StockTransactionLines)
                .FirstOrDefaultAsync(transaction => transaction.TransactionId == transactionId, ct);

            if (issue is null)
            {
                return CancelIssueResult.Failure("Issue document not found.");
            }

            if (!string.Equals(issue.TransactionType, IssueTransactionType, StringComparison.OrdinalIgnoreCase))
            {
                return CancelIssueResult.Failure("Selected document is not an issue transaction.");
            }

            if (string.Equals(issue.DocumentStatus, DocumentStatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                return CancelIssueResult.Success(issue.TransactionId, issue.TransactionNo, issue.UpdatedAt ?? DateTime.UtcNow);
            }

            if (!string.Equals(issue.DocumentStatus, DocumentStatusDraft, StringComparison.OrdinalIgnoreCase))
            {
                return CancelIssueResult.Failure($"Issue must be in {DocumentStatusDraft} status before cancellation.");
            }

            DateTime now = DateTime.UtcNow;

            if (issue.StockTransactionLines.Count > 0)
            {
                IReadOnlyCollection<long> lotIds = issue.StockTransactionLines
                    .Select(line => line.ProductLotId)
                    .Distinct()
                    .ToArray();

                Dictionary<(int ProductId, long ProductLotId), StockBalance> stockBalanceMap = await _dbContext.StockBalances
                    .Where(balance => balance.WarehouseId == issue.WarehouseId && lotIds.Contains(balance.ProductLotId))
                    .ToDictionaryAsync(balance => (balance.ProductId, balance.ProductLotId), ct);

                List<StockTransactionLine> orderedLines = issue.StockTransactionLines
                    .OrderBy(line => line.LineNo)
                    .ToList();

                foreach (StockTransactionLine line in orderedLines)
                {
                    if (!stockBalanceMap.TryGetValue((line.ProductId, line.ProductLotId), out StockBalance? stockBalance))
                    {
                        return CancelIssueResult.Failure($"Stock balance not found for line {line.LineNo}.");
                    }

                    if (stockBalance.AllocatedQty < line.Qty)
                    {
                        return CancelIssueResult.Failure(
                            $"Reserved quantity for lot id {line.ProductLotId} is inconsistent. Cannot cancel safely.");
                    }
                }

                foreach (StockTransactionLine line in orderedLines)
                {
                    StockBalance stockBalance = stockBalanceMap[(line.ProductId, line.ProductLotId)];
                    stockBalance.AllocatedQty -= line.Qty;
                    if (stockBalance.AllocatedQty < 0)
                    {
                        stockBalance.AllocatedQty = 0;
                    }
                    stockBalance.UpdatedAt = now;
                }
            }

            issue.DocumentStatus = DocumentStatusCancelled;
            issue.PostedByUserId = null;
            issue.PostedAt = null;
            issue.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CANCEL_ISSUE",
                entityName: "StockTransactions",
                entityId: issue.TransactionId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    issue.TransactionNo,
                    issue.WarehouseId,
                    LineCount = issue.StockTransactionLines.Count
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return CancelIssueResult.Success(issue.TransactionId, issue.TransactionNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CancelIssueResult.Failure("Cancellation failed because data was modified by another user. Please refresh and retry.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CancelIssueResult.Failure("Cancellation failed due to a database update conflict.");
        }
    }

    private async Task<AllocationComputation> ComputeAllocationAsync(IssueRequestDto request, CancellationToken ct)
    {
        if (request is null)
        {
            return AllocationComputation.Failure("Issue request payload is required.");
        }

        if (request.WarehouseId <= 0)
        {
            return AllocationComputation.Failure("Warehouse is required.");
        }

        bool warehouseExists = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(warehouse => warehouse.WarehouseId == request.WarehouseId && warehouse.IsActive, ct);
        if (!warehouseExists)
        {
            return AllocationComputation.Failure("Selected warehouse is invalid or inactive.");
        }

        NormalizedLineResult normalizedLineResult = NormalizeLines(request.Lines);
        if (!normalizedLineResult.IsSuccess)
        {
            return AllocationComputation.Failure(normalizedLineResult.ErrorMessage ?? "Issue lines are invalid.");
        }

        List<NormalizedRequestLine> requestedLines = normalizedLineResult.Lines;
        List<int> productIds = requestedLines.Select(line => line.ProductId).ToList();

        Dictionary<int, ProductLookupDto> productMap = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && productIds.Contains(product.ProductId))
            .Select(product => new ProductLookupDto
            {
                ProductId = product.ProductId,
                Sku = product.Sku,
                ProductName = product.ProductName,
                BaseUom = product.BaseUom
            })
            .ToDictionaryAsync(product => product.ProductId, ct);

        if (productMap.Count != productIds.Count)
        {
            return AllocationComputation.Failure("One or more selected products are invalid or inactive.");
        }

        DateOnly transactionDate = DateOnly.FromDateTime((request.TransactionDate == default ? DateTime.UtcNow : request.TransactionDate).Date);

        List<AvailableLotSnapshot> lotSnapshots = await _dbContext.StockBalances
            .AsNoTracking()
            .Where(balance => balance.WarehouseId == request.WarehouseId && productIds.Contains(balance.ProductId))
            .Select(balance => new AvailableLotSnapshot
            {
                ProductId = balance.ProductId,
                ProductLotId = balance.ProductLotId,
                LotCode = balance.ProductLot.LotCode,
                ReceivedDate = balance.ProductLot.ReceivedDate,
                ExpiryDate = balance.ProductLot.ExpiryDate,
                UnitCost = balance.ProductLot.UnitCost,
                LotStatus = balance.ProductLot.Status,
                AvailableQty = balance.AvailableQty ?? (balance.OnHandQty - balance.AllocatedQty)
            })
            .Where(snapshot => snapshot.AvailableQty > 0 && snapshot.LotStatus != "LOCKED")
            .ToListAsync(ct);

        List<IssueAllocationPreviewItemDto> allocations = [];
        List<IssueAllocationShortageDto> shortages = [];

        foreach (NormalizedRequestLine requestedLine in requestedLines)
        {
            ProductLookupDto product = productMap[requestedLine.ProductId];

            List<AvailableLotSnapshot> eligibleLots = lotSnapshots
                .Where(snapshot =>
                    snapshot.ProductId == requestedLine.ProductId &&
                    (!snapshot.ExpiryDate.HasValue || snapshot.ExpiryDate.Value >= transactionDate))
                .OrderBy(snapshot => snapshot.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(snapshot => snapshot.ExpiryDate ?? DateOnly.MaxValue)
                .ThenBy(snapshot => snapshot.ReceivedDate)
                .ThenBy(snapshot => snapshot.ProductLotId)
                .ToList();

            decimal availableTotal = eligibleLots.Sum(snapshot => snapshot.AvailableQty);
            decimal remainingQty = requestedLine.Qty;

            foreach (AvailableLotSnapshot lot in eligibleLots)
            {
                if (remainingQty <= 0)
                {
                    break;
                }

                decimal allocatedQty = Math.Min(remainingQty, lot.AvailableQty);
                if (allocatedQty <= 0)
                {
                    continue;
                }

                decimal cogsAmount = RoundMoney(allocatedQty * lot.UnitCost);
                decimal lineAmount = RoundMoney(allocatedQty * (requestedLine.UnitPrice ?? lot.UnitCost));

                allocations.Add(new IssueAllocationPreviewItemDto
                {
                    ProductId = product.ProductId,
                    Sku = product.Sku,
                    ProductName = product.ProductName,
                    ProductLotId = lot.ProductLotId,
                    LotCode = lot.LotCode,
                    ReceivedDate = lot.ReceivedDate,
                    ExpiryDate = lot.ExpiryDate,
                    AvailableQtyBeforeAllocation = lot.AvailableQty,
                    AllocatedQty = allocatedQty,
                    UnitCost = lot.UnitCost,
                    UnitPrice = requestedLine.UnitPrice,
                    CogsAmount = cogsAmount,
                    LineAmount = lineAmount,
                    AllocationRule = lot.ExpiryDate.HasValue ? "FEFO" : "FIFO"
                });

                remainingQty -= allocatedQty;
            }

            if (remainingQty > 0)
            {
                shortages.Add(new IssueAllocationShortageDto
                {
                    ProductId = product.ProductId,
                    Sku = product.Sku,
                    ProductName = product.ProductName,
                    RequestedQty = requestedLine.Qty,
                    AvailableQty = availableTotal
                });
            }
        }

        decimal totalCogs = allocations.Sum(allocation => allocation.CogsAmount);
        decimal totalSales = allocations.Sum(allocation => allocation.LineAmount);

        if (shortages.Count > 0)
        {
            return AllocationComputation.Failure(
                "Insufficient available stock for one or more products.",
                allocations,
                shortages,
                totalCogs,
                totalSales);
        }

        return AllocationComputation.Success(allocations, totalCogs, totalSales);
    }

    private async Task<AllocationComputation> ComputeAllocationForReservationAsync(
        IssueRequestDto request,
        DateTime reservedAtUtc,
        CancellationToken ct)
    {
        if (request is null)
        {
            return AllocationComputation.Failure("Issue request payload is required.");
        }

        if (request.WarehouseId <= 0)
        {
            return AllocationComputation.Failure("Warehouse is required.");
        }

        bool warehouseExists = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(warehouse => warehouse.WarehouseId == request.WarehouseId && warehouse.IsActive, ct);
        if (!warehouseExists)
        {
            return AllocationComputation.Failure("Selected warehouse is invalid or inactive.");
        }

        NormalizedLineResult normalizedLineResult = NormalizeLines(request.Lines);
        if (!normalizedLineResult.IsSuccess)
        {
            return AllocationComputation.Failure(normalizedLineResult.ErrorMessage ?? "Issue lines are invalid.");
        }

        List<NormalizedRequestLine> requestedLines = normalizedLineResult.Lines;
        List<int> productIds = requestedLines.Select(line => line.ProductId).ToList();

        Dictionary<int, ProductLookupDto> productMap = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && productIds.Contains(product.ProductId))
            .Select(product => new ProductLookupDto
            {
                ProductId = product.ProductId,
                Sku = product.Sku,
                ProductName = product.ProductName,
                BaseUom = product.BaseUom
            })
            .ToDictionaryAsync(product => product.ProductId, ct);

        if (productMap.Count != productIds.Count)
        {
            return AllocationComputation.Failure("One or more selected products are invalid or inactive.");
        }

        DateOnly transactionDate = DateOnly.FromDateTime((request.TransactionDate == default ? DateTime.UtcNow : request.TransactionDate).Date);

        List<ReservableLotSnapshot> lotSnapshots = await _dbContext.StockBalances
            .Include(balance => balance.ProductLot)
            .Where(balance => balance.WarehouseId == request.WarehouseId && productIds.Contains(balance.ProductId))
            .Select(balance => new ReservableLotSnapshot
            {
                StockBalance = balance,
                ProductId = balance.ProductId,
                ProductLotId = balance.ProductLotId,
                LotCode = balance.ProductLot.LotCode,
                ReceivedDate = balance.ProductLot.ReceivedDate,
                ExpiryDate = balance.ProductLot.ExpiryDate,
                UnitCost = balance.ProductLot.UnitCost,
                LotStatus = balance.ProductLot.Status,
                AvailableQty = balance.OnHandQty - balance.AllocatedQty
            })
            .Where(snapshot => snapshot.AvailableQty > 0 && snapshot.LotStatus != "LOCKED")
            .ToListAsync(ct);

        List<IssueAllocationPreviewItemDto> allocations = [];
        List<IssueAllocationShortageDto> shortages = [];

        foreach (NormalizedRequestLine requestedLine in requestedLines)
        {
            ProductLookupDto product = productMap[requestedLine.ProductId];

            List<ReservableLotSnapshot> eligibleLots = lotSnapshots
                .Where(snapshot =>
                    snapshot.ProductId == requestedLine.ProductId &&
                    (!snapshot.ExpiryDate.HasValue || snapshot.ExpiryDate.Value >= transactionDate))
                .OrderBy(snapshot => snapshot.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(snapshot => snapshot.ExpiryDate ?? DateOnly.MaxValue)
                .ThenBy(snapshot => snapshot.ReceivedDate)
                .ThenBy(snapshot => snapshot.ProductLotId)
                .ToList();

            decimal availableTotal = eligibleLots.Sum(snapshot => snapshot.AvailableQty);
            decimal remainingQty = requestedLine.Qty;

            foreach (ReservableLotSnapshot lot in eligibleLots)
            {
                if (remainingQty <= 0)
                {
                    break;
                }

                decimal availableBeforeAllocation = lot.AvailableQty;
                decimal allocatedQty = Math.Min(remainingQty, availableBeforeAllocation);
                if (allocatedQty <= 0)
                {
                    continue;
                }

                decimal cogsAmount = RoundMoney(allocatedQty * lot.UnitCost);
                decimal lineAmount = RoundMoney(allocatedQty * (requestedLine.UnitPrice ?? lot.UnitCost));

                allocations.Add(new IssueAllocationPreviewItemDto
                {
                    ProductId = product.ProductId,
                    Sku = product.Sku,
                    ProductName = product.ProductName,
                    ProductLotId = lot.ProductLotId,
                    LotCode = lot.LotCode,
                    ReceivedDate = lot.ReceivedDate,
                    ExpiryDate = lot.ExpiryDate,
                    AvailableQtyBeforeAllocation = availableBeforeAllocation,
                    AllocatedQty = allocatedQty,
                    UnitCost = lot.UnitCost,
                    UnitPrice = requestedLine.UnitPrice,
                    CogsAmount = cogsAmount,
                    LineAmount = lineAmount,
                    AllocationRule = lot.ExpiryDate.HasValue ? "FEFO" : "FIFO"
                });

                lot.AvailableQty -= allocatedQty;
                remainingQty -= allocatedQty;
            }

            if (remainingQty > 0)
            {
                shortages.Add(new IssueAllocationShortageDto
                {
                    ProductId = product.ProductId,
                    Sku = product.Sku,
                    ProductName = product.ProductName,
                    RequestedQty = requestedLine.Qty,
                    AvailableQty = availableTotal
                });
            }
        }

        decimal totalCogs = allocations.Sum(allocation => allocation.CogsAmount);
        decimal totalSales = allocations.Sum(allocation => allocation.LineAmount);

        if (shortages.Count > 0)
        {
            return AllocationComputation.Failure(
                "Insufficient available stock for one or more products.",
                allocations,
                shortages,
                totalCogs,
                totalSales);
        }

        Dictionary<(int ProductId, long ProductLotId), StockBalance> reservableStockBalanceMap = lotSnapshots
            .ToDictionary(snapshot => (snapshot.ProductId, snapshot.ProductLotId), snapshot => snapshot.StockBalance);

        foreach (IssueAllocationPreviewItemDto allocation in allocations)
        {
            if (!reservableStockBalanceMap.TryGetValue((allocation.ProductId, allocation.ProductLotId), out StockBalance? stockBalance))
            {
                return AllocationComputation.Failure(
                    $"Stock balance not found while reserving lot '{allocation.LotCode}'.");
            }

            stockBalance.AllocatedQty = decimal.Round(stockBalance.AllocatedQty + allocation.AllocatedQty, 3);
            stockBalance.UpdatedAt = reservedAtUtc;
        }

        return AllocationComputation.Success(allocations, totalCogs, totalSales);
    }

    private async Task<string> GenerateIssueTransactionNoAsync(DateTime transactionDate, CancellationToken ct)
    {
        string dateToken = transactionDate.ToString("yyyyMMdd");

        for (int attempt = 0; attempt < 5; attempt++)
        {
            string candidate = $"ISSUE-{dateToken}-{DateTime.UtcNow:HHmmssfff}";
            bool exists = await _dbContext.StockTransactions
                .AsNoTracking()
                .AnyAsync(transaction => transaction.TransactionNo == candidate, ct);

            if (!exists)
            {
                return candidate;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), ct);
        }

        return $"ISSUE-{dateToken}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
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

    private static NormalizedLineResult NormalizeLines(IReadOnlyCollection<IssueRequestLineDto> inputLines)
    {
        if (inputLines is null || inputLines.Count == 0)
        {
            return NormalizedLineResult.Failure("At least one issue line is required.");
        }

        List<IssueRequestLineDto> validLines = inputLines
            .Where(line => line.ProductId > 0 && line.Qty > 0)
            .ToList();

        if (validLines.Count == 0)
        {
            return NormalizedLineResult.Failure("Issue lines must include valid product and quantity.");
        }

        List<NormalizedRequestLine> normalizedLines = [];

        foreach (IGrouping<int, IssueRequestLineDto> groupedLine in validLines.GroupBy(line => line.ProductId))
        {
            List<decimal?> priceSet = groupedLine
                .Select(line => line.UnitPrice.HasValue ? decimal.Round(line.UnitPrice.Value, 4) : (decimal?)null)
                .Distinct()
                .ToList();

            if (priceSet.Count > 1)
            {
                return NormalizedLineResult.Failure(
                    $"ProductId {groupedLine.Key} has multiple unit prices in one issue request.");
            }

            decimal totalQty = decimal.Round(groupedLine.Sum(line => line.Qty), 3);
            if (totalQty <= 0)
            {
                return NormalizedLineResult.Failure($"ProductId {groupedLine.Key} must have quantity greater than 0.");
            }

            decimal? unitPrice = priceSet.Single();
            if (unitPrice.HasValue && unitPrice.Value < 0)
            {
                return NormalizedLineResult.Failure($"ProductId {groupedLine.Key} has invalid unit price.");
            }

            normalizedLines.Add(new NormalizedRequestLine
            {
                ProductId = groupedLine.Key,
                Qty = totalQty,
                UnitPrice = unitPrice
            });
        }

        return NormalizedLineResult.Success(normalizedLines.OrderBy(line => line.ProductId).ToList());
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class AvailableLotSnapshot
    {
        public int ProductId { get; init; }

        public long ProductLotId { get; init; }

        public string LotCode { get; init; } = string.Empty;

        public DateOnly ReceivedDate { get; init; }

        public DateOnly? ExpiryDate { get; init; }

        public decimal UnitCost { get; init; }

        public decimal AvailableQty { get; init; }

        public string LotStatus { get; init; } = string.Empty;
    }

    private sealed class ReservableLotSnapshot
    {
        public required StockBalance StockBalance { get; init; }

        public int ProductId { get; init; }

        public long ProductLotId { get; init; }

        public string LotCode { get; init; } = string.Empty;

        public DateOnly ReceivedDate { get; init; }

        public DateOnly? ExpiryDate { get; init; }

        public decimal UnitCost { get; init; }

        public decimal AvailableQty { get; set; }

        public string LotStatus { get; init; } = string.Empty;
    }

    private sealed class NormalizedRequestLine
    {
        public int ProductId { get; init; }

        public decimal Qty { get; init; }

        public decimal? UnitPrice { get; init; }
    }

    private sealed class NormalizedLineResult
    {
        public bool IsSuccess { get; private init; }

        public string? ErrorMessage { get; private init; }

        public List<NormalizedRequestLine> Lines { get; private init; } = [];

        public static NormalizedLineResult Success(List<NormalizedRequestLine> lines)
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
                ErrorMessage = errorMessage,
                Lines = []
            };
        }
    }

    private sealed class AllocationComputation
    {
        public bool IsSuccess { get; private init; }

        public string? ErrorMessage { get; private init; }

        public List<IssueAllocationPreviewItemDto> Allocations { get; private init; } = [];

        public List<IssueAllocationShortageDto> Shortages { get; private init; } = [];

        public decimal TotalCogs { get; private init; }

        public decimal TotalSales { get; private init; }

        public static AllocationComputation Success(List<IssueAllocationPreviewItemDto> allocations, decimal totalCogs, decimal totalSales)
        {
            return new AllocationComputation
            {
                IsSuccess = true,
                Allocations = allocations,
                TotalCogs = totalCogs,
                TotalSales = totalSales
            };
        }

        public static AllocationComputation Failure(
            string errorMessage,
            List<IssueAllocationPreviewItemDto>? allocations = null,
            List<IssueAllocationShortageDto>? shortages = null,
            decimal totalCogs = 0,
            decimal totalSales = 0)
        {
            return new AllocationComputation
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Allocations = allocations ?? [],
                Shortages = shortages ?? [],
                TotalCogs = totalCogs,
                TotalSales = totalSales
            };
        }
    }
}
