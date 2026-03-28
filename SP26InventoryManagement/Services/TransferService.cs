using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services;

public class TransferService : ITransferService
{
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string AdminRoleCode = "ADMIN";

    private const string TransferStatusCreated = "CREATED";
    private const string TransferStatusSourceDispatched = "SOURCE_DISPATCHED";
    private const string TransferStatusDestinationReceived = "DESTINATION_RECEIVED";
    private const string TransferStatusCancelled = "CANCELLED";

    private const string ProductLotStatusLocked = "LOCKED";
    private const string ProductLotStatusActive = "ACTIVE";
    private const string ProductLotStatusDepleted = "DEPLETED";

    private readonly Sp26inventoryManagementDbContext _dbContext;
    private readonly ISessionValidationService _sessionValidationService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IAuditLogService _auditLogService;

    public TransferService(
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

    public async Task<IReadOnlyList<WarehouseLookupDto>> GetAllowedSourceWarehousesAsync(int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            return Array.Empty<WarehouseLookupDto>();
        }

        IQueryable<Warehouse> query = _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.IsActive);

        if (!authorization.IsAdmin && authorization.AssignedWarehouseId.HasValue)
        {
            int warehouseId = authorization.AssignedWarehouseId.Value;
            query = query.Where(warehouse => warehouse.WarehouseId == warehouseId);
        }

        return await query
            .OrderBy(warehouse => warehouse.WarehouseCode)
            .Select(warehouse => new WarehouseLookupDto
            {
                WarehouseId = warehouse.WarehouseId,
                WarehouseCode = warehouse.WarehouseCode,
                WarehouseName = warehouse.WarehouseName
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WarehouseLookupDto>> GetActiveDestinationWarehousesAsync(int sourceWarehouseId, CancellationToken ct)
    {
        await EnsureCurrentSessionOrThrowAsync(ct);

        return await _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.IsActive && warehouse.WarehouseId != sourceWarehouseId)
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

    public async Task<decimal> GetAvailableQtyAsync(
        int sourceWarehouseId,
        int productId,
        DateTime requestDate,
        int actorUserId,
        CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        if (!authorization.IsAdmin && authorization.AssignedWarehouseId != sourceWarehouseId)
        {
            throw new UnauthorizedAccessException("Access denied. Source warehouse must match your warehouse assignment.");
        }

        if (sourceWarehouseId <= 0 || productId <= 0)
        {
            return 0;
        }

        DateOnly requestDay = DateOnly.FromDateTime((requestDate == default ? DateTime.UtcNow : requestDate).Date);

        decimal? availableQty = await _dbContext.StockBalances
            .AsNoTracking()
            .Where(balance =>
                balance.WarehouseId == sourceWarehouseId &&
                balance.ProductId == productId &&
                balance.ProductLot.Status != ProductLotStatusLocked &&
                (!balance.ProductLot.ExpiryDate.HasValue || balance.ProductLot.ExpiryDate.Value >= requestDay))
            .Select(balance => (decimal?)(balance.AvailableQty ?? (balance.OnHandQty - balance.AllocatedQty)))
            .SumAsync(ct);

        decimal safeAvailableQty = availableQty ?? 0m;
        if (safeAvailableQty <= 0)
        {
            return 0;
        }

        return decimal.Round(safeAvailableQty, 3);
    }

    public async Task<PreviewCreateTransferLotSuggestionResult> PreviewCreateTransferLotSuggestionAsync(
        TransferSuggestionRequestDto request,
        int actorUserId,
        CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            return PreviewCreateTransferLotSuggestionResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        ValidationResult validateWarehouses = await ValidateWarehousesAndAccessAsync(
            request.SourceWarehouseId,
            request.DestinationWarehouseId,
            authorization,
            ct);
        if (!validateWarehouses.IsSuccess)
        {
            return PreviewCreateTransferLotSuggestionResult.Failure(validateWarehouses.ErrorMessage ?? "Invalid warehouse selection.");
        }

        NormalizeSuggestionResult normalizeResult = NormalizeSuggestionLines(request.Lines);
        if (!normalizeResult.IsSuccess)
        {
            return PreviewCreateTransferLotSuggestionResult.Failure(normalizeResult.ErrorMessage ?? "Transfer lines are invalid.");
        }

        List<NormalizedSuggestionLine> lines = normalizeResult.Lines;
        List<int> productIds = lines.Select(line => line.ProductId).ToList();

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
            return PreviewCreateTransferLotSuggestionResult.Failure("One or more selected products are invalid or inactive.");
        }

        DateOnly requestDate = DateOnly.FromDateTime((request.RequestDate == default ? DateTime.UtcNow : request.RequestDate).Date);

        List<AvailableLotSnapshot> lotSnapshots = await _dbContext.StockBalances
            .AsNoTracking()
            .Where(balance => balance.WarehouseId == request.SourceWarehouseId && productIds.Contains(balance.ProductId))
            .Select(balance => new AvailableLotSnapshot
            {
                ProductId = balance.ProductId,
                SourceProductLotId = balance.ProductLotId,
                LotCode = balance.ProductLot.LotCode,
                ReceivedDate = balance.ProductLot.ReceivedDate,
                ExpiryDate = balance.ProductLot.ExpiryDate,
                UnitCost = balance.ProductLot.UnitCost,
                LotStatus = balance.ProductLot.Status,
                AvailableQty = balance.AvailableQty ?? (balance.OnHandQty - balance.AllocatedQty)
            })
            .Where(snapshot =>
                snapshot.AvailableQty > 0 &&
                snapshot.LotStatus != ProductLotStatusLocked &&
                (!snapshot.ExpiryDate.HasValue || snapshot.ExpiryDate.Value >= requestDate))
            .ToListAsync(ct);

        List<TransferLotSuggestionItemDto> suggestionItems = [];
        List<TransferSuggestionShortageDto> shortages = [];

        foreach (NormalizedSuggestionLine line in lines)
        {
            ProductLookupDto product = productMap[line.ProductId];
            List<AvailableLotSnapshot> eligibleLots = lotSnapshots
                .Where(snapshot => snapshot.ProductId == line.ProductId)
                .OrderBy(snapshot => snapshot.ExpiryDate.HasValue ? 0 : 1)
                .ThenBy(snapshot => snapshot.ExpiryDate ?? DateOnly.MaxValue)
                .ThenBy(snapshot => snapshot.ReceivedDate)
                .ThenBy(snapshot => snapshot.SourceProductLotId)
                .ToList();

            decimal availableTotal = eligibleLots.Sum(snapshot => snapshot.AvailableQty);
            decimal remainingQty = line.RequestedQty;

            foreach (AvailableLotSnapshot lot in eligibleLots)
            {
                if (remainingQty <= 0)
                {
                    break;
                }

                decimal suggestedQty = Math.Min(remainingQty, lot.AvailableQty);
                if (suggestedQty <= 0)
                {
                    continue;
                }

                suggestionItems.Add(new TransferLotSuggestionItemDto
                {
                    ProductId = line.ProductId,
                    Sku = product.Sku,
                    ProductName = product.ProductName,
                    SourceProductLotId = lot.SourceProductLotId,
                    LotCode = lot.LotCode,
                    ReceivedDate = lot.ReceivedDate,
                    ExpiryDate = lot.ExpiryDate,
                    AvailableQtyBeforeAllocation = lot.AvailableQty,
                    SuggestedQty = decimal.Round(suggestedQty, 3),
                    UnitCost = lot.UnitCost,
                    AllocationRule = lot.ExpiryDate.HasValue ? "FEFO" : "FIFO"
                });

                remainingQty -= suggestedQty;
            }

            if (remainingQty > 0)
            {
                shortages.Add(new TransferSuggestionShortageDto
                {
                    ProductId = line.ProductId,
                    Sku = product.Sku,
                    ProductName = product.ProductName,
                    RequestedQty = line.RequestedQty,
                    AvailableQty = availableTotal
                });
            }
        }

        if (shortages.Count > 0)
        {
            return PreviewCreateTransferLotSuggestionResult.Failure(
                "Insufficient available stock for one or more products.",
                suggestionItems,
                shortages);
        }

        return PreviewCreateTransferLotSuggestionResult.Success(suggestionItems);
    }

    public async Task<CreateTransferResult> CreateTransferAsync(TransferCreateRequestDto request, int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            return CreateTransferResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();

        ValidationResult validateWarehouses = await ValidateWarehousesAndAccessAsync(
            request.SourceWarehouseId,
            request.DestinationWarehouseId,
            authorization,
            ct);
        if (!validateWarehouses.IsSuccess)
        {
            return CreateTransferResult.Failure(validateWarehouses.ErrorMessage ?? "Invalid warehouse selection.");
        }

        NormalizeCreateResult normalizeResult = NormalizeCreateLines(request.Lines);
        if (!normalizeResult.IsSuccess)
        {
            return CreateTransferResult.Failure(normalizeResult.ErrorMessage ?? "Transfer lines are invalid.");
        }

        List<NormalizedCreateLine> lines = normalizeResult.Lines;
        List<int> productIds = lines.Select(line => line.ProductId).ToList();

        bool productsValid = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && productIds.Contains(product.ProductId))
            .Select(product => product.ProductId)
            .Distinct()
            .CountAsync(ct) == productIds.Count;
        if (!productsValid)
        {
            return CreateTransferResult.Failure("One or more selected products are invalid or inactive.");
        }

        DateTime now = DateTime.UtcNow;
        DateTime requestDate = request.RequestDate == default ? now : request.RequestDate;
        DateOnly requestDay = DateOnly.FromDateTime(requestDate.Date);
        if (request.RequiredDate.HasValue && request.RequiredDate.Value <= requestDay)
        {
            return CreateTransferResult.Failure("Required date must be later than request date.");
        }

        string transferNo = await GenerateTransferNoAsync(requestDate, ct);

        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            List<long> sourceLotIds = lines
                .SelectMany(line => line.LotSelections)
                .Select(selection => selection.SourceProductLotId)
                .Distinct()
                .ToList();

            Dictionary<long, StockBalance> stockBalanceMap = await _dbContext.StockBalances
                .Include(balance => balance.ProductLot)
                .Where(balance =>
                    balance.WarehouseId == request.SourceWarehouseId &&
                    sourceLotIds.Contains(balance.ProductLotId))
                .ToDictionaryAsync(balance => balance.ProductLotId, ct);

            if (stockBalanceMap.Count != sourceLotIds.Count)
            {
                return CreateTransferResult.Failure("One or more selected lots are invalid for source warehouse.");
            }

            foreach (NormalizedCreateLine line in lines)
            {
                foreach (NormalizedLotSelection selection in line.LotSelections)
                {
                    if (!stockBalanceMap.TryGetValue(selection.SourceProductLotId, out StockBalance? stockBalance))
                    {
                        return CreateTransferResult.Failure($"Source lot id {selection.SourceProductLotId} not found.");
                    }

                    if (stockBalance.ProductId != line.ProductId)
                    {
                        return CreateTransferResult.Failure(
                            $"Lot '{stockBalance.ProductLot.LotCode}' does not match selected product.");
                    }

                    ProductLot lot = stockBalance.ProductLot;
                    if (string.Equals(lot.Status, ProductLotStatusLocked, StringComparison.OrdinalIgnoreCase))
                    {
                        return CreateTransferResult.Failure($"Lot '{lot.LotCode}' is locked and cannot be transferred.");
                    }

                    if (lot.ExpiryDate.HasValue && lot.ExpiryDate.Value < requestDay)
                    {
                        return CreateTransferResult.Failure($"Lot '{lot.LotCode}' is expired for the transfer date.");
                    }

                    decimal availableQty = stockBalance.OnHandQty - stockBalance.AllocatedQty;
                    if (availableQty < selection.Qty)
                    {
                        return CreateTransferResult.Failure(
                            $"Insufficient available quantity for lot '{lot.LotCode}'. Requested {selection.Qty:N3}, available {availableQty:N3}.");
                    }

                    stockBalance.AllocatedQty = decimal.Round(stockBalance.AllocatedQty + selection.Qty, 3);
                    stockBalance.UpdatedAt = now;
                }
            }

            TransferOrder transferOrder = new()
            {
                TransferNo = transferNo,
                SourceWarehouseId = request.SourceWarehouseId,
                DestinationWarehouseId = request.DestinationWarehouseId,
                TransferStatus = TransferStatusCreated,
                RequestDate = requestDate,
                RequiredDate = request.RequiredDate,
                Remarks = NormalizeNullableText(request.Remarks),
                CreatedByUserId = actorUserId,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = Array.Empty<byte>()
            };

            List<TransferOrderLine> lineEntities = lines
                .OrderBy(line => line.ProductId)
                .Select((line, index) => new TransferOrderLine
                {
                    LineNo = index + 1,
                    ProductId = line.ProductId,
                    RequestedQty = decimal.Round(line.RequestedQty, 3),
                    DispatchedQty = 0,
                    ReceivedQty = 0,
                    Notes = null,
                    CreatedAt = now,
                    RowVersion = Array.Empty<byte>()
                })
                .ToList();

            foreach (TransferOrderLine lineEntity in lineEntities)
            {
                transferOrder.TransferOrderLines.Add(lineEntity);
            }

            _dbContext.TransferOrders.Add(transferOrder);
            await _dbContext.SaveChangesAsync(ct);

            Dictionary<int, TransferOrderLine> lineEntityMap = lineEntities
                .ToDictionary(line => line.ProductId);

            List<TransferLotAllocation> allocationEntities = [];
            foreach (NormalizedCreateLine line in lines)
            {
                TransferOrderLine lineEntity = lineEntityMap[line.ProductId];
                foreach (NormalizedLotSelection selection in line.LotSelections)
                {
                    StockBalance stockBalance = stockBalanceMap[selection.SourceProductLotId];
                    ProductLot lot = stockBalance.ProductLot;

                    allocationEntities.Add(new TransferLotAllocation
                    {
                        TransferOrderLineId = lineEntity.TransferOrderLineId,
                        SourceProductLotId = selection.SourceProductLotId,
                        DestinationProductLotId = null,
                        LotCodeSnapshot = lot.LotCode,
                        ReceivedDateSnapshot = lot.ReceivedDate,
                        ExpiryDateSnapshot = lot.ExpiryDate,
                        UnitCost = lot.UnitCost,
                        DispatchedQty = decimal.Round(selection.Qty, 3),
                        ReceivedQty = 0,
                        CreatedAt = now,
                        RowVersion = Array.Empty<byte>()
                    });
                }
            }

            _dbContext.TransferLotAllocations.AddRange(allocationEntities);
            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CREATE_TRANSFER",
                entityName: "TransferOrders",
                entityId: transferOrder.TransferOrderId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    transferOrder.TransferNo,
                    transferOrder.SourceWarehouseId,
                    transferOrder.DestinationWarehouseId,
                    LineCount = lineEntities.Count
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return CreateTransferResult.Success(transferOrder.TransferOrderId, transferOrder.TransferNo);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CreateTransferResult.Failure("Stock changed while reserving transfer lots. Please preview again and retry.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CreateTransferResult.Failure("Transfer creation failed due to a database update conflict.");
        }
    }

    public async Task<IReadOnlyList<TransferQueueItemDto>> GetSourceDispatchQueueAsync(int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        IQueryable<TransferOrder> query = _dbContext.TransferOrders
            .AsNoTracking()
            .Where(order => order.TransferStatus == TransferStatusCreated);

        if (!authorization.IsAdmin)
        {
            int warehouseId = authorization.AssignedWarehouseId!.Value;
            query = query.Where(order => order.SourceWarehouseId == warehouseId);
        }

        return await query
            .OrderByDescending(order => order.RequestDate)
            .ThenByDescending(order => order.CreatedAt)
            .Select(order => new TransferQueueItemDto
            {
                TransferOrderId = order.TransferOrderId,
                TransferNo = order.TransferNo,
                SourceWarehouseId = order.SourceWarehouseId,
                SourceWarehouseName = order.SourceWarehouse.WarehouseName,
                DestinationWarehouseId = order.DestinationWarehouseId,
                DestinationWarehouseName = order.DestinationWarehouse.WarehouseName,
                TransferStatus = order.TransferStatus,
                RequestDate = order.RequestDate,
                RequiredDate = order.RequiredDate,
                CreatedBy = order.CreatedByUser.Username,
                CreatedAt = order.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TransferQueueItemDto>> GetDestinationReceiptQueueAsync(int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        IQueryable<TransferOrder> query = _dbContext.TransferOrders
            .AsNoTracking()
            .Where(order => order.TransferStatus == TransferStatusSourceDispatched);

        if (!authorization.IsAdmin)
        {
            int warehouseId = authorization.AssignedWarehouseId!.Value;
            query = query.Where(order => order.DestinationWarehouseId == warehouseId);
        }

        return await query
            .OrderByDescending(order => order.RequestDate)
            .ThenByDescending(order => order.CreatedAt)
            .Select(order => new TransferQueueItemDto
            {
                TransferOrderId = order.TransferOrderId,
                TransferNo = order.TransferNo,
                SourceWarehouseId = order.SourceWarehouseId,
                SourceWarehouseName = order.SourceWarehouse.WarehouseName,
                DestinationWarehouseId = order.DestinationWarehouseId,
                DestinationWarehouseName = order.DestinationWarehouse.WarehouseName,
                TransferStatus = order.TransferStatus,
                RequestDate = order.RequestDate,
                RequiredDate = order.RequiredDate,
                CreatedBy = order.CreatedByUser.Username,
                CreatedAt = order.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<TransferDetailDto?> GetTransferDetailAsync(long transferOrderId, int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            throw new UnauthorizedAccessException(authorization.ErrorMessage ?? "Access denied.");
        }

        TransferOrder? transferOrder = await _dbContext.TransferOrders
            .AsNoTracking()
            .Include(order => order.SourceWarehouse)
            .Include(order => order.DestinationWarehouse)
            .Include(order => order.TransferOrderLines)
            .ThenInclude(line => line.Product)
            .Include(order => order.TransferOrderLines)
            .ThenInclude(line => line.TransferLotAllocations)
            .FirstOrDefaultAsync(order => order.TransferOrderId == transferOrderId, ct);

        if (transferOrder is null)
        {
            return null;
        }

        if (!authorization.IsAdmin && authorization.AssignedWarehouseId.HasValue)
        {
            int assignedWarehouseId = authorization.AssignedWarehouseId.Value;
            bool hasWarehouseAccess = transferOrder.SourceWarehouseId == assignedWarehouseId ||
                                      transferOrder.DestinationWarehouseId == assignedWarehouseId;
            if (!hasWarehouseAccess)
            {
                return null;
            }
        }

        List<TransferDetailLineDto> lines = transferOrder.TransferOrderLines
            .OrderBy(line => line.LineNo)
            .Select(line => new TransferDetailLineDto
            {
                TransferOrderLineId = line.TransferOrderLineId,
                LineNo = line.LineNo,
                ProductId = line.ProductId,
                Sku = line.Product.Sku,
                ProductName = line.Product.ProductName,
                RequestedQty = line.RequestedQty,
                DispatchedQty = line.DispatchedQty,
                ReceivedQty = line.ReceivedQty,
                Lots = line.TransferLotAllocations
                    .OrderBy(allocation => allocation.SourceProductLotId)
                    .Select(allocation => new TransferDetailLotDto
                    {
                        TransferLotAllocationId = allocation.TransferLotAllocationId,
                        LineNo = line.LineNo,
                        ProductId = line.ProductId,
                        Sku = line.Product.Sku,
                        ProductName = line.Product.ProductName,
                        SourceProductLotId = allocation.SourceProductLotId,
                        DestinationProductLotId = allocation.DestinationProductLotId,
                        LotCode = allocation.LotCodeSnapshot,
                        ReceivedDateSnapshot = allocation.ReceivedDateSnapshot,
                        ExpiryDateSnapshot = allocation.ExpiryDateSnapshot,
                        UnitCost = allocation.UnitCost,
                        DispatchedQty = allocation.DispatchedQty,
                        ReceivedQty = allocation.ReceivedQty
                    })
                    .ToList()
            })
            .ToList();

        return new TransferDetailDto
        {
            TransferOrderId = transferOrder.TransferOrderId,
            TransferNo = transferOrder.TransferNo,
            SourceWarehouseId = transferOrder.SourceWarehouseId,
            SourceWarehouseName = transferOrder.SourceWarehouse.WarehouseName,
            DestinationWarehouseId = transferOrder.DestinationWarehouseId,
            DestinationWarehouseName = transferOrder.DestinationWarehouse.WarehouseName,
            TransferStatus = transferOrder.TransferStatus,
            RequestDate = transferOrder.RequestDate,
            RequiredDate = transferOrder.RequiredDate,
            Remarks = transferOrder.Remarks,
            Lines = lines
        };
    }

    public async Task<ConfirmSourceDispatchResult> ConfirmSourceDispatchAsync(long transferOrderId, int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            return ConfirmSourceDispatchResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            TransferOrder? transferOrder = await _dbContext.TransferOrders
                .Include(order => order.TransferOrderLines)
                .ThenInclude(line => line.TransferLotAllocations)
                .ThenInclude(allocation => allocation.SourceProductLot)
                .FirstOrDefaultAsync(order => order.TransferOrderId == transferOrderId, ct);

            if (transferOrder is null)
            {
                return ConfirmSourceDispatchResult.Failure("Transfer order not found.");
            }

            if (!authorization.IsAdmin && authorization.AssignedWarehouseId != transferOrder.SourceWarehouseId)
            {
                return ConfirmSourceDispatchResult.Failure("Access denied. You can only dispatch transfers for your assigned source warehouse.");
            }

            if (string.Equals(transferOrder.TransferStatus, TransferStatusSourceDispatched, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(transferOrder.TransferStatus, TransferStatusDestinationReceived, StringComparison.OrdinalIgnoreCase))
            {
                return ConfirmSourceDispatchResult.Success(
                    transferOrder.TransferOrderId,
                    transferOrder.TransferNo,
                    transferOrder.SourceConfirmedAt ?? DateTime.UtcNow);
            }

            if (string.Equals(transferOrder.TransferStatus, TransferStatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                return ConfirmSourceDispatchResult.Failure("Transfer order is cancelled and cannot be dispatched.");
            }

            if (!string.Equals(transferOrder.TransferStatus, TransferStatusCreated, StringComparison.OrdinalIgnoreCase))
            {
                return ConfirmSourceDispatchResult.Failure("Transfer order must be in CREATED status before source dispatch.");
            }

            List<TransferOrderLine> lines = transferOrder.TransferOrderLines
                .OrderBy(line => line.LineNo)
                .ToList();
            if (lines.Count == 0)
            {
                return ConfirmSourceDispatchResult.Failure("Transfer order has no lines.");
            }

            List<TransferLotAllocation> allocations = lines
                .SelectMany(line => line.TransferLotAllocations)
                .OrderBy(allocation => allocation.TransferLotAllocationId)
                .ToList();
            if (allocations.Count == 0)
            {
                return ConfirmSourceDispatchResult.Failure("Transfer order has no lot allocations.");
            }

            foreach (TransferOrderLine line in lines)
            {
                decimal lineAllocatedQty = line.TransferLotAllocations.Sum(allocation => allocation.DispatchedQty);
                if (!IsQtyEqual(lineAllocatedQty, line.RequestedQty))
                {
                    return ConfirmSourceDispatchResult.Failure(
                        $"Allocated quantity does not match requested quantity on line {line.LineNo}.");
                }
            }

            IReadOnlyCollection<long> sourceLotIds = allocations
                .Select(allocation => allocation.SourceProductLotId)
                .Distinct()
                .ToArray();

            Dictionary<long, StockBalance> stockBalanceMap = await _dbContext.StockBalances
                .Where(balance =>
                    balance.WarehouseId == transferOrder.SourceWarehouseId &&
                    sourceLotIds.Contains(balance.ProductLotId))
                .ToDictionaryAsync(balance => balance.ProductLotId, ct);

            DateOnly dispatchDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

            foreach (TransferOrderLine line in lines)
            {
                foreach (TransferLotAllocation allocation in line.TransferLotAllocations)
                {
                    if (allocation.SourceProductLot.WarehouseId != transferOrder.SourceWarehouseId ||
                        allocation.SourceProductLot.ProductId != line.ProductId)
                    {
                        return ConfirmSourceDispatchResult.Failure(
                            $"Lot '{allocation.LotCodeSnapshot}' is invalid for line {line.LineNo}.");
                    }

                    if (allocation.SourceProductLot.ExpiryDate.HasValue &&
                        allocation.SourceProductLot.ExpiryDate.Value < dispatchDate)
                    {
                        return ConfirmSourceDispatchResult.Failure(
                            $"Lot '{allocation.SourceProductLot.LotCode}' is expired and cannot be dispatched.");
                    }

                    if (!stockBalanceMap.TryGetValue(allocation.SourceProductLotId, out StockBalance? stockBalance))
                    {
                        return ConfirmSourceDispatchResult.Failure(
                            $"Stock balance not found for lot '{allocation.SourceProductLot.LotCode}'.");
                    }

                    if (stockBalance.AllocatedQty < allocation.DispatchedQty)
                    {
                        return ConfirmSourceDispatchResult.Failure(
                            $"Reserved quantity for lot '{allocation.SourceProductLot.LotCode}' is no longer sufficient.");
                    }

                    if (stockBalance.OnHandQty < allocation.DispatchedQty)
                    {
                        return ConfirmSourceDispatchResult.Failure(
                            $"Insufficient on-hand quantity for lot '{allocation.SourceProductLot.LotCode}'.");
                    }

                    if (allocation.SourceProductLot.RemainingQty < allocation.DispatchedQty)
                    {
                        return ConfirmSourceDispatchResult.Failure(
                            $"Lot '{allocation.SourceProductLot.LotCode}' no longer has enough remaining quantity.");
                    }
                }
            }

            DateTime now = DateTime.UtcNow;

            foreach (TransferLotAllocation allocation in allocations)
            {
                StockBalance stockBalance = stockBalanceMap[allocation.SourceProductLotId];
                stockBalance.OnHandQty = decimal.Round(stockBalance.OnHandQty - allocation.DispatchedQty, 3);
                if (stockBalance.OnHandQty < 0)
                {
                    stockBalance.OnHandQty = 0;
                }

                stockBalance.AllocatedQty = decimal.Round(stockBalance.AllocatedQty - allocation.DispatchedQty, 3);
                if (stockBalance.AllocatedQty < 0)
                {
                    stockBalance.AllocatedQty = 0;
                }

                stockBalance.LastMovementAt = now;
                stockBalance.UpdatedAt = now;

                ProductLot sourceLot = allocation.SourceProductLot;
                sourceLot.RemainingQty = decimal.Round(sourceLot.RemainingQty - allocation.DispatchedQty, 3);
                if (sourceLot.RemainingQty < 0)
                {
                    sourceLot.RemainingQty = 0;
                }
                sourceLot.Status = sourceLot.RemainingQty <= 0 ? ProductLotStatusDepleted : ProductLotStatusActive;
                sourceLot.UpdatedAt = now;
            }

            foreach (TransferOrderLine line in lines)
            {
                line.DispatchedQty = decimal.Round(line.RequestedQty, 3);
            }

            StockTransaction transferOutTransaction = await CreateTransferOutTransactionAsync(
                transferOrder,
                lines,
                actorUserId,
                now,
                ct);

            transferOrder.TransferStatus = TransferStatusSourceDispatched;
            transferOrder.SourceConfirmedByUserId = actorUserId;
            transferOrder.SourceConfirmedAt = now;
            transferOrder.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CONFIRM_SOURCE_DISPATCH",
                entityName: "TransferOrders",
                entityId: transferOrder.TransferOrderId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    transferOrder.TransferNo,
                    transferOrder.SourceWarehouseId,
                    transferOutTransaction.TransactionNo
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return ConfirmSourceDispatchResult.Success(transferOrder.TransferOrderId, transferOrder.TransferNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return ConfirmSourceDispatchResult.Failure("Source dispatch failed because data was modified by another user.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return ConfirmSourceDispatchResult.Failure("Source dispatch failed due to a database update conflict.");
        }
    }

    public async Task<ConfirmDestinationReceiptResult> ConfirmDestinationReceiptAsync(long transferOrderId, int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            return ConfirmDestinationReceiptResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            TransferOrder? transferOrder = await _dbContext.TransferOrders
                .Include(order => order.TransferOrderLines)
                .ThenInclude(line => line.TransferLotAllocations)
                .ThenInclude(allocation => allocation.SourceProductLot)
                .FirstOrDefaultAsync(order => order.TransferOrderId == transferOrderId, ct);

            if (transferOrder is null)
            {
                return ConfirmDestinationReceiptResult.Failure("Transfer order not found.");
            }

            if (!authorization.IsAdmin && authorization.AssignedWarehouseId != transferOrder.DestinationWarehouseId)
            {
                return ConfirmDestinationReceiptResult.Failure("Access denied. You can only receive transfers for your assigned destination warehouse.");
            }

            if (string.Equals(transferOrder.TransferStatus, TransferStatusDestinationReceived, StringComparison.OrdinalIgnoreCase))
            {
                return ConfirmDestinationReceiptResult.Success(
                    transferOrder.TransferOrderId,
                    transferOrder.TransferNo,
                    transferOrder.DestinationConfirmedAt ?? DateTime.UtcNow);
            }

            if (!string.Equals(transferOrder.TransferStatus, TransferStatusSourceDispatched, StringComparison.OrdinalIgnoreCase))
            {
                return ConfirmDestinationReceiptResult.Failure("Transfer order must be SOURCE_DISPATCHED before destination receipt.");
            }

            List<TransferOrderLine> lines = transferOrder.TransferOrderLines
                .OrderBy(line => line.LineNo)
                .ToList();
            if (lines.Count == 0)
            {
                return ConfirmDestinationReceiptResult.Failure("Transfer order has no lines.");
            }

            List<TransferLotAllocation> allocations = lines
                .SelectMany(line => line.TransferLotAllocations)
                .OrderBy(allocation => allocation.TransferLotAllocationId)
                .ToList();
            if (allocations.Count == 0)
            {
                return ConfirmDestinationReceiptResult.Failure("Transfer order has no lot allocations.");
            }

            foreach (TransferOrderLine line in lines)
            {
                if (!IsQtyEqual(line.DispatchedQty, line.RequestedQty))
                {
                    return ConfirmDestinationReceiptResult.Failure(
                        $"Dispatched quantity does not match requested quantity on line {line.LineNo}.");
                }

                decimal allocatedDispatchedQty = line.TransferLotAllocations.Sum(allocation => allocation.DispatchedQty);
                if (!IsQtyEqual(allocatedDispatchedQty, line.DispatchedQty))
                {
                    return ConfirmDestinationReceiptResult.Failure(
                        $"Lot dispatched quantity is inconsistent on line {line.LineNo}.");
                }
            }

            DateTime now = DateTime.UtcNow;
            DateOnly destinationReceivedDate = DateOnly.FromDateTime(now);
            Dictionary<long, TransferOrderLine> lineById = lines.ToDictionary(line => line.TransferOrderLineId);

            List<DestinationLotKey> destinationLotKeys = allocations
                .Select(allocation =>
                {
                    TransferOrderLine line = lineById[allocation.TransferOrderLineId];
                    return new DestinationLotKey(line.ProductId, allocation.LotCodeSnapshot);
                })
                .Distinct()
                .ToList();

            List<int> destinationProductIds = destinationLotKeys.Select(key => key.ProductId).Distinct().ToList();
            List<string> destinationLotCodes = destinationLotKeys.Select(key => key.LotCode).Distinct().ToList();

            List<ProductLot> existingDestinationLots = await _dbContext.ProductLots
                .Where(lot =>
                    lot.WarehouseId == transferOrder.DestinationWarehouseId &&
                    destinationProductIds.Contains(lot.ProductId) &&
                    destinationLotCodes.Contains(lot.LotCode))
                .ToListAsync(ct);

            Dictionary<DestinationLotKey, ProductLot> destinationLotMap = existingDestinationLots
                .ToDictionary(lot => new DestinationLotKey(lot.ProductId, lot.LotCode));

            foreach (TransferLotAllocation allocation in allocations)
            {
                TransferOrderLine line = lineById[allocation.TransferOrderLineId];
                DestinationLotKey key = new(line.ProductId, allocation.LotCodeSnapshot);

                if (destinationLotMap.TryGetValue(key, out ProductLot? existingLot))
                {
                    if (existingLot.ExpiryDate != allocation.ExpiryDateSnapshot ||
                        existingLot.UnitCost != allocation.UnitCost)
                    {
                        return ConfirmDestinationReceiptResult.Failure(
                            $"Destination lot '{existingLot.LotCode}' has different metadata and cannot be reused.");
                    }

                    if (string.Equals(existingLot.Status, ProductLotStatusLocked, StringComparison.OrdinalIgnoreCase))
                    {
                        return ConfirmDestinationReceiptResult.Failure(
                            $"Destination lot '{existingLot.LotCode}' is locked and cannot receive quantity.");
                    }

                    continue;
                }

                ProductLot newDestinationLot = new()
                {
                    WarehouseId = transferOrder.DestinationWarehouseId,
                    ProductId = line.ProductId,
                    LotCode = allocation.LotCodeSnapshot,
                    ReceivedDate = destinationReceivedDate,
                    ExpiryDate = allocation.ExpiryDateSnapshot,
                    UnitCost = allocation.UnitCost,
                    InitialQty = 0,
                    RemainingQty = 0,
                    SupplierId = null,
                    Status = ProductLotStatusActive,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = Array.Empty<byte>()
                };

                _dbContext.ProductLots.Add(newDestinationLot);
                destinationLotMap[key] = newDestinationLot;
            }

            await _dbContext.SaveChangesAsync(ct);

            List<long> destinationLotIds = destinationLotMap
                .Values
                .Select(lot => lot.ProductLotId)
                .Distinct()
                .ToList();

            Dictionary<long, StockBalance> destinationStockBalanceMap = await _dbContext.StockBalances
                .Where(balance =>
                    balance.WarehouseId == transferOrder.DestinationWarehouseId &&
                    destinationLotIds.Contains(balance.ProductLotId))
                .ToDictionaryAsync(balance => balance.ProductLotId, ct);

            foreach (TransferLotAllocation allocation in allocations)
            {
                TransferOrderLine line = lineById[allocation.TransferOrderLineId];
                DestinationLotKey key = new(line.ProductId, allocation.LotCodeSnapshot);
                ProductLot destinationLot = destinationLotMap[key];

                decimal receiveQty = decimal.Round(allocation.DispatchedQty, 3);

                if (!destinationStockBalanceMap.TryGetValue(destinationLot.ProductLotId, out StockBalance? stockBalance))
                {
                    stockBalance = new StockBalance
                    {
                        WarehouseId = transferOrder.DestinationWarehouseId,
                        ProductId = line.ProductId,
                        ProductLotId = destinationLot.ProductLotId,
                        OnHandQty = 0,
                        AllocatedQty = 0,
                        UpdatedAt = now,
                        LastMovementAt = now,
                        RowVersion = Array.Empty<byte>()
                    };

                    _dbContext.StockBalances.Add(stockBalance);
                    destinationStockBalanceMap[destinationLot.ProductLotId] = stockBalance;
                }

                stockBalance.OnHandQty = decimal.Round(stockBalance.OnHandQty + receiveQty, 3);
                stockBalance.LastMovementAt = now;
                stockBalance.UpdatedAt = now;

                destinationLot.InitialQty = decimal.Round(destinationLot.InitialQty + receiveQty, 3);
                destinationLot.RemainingQty = decimal.Round(destinationLot.RemainingQty + receiveQty, 3);
                destinationLot.Status = destinationLot.RemainingQty <= 0 ? ProductLotStatusDepleted : ProductLotStatusActive;
                destinationLot.UpdatedAt = now;

                allocation.DestinationProductLotId = destinationLot.ProductLotId;
                allocation.ReceivedQty = receiveQty;
            }

            foreach (TransferOrderLine line in lines)
            {
                line.ReceivedQty = decimal.Round(line.DispatchedQty, 3);
            }

            StockTransaction transferInTransaction = await CreateTransferInTransactionAsync(
                transferOrder,
                lines,
                destinationLotMap,
                actorUserId,
                now,
                ct);

            transferOrder.TransferStatus = TransferStatusDestinationReceived;
            transferOrder.DestinationConfirmedByUserId = actorUserId;
            transferOrder.DestinationConfirmedAt = now;
            transferOrder.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CONFIRM_DESTINATION_RECEIPT",
                entityName: "TransferOrders",
                entityId: transferOrder.TransferOrderId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    transferOrder.TransferNo,
                    transferOrder.DestinationWarehouseId,
                    transferInTransaction.TransactionNo
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return ConfirmDestinationReceiptResult.Success(transferOrder.TransferOrderId, transferOrder.TransferNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return ConfirmDestinationReceiptResult.Failure("Destination receipt failed because data was modified by another user.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return ConfirmDestinationReceiptResult.Failure("Destination receipt failed due to a database update conflict.");
        }
    }

    public async Task<CancelTransferResult> CancelCreatedTransferAsync(long transferOrderId, int actorUserId, CancellationToken ct)
    {
        TransferAuthorizationContext authorization = await EnsureStaffOrAdminAsync(actorUserId, ct);
        if (!authorization.IsSuccess)
        {
            return CancelTransferResult.Failure(authorization.ErrorMessage ?? "Access denied.");
        }

        _dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction dbTransaction = await BeginTransactionAsync(serializable: true, ct);

        try
        {
            TransferOrder? transferOrder = await _dbContext.TransferOrders
                .Include(order => order.TransferOrderLines)
                .ThenInclude(line => line.TransferLotAllocations)
                .FirstOrDefaultAsync(order => order.TransferOrderId == transferOrderId, ct);

            if (transferOrder is null)
            {
                return CancelTransferResult.Failure("Transfer order not found.");
            }

            if (!authorization.IsAdmin && authorization.AssignedWarehouseId != transferOrder.SourceWarehouseId)
            {
                return CancelTransferResult.Failure("Access denied. You can only cancel transfers for your assigned source warehouse.");
            }

            if (string.Equals(transferOrder.TransferStatus, TransferStatusCancelled, StringComparison.OrdinalIgnoreCase))
            {
                return CancelTransferResult.Success(
                    transferOrder.TransferOrderId,
                    transferOrder.TransferNo,
                    transferOrder.UpdatedAt ?? DateTime.UtcNow);
            }

            if (!string.Equals(transferOrder.TransferStatus, TransferStatusCreated, StringComparison.OrdinalIgnoreCase))
            {
                return CancelTransferResult.Failure("Only transfer orders in CREATED status can be cancelled.");
            }

            List<TransferLotAllocation> allocations = transferOrder.TransferOrderLines
                .SelectMany(line => line.TransferLotAllocations)
                .ToList();

            IReadOnlyCollection<long> sourceLotIds = allocations
                .Select(allocation => allocation.SourceProductLotId)
                .Distinct()
                .ToArray();

            Dictionary<long, StockBalance> stockBalanceMap = await _dbContext.StockBalances
                .Where(balance =>
                    balance.WarehouseId == transferOrder.SourceWarehouseId &&
                    sourceLotIds.Contains(balance.ProductLotId))
                .ToDictionaryAsync(balance => balance.ProductLotId, ct);

            foreach (TransferLotAllocation allocation in allocations)
            {
                if (!stockBalanceMap.TryGetValue(allocation.SourceProductLotId, out StockBalance? stockBalance))
                {
                    return CancelTransferResult.Failure($"Stock balance not found for lot id {allocation.SourceProductLotId}.");
                }

                if (stockBalance.AllocatedQty < allocation.DispatchedQty)
                {
                    return CancelTransferResult.Failure(
                        $"Reserved quantity for lot id {allocation.SourceProductLotId} is inconsistent. Cannot cancel safely.");
                }
            }

            DateTime now = DateTime.UtcNow;
            foreach (TransferLotAllocation allocation in allocations)
            {
                StockBalance stockBalance = stockBalanceMap[allocation.SourceProductLotId];
                stockBalance.AllocatedQty = decimal.Round(stockBalance.AllocatedQty - allocation.DispatchedQty, 3);
                if (stockBalance.AllocatedQty < 0)
                {
                    stockBalance.AllocatedQty = 0;
                }
                stockBalance.UpdatedAt = now;
            }

            transferOrder.TransferStatus = TransferStatusCancelled;
            transferOrder.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            await _auditLogService.LogAsync(
                actionType: "CANCEL_TRANSFER",
                entityName: "TransferOrders",
                entityId: transferOrder.TransferOrderId.ToString(),
                userId: actorUserId,
                isSuccess: true,
                severity: "INFO",
                details: new
                {
                    transferOrder.TransferNo,
                    transferOrder.SourceWarehouseId
                },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return CancelTransferResult.Success(transferOrder.TransferOrderId, transferOrder.TransferNo, now);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CancelTransferResult.Failure("Transfer cancellation failed because data was modified by another user.");
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync(ct);
            _dbContext.ChangeTracker.Clear();
            return CancelTransferResult.Failure("Transfer cancellation failed due to a database update conflict.");
        }
    }

    private async Task<StockTransaction> CreateTransferOutTransactionAsync(
        TransferOrder transferOrder,
        IReadOnlyList<TransferOrderLine> lines,
        int actorUserId,
        DateTime now,
        CancellationToken ct)
    {
        string transactionNo = await GenerateStockTransactionNoAsync("TRFOUT", now, ct);

        List<StockTransactionLine> transactionLines = lines
            .SelectMany(line => line.TransferLotAllocations.Select(allocation => new StockTransactionLine
            {
                LineNo = 0,
                ProductId = line.ProductId,
                ProductLotId = allocation.SourceProductLotId,
                Qty = decimal.Round(allocation.DispatchedQty, 3),
                UnitCost = allocation.UnitCost,
                UnitPrice = null,
                CogsAmount = RoundMoney(allocation.DispatchedQty * allocation.UnitCost),
                ReceivedDateSnapshot = allocation.ReceivedDateSnapshot,
                ExpiryDateSnapshot = allocation.ExpiryDateSnapshot,
                Notes = "TRANSFER_OUT_ALLOCATED",
                CreatedAt = now,
                RowVersion = Array.Empty<byte>()
            }))
            .OrderBy(line => line.ProductId)
            .ThenBy(line => line.ProductLotId)
            .ToList();

        for (int index = 0; index < transactionLines.Count; index++)
        {
            transactionLines[index].LineNo = index + 1;
        }

        StockTransaction transaction = new()
        {
            TransactionNo = transactionNo,
            TransactionType = "TRANSFER_OUT",
            DocumentStatus = "POSTED",
            WarehouseId = transferOrder.SourceWarehouseId,
            TransactionDate = now,
            SupplierId = null,
            CustomerId = null,
            ReferenceType = "TRANSFER_ORDER",
            ReferenceNo = transferOrder.TransferNo,
            AdjustmentReason = null,
            Remarks = transferOrder.Remarks,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            PostedByUserId = actorUserId,
            PostedAt = now,
            TotalAmount = transactionLines.Sum(line => RoundMoney(line.Qty * line.UnitCost)),
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>(),
            StockTransactionLines = transactionLines
        };

        _dbContext.StockTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(ct);

        return transaction;
    }

    private async Task<StockTransaction> CreateTransferInTransactionAsync(
        TransferOrder transferOrder,
        IReadOnlyList<TransferOrderLine> lines,
        IReadOnlyDictionary<DestinationLotKey, ProductLot> destinationLotMap,
        int actorUserId,
        DateTime now,
        CancellationToken ct)
    {
        string transactionNo = await GenerateStockTransactionNoAsync("TRFIN", now, ct);

        List<StockTransactionLine> transactionLines = lines
            .SelectMany(line => line.TransferLotAllocations.Select(allocation =>
            {
                DestinationLotKey key = new(line.ProductId, allocation.LotCodeSnapshot);
                ProductLot destinationLot = destinationLotMap[key];

                return new StockTransactionLine
                {
                    LineNo = 0,
                    ProductId = line.ProductId,
                    ProductLotId = destinationLot.ProductLotId,
                    Qty = decimal.Round(allocation.ReceivedQty, 3),
                    UnitCost = allocation.UnitCost,
                    UnitPrice = null,
                    CogsAmount = null,
                    ReceivedDateSnapshot = allocation.ReceivedDateSnapshot,
                    ExpiryDateSnapshot = allocation.ExpiryDateSnapshot,
                    Notes = "TRANSFER_IN_RECEIVED",
                    CreatedAt = now,
                    RowVersion = Array.Empty<byte>()
                };
            }))
            .OrderBy(line => line.ProductId)
            .ThenBy(line => line.ProductLotId)
            .ToList();

        for (int index = 0; index < transactionLines.Count; index++)
        {
            transactionLines[index].LineNo = index + 1;
        }

        StockTransaction transaction = new()
        {
            TransactionNo = transactionNo,
            TransactionType = "TRANSFER_IN",
            DocumentStatus = "POSTED",
            WarehouseId = transferOrder.DestinationWarehouseId,
            TransactionDate = now,
            SupplierId = null,
            CustomerId = null,
            ReferenceType = "TRANSFER_ORDER",
            ReferenceNo = transferOrder.TransferNo,
            AdjustmentReason = null,
            Remarks = transferOrder.Remarks,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            PostedByUserId = actorUserId,
            PostedAt = now,
            TotalAmount = transactionLines.Sum(line => RoundMoney(line.Qty * line.UnitCost)),
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>(),
            StockTransactionLines = transactionLines
        };

        _dbContext.StockTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync(ct);

        return transaction;
    }

    private async Task<ValidationResult> ValidateWarehousesAndAccessAsync(
        int sourceWarehouseId,
        int destinationWarehouseId,
        TransferAuthorizationContext authorization,
        CancellationToken ct)
    {
        if (sourceWarehouseId <= 0)
        {
            return ValidationResult.Failure("Source warehouse is required.");
        }

        if (destinationWarehouseId <= 0)
        {
            return ValidationResult.Failure("Destination warehouse is required.");
        }

        if (sourceWarehouseId == destinationWarehouseId)
        {
            return ValidationResult.Failure("Source warehouse and destination warehouse must be different.");
        }

        Dictionary<int, bool> warehouseActiveMap = await _dbContext.Warehouses
            .AsNoTracking()
            .Where(warehouse => warehouse.WarehouseId == sourceWarehouseId || warehouse.WarehouseId == destinationWarehouseId)
            .ToDictionaryAsync(warehouse => warehouse.WarehouseId, warehouse => warehouse.IsActive, ct);

        if (!warehouseActiveMap.TryGetValue(sourceWarehouseId, out bool sourceActive) || !sourceActive)
        {
            return ValidationResult.Failure("Selected source warehouse is invalid or inactive.");
        }

        if (!warehouseActiveMap.TryGetValue(destinationWarehouseId, out bool destinationActive) || !destinationActive)
        {
            return ValidationResult.Failure("Selected destination warehouse is invalid or inactive.");
        }

        if (!authorization.IsAdmin && authorization.AssignedWarehouseId != sourceWarehouseId)
        {
            return ValidationResult.Failure("Access denied. Source warehouse must match your warehouse assignment.");
        }

        return ValidationResult.Success();
    }

    private static NormalizeSuggestionResult NormalizeSuggestionLines(IReadOnlyCollection<TransferSuggestionLineDto> inputLines)
    {
        if (inputLines.Count == 0)
        {
            return NormalizeSuggestionResult.Failure("At least one transfer line is required.");
        }

        List<NormalizedSuggestionLine> normalizedLines = [];
        HashSet<int> seenProductIds = [];

        foreach (TransferSuggestionLineDto inputLine in inputLines)
        {
            if (inputLine.ProductId <= 0)
            {
                return NormalizeSuggestionResult.Failure("Product is required on every line.");
            }

            decimal requestedQty = decimal.Round(inputLine.RequestedQty, 3);
            if (requestedQty <= 0)
            {
                return NormalizeSuggestionResult.Failure("Requested quantity must be greater than 0.");
            }

            if (!seenProductIds.Add(inputLine.ProductId))
            {
                return NormalizeSuggestionResult.Failure("A product can only appear once in transfer lines.");
            }

            normalizedLines.Add(new NormalizedSuggestionLine
            {
                ProductId = inputLine.ProductId,
                RequestedQty = requestedQty
            });
        }

        return NormalizeSuggestionResult.Success(normalizedLines);
    }

    private static NormalizeCreateResult NormalizeCreateLines(IReadOnlyCollection<TransferCreateLineDto> inputLines)
    {
        if (inputLines.Count == 0)
        {
            return NormalizeCreateResult.Failure("At least one transfer line is required.");
        }

        List<NormalizedCreateLine> normalizedLines = [];
        HashSet<int> seenProductIds = [];

        foreach (TransferCreateLineDto inputLine in inputLines)
        {
            if (inputLine.ProductId <= 0)
            {
                return NormalizeCreateResult.Failure("Product is required on every line.");
            }

            decimal requestedQty = decimal.Round(inputLine.RequestedQty, 3);
            if (requestedQty <= 0)
            {
                return NormalizeCreateResult.Failure("Requested quantity must be greater than 0.");
            }

            if (!seenProductIds.Add(inputLine.ProductId))
            {
                return NormalizeCreateResult.Failure("A product can only appear once in transfer lines.");
            }

            if (inputLine.LotSelections.Count == 0)
            {
                return NormalizeCreateResult.Failure("Each transfer line must include at least one selected lot.");
            }

            List<NormalizedLotSelection> lotSelections = inputLine.LotSelections
                .GroupBy(selection => selection.SourceProductLotId)
                .Select(group => new NormalizedLotSelection
                {
                    SourceProductLotId = group.Key,
                    Qty = decimal.Round(group.Sum(item => item.Qty), 3)
                })
                .OrderBy(selection => selection.SourceProductLotId)
                .ToList();

            if (lotSelections.Any(selection => selection.SourceProductLotId <= 0))
            {
                return NormalizeCreateResult.Failure("Selected lot id is invalid.");
            }

            if (lotSelections.Any(selection => selection.Qty <= 0))
            {
                return NormalizeCreateResult.Failure("Selected lot quantity must be greater than 0.");
            }

            decimal selectedQty = lotSelections.Sum(selection => selection.Qty);
            if (!IsQtyEqual(selectedQty, requestedQty))
            {
                return NormalizeCreateResult.Failure(
                    $"Selected lot quantity ({selectedQty:N3}) must equal requested quantity ({requestedQty:N3}) for product id {inputLine.ProductId}.");
            }

            normalizedLines.Add(new NormalizedCreateLine
            {
                ProductId = inputLine.ProductId,
                RequestedQty = requestedQty,
                LotSelections = lotSelections
            });
        }

        return NormalizeCreateResult.Success(normalizedLines);
    }

    private async Task<TransferAuthorizationContext> EnsureStaffOrAdminAsync(int actorUserId, CancellationToken ct)
    {
        OperationResult sessionValidation = await _sessionValidationService.EnsureSessionForUserAsync(actorUserId, null, ct);
        if (!sessionValidation.IsSuccess)
        {
            return TransferAuthorizationContext.Failure(sessionValidation.ErrorMessage ?? "Session expired.");
        }

        bool isAdmin = _currentUserContext.IsInRole(AdminRoleCode);
        bool isStaff = _currentUserContext.IsInRole(StaffRoleCode);
        if (!isAdmin && !isStaff)
        {
            return TransferAuthorizationContext.Failure($"Access denied. Role '{StaffRoleCode}' is required.");
        }

        if (isAdmin)
        {
            return TransferAuthorizationContext.Success(isAdmin: true, assignedWarehouseId: null);
        }

        int? assignedWarehouseId = await _dbContext.UserWarehouseAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UserId == actorUserId)
            .Select(assignment => (int?)assignment.WarehouseId)
            .SingleOrDefaultAsync(ct);

        if (!assignedWarehouseId.HasValue)
        {
            return TransferAuthorizationContext.Failure("Your account has no warehouse assignment.");
        }

        return TransferAuthorizationContext.Success(isAdmin: false, assignedWarehouseId: assignedWarehouseId.Value);
    }

    private async Task EnsureCurrentSessionOrThrowAsync(CancellationToken ct)
    {
        OperationResult sessionValidation = await _sessionValidationService.EnsureCurrentSessionAsync(null, ct);
        if (!sessionValidation.IsSuccess)
        {
            throw new UnauthorizedAccessException(sessionValidation.ErrorMessage ?? "Session expired.");
        }
    }

    private async Task<string> GenerateTransferNoAsync(DateTime requestDate, CancellationToken ct)
    {
        string dateToken = requestDate.ToString("yyyyMMdd");

        for (int attempt = 0; attempt < 5; attempt++)
        {
            string candidate = $"TO-{dateToken}-{DateTime.UtcNow:HHmmssfff}";
            bool exists = await _dbContext.TransferOrders
                .AsNoTracking()
                .AnyAsync(order => order.TransferNo == candidate, ct);
            if (!exists)
            {
                return candidate;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), ct);
        }

        return $"TO-{dateToken}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
    }

    private async Task<string> GenerateStockTransactionNoAsync(string prefix, DateTime transactionDate, CancellationToken ct)
    {
        string dateToken = transactionDate.ToString("yyyyMMdd");

        for (int attempt = 0; attempt < 5; attempt++)
        {
            string candidate = $"{prefix}-{dateToken}-{DateTime.UtcNow:HHmmssfff}";
            bool exists = await _dbContext.StockTransactions
                .AsNoTracking()
                .AnyAsync(transaction => transaction.TransactionNo == candidate, ct);
            if (!exists)
            {
                return candidate;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), ct);
        }

        return $"{prefix}-{dateToken}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
    }

    private Task<IDbContextTransaction> BeginTransactionAsync(bool serializable, CancellationToken ct)
    {
        if (serializable && _dbContext.Database.IsRelational())
        {
            return _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        }

        return _dbContext.Database.BeginTransactionAsync(ct);
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsQtyEqual(decimal left, decimal right)
    {
        return Math.Abs(left - right) <= 0.0005m;
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class AvailableLotSnapshot
    {
        public int ProductId { get; init; }

        public long SourceProductLotId { get; init; }

        public string LotCode { get; init; } = string.Empty;

        public DateOnly ReceivedDate { get; init; }

        public DateOnly? ExpiryDate { get; init; }

        public decimal UnitCost { get; init; }

        public string LotStatus { get; init; } = string.Empty;

        public decimal AvailableQty { get; init; }
    }

    private sealed class NormalizedSuggestionLine
    {
        public int ProductId { get; init; }

        public decimal RequestedQty { get; init; }
    }

    private sealed class NormalizeSuggestionResult
    {
        public bool IsSuccess { get; private init; }

        public string? ErrorMessage { get; private init; }

        public List<NormalizedSuggestionLine> Lines { get; private init; } = [];

        public static NormalizeSuggestionResult Success(List<NormalizedSuggestionLine> lines)
        {
            return new NormalizeSuggestionResult
            {
                IsSuccess = true,
                Lines = lines
            };
        }

        public static NormalizeSuggestionResult Failure(string errorMessage)
        {
            return new NormalizeSuggestionResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed class NormalizedCreateLine
    {
        public int ProductId { get; init; }

        public decimal RequestedQty { get; init; }

        public List<NormalizedLotSelection> LotSelections { get; init; } = [];
    }

    private sealed class NormalizedLotSelection
    {
        public long SourceProductLotId { get; init; }

        public decimal Qty { get; init; }
    }

    private sealed class NormalizeCreateResult
    {
        public bool IsSuccess { get; private init; }

        public string? ErrorMessage { get; private init; }

        public List<NormalizedCreateLine> Lines { get; private init; } = [];

        public static NormalizeCreateResult Success(List<NormalizedCreateLine> lines)
        {
            return new NormalizeCreateResult
            {
                IsSuccess = true,
                Lines = lines
            };
        }

        public static NormalizeCreateResult Failure(string errorMessage)
        {
            return new NormalizeCreateResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed class TransferAuthorizationContext
    {
        public bool IsSuccess { get; private init; }

        public bool IsAdmin { get; private init; }

        public int? AssignedWarehouseId { get; private init; }

        public string? ErrorMessage { get; private init; }

        public static TransferAuthorizationContext Success(bool isAdmin, int? assignedWarehouseId)
        {
            return new TransferAuthorizationContext
            {
                IsSuccess = true,
                IsAdmin = isAdmin,
                AssignedWarehouseId = assignedWarehouseId
            };
        }

        public static TransferAuthorizationContext Failure(string errorMessage)
        {
            return new TransferAuthorizationContext
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed class ValidationResult
    {
        public bool IsSuccess { get; private init; }

        public string? ErrorMessage { get; private init; }

        public static ValidationResult Success()
        {
            return new ValidationResult { IsSuccess = true };
        }

        public static ValidationResult Failure(string errorMessage)
        {
            return new ValidationResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed record DestinationLotKey(int ProductId, string LotCode);
}
