using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.Test;

public class TransferServiceTests
{
    [Fact]
    public async Task PreviewCreateTransferLotSuggestionAsync_ShouldAllocateByFefoThenFifo()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 1001, lotCode: "LOT-FEFO", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 1002, lotCode: "LOT-FIFO", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        PreviewCreateTransferLotSuggestionResult result = await harness.Service.PreviewCreateTransferLotSuggestionAsync(
            BuildSuggestionRequest(qty: 5m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.SuggestionItems.Count);
        Assert.Equal("LOT-FEFO", result.SuggestionItems[0].LotCode);
        Assert.Equal("FEFO", result.SuggestionItems[0].AllocationRule);
        Assert.Equal(4m, result.SuggestionItems[0].SuggestedQty);
        Assert.Equal("LOT-FIFO", result.SuggestionItems[1].LotCode);
        Assert.Equal("FIFO", result.SuggestionItems[1].AllocationRule);
        Assert.Equal(1m, result.SuggestionItems[1].SuggestedQty);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldCreateAndReserveSelectedLots()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 2001, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 2002, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        CreateTransferResult result = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 2001, firstQty: 4m, secondLotId: 2002, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.TransferOrderId);

        TransferOrder order = await harness.DbContext.TransferOrders
            .Include(x => x.TransferOrderLines)
            .ThenInclude(line => line.TransferLotAllocations)
            .SingleAsync(x => x.TransferOrderId == result.TransferOrderId!.Value);

        StockBalance lotA = await harness.DbContext.StockBalances.SingleAsync(x => x.WarehouseId == 1 && x.ProductLotId == 2001);
        StockBalance lotB = await harness.DbContext.StockBalances.SingleAsync(x => x.WarehouseId == 1 && x.ProductLotId == 2002);

        Assert.Equal("CREATED", order.TransferStatus);
        Assert.Single(order.TransferOrderLines);
        Assert.Equal(2, order.TransferOrderLines.Single().TransferLotAllocations.Count);
        Assert.Equal(4m, lotA.AllocatedQty);
        Assert.Equal(1m, lotB.AllocatedQty);
        Assert.Contains("CREATE_TRANSFER", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFailWhenSelectedLotQtyMismatchRequestedQty()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 2101, lotCode: "LOT-A", onHandQty: 5m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5));

        CreateTransferResult result = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 2101, firstQty: 4m, secondLotId: 2101, secondQty: 0m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("must equal requested quantity", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFailWhenSelectedLotQtyExceedsAvailableQty()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 2201, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5));

        CreateTransferResult result = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 2201, firstQty: 5m, secondLotId: 2201, secondQty: 0m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Insufficient available quantity", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFailWhenRequiredDateIsNotLaterThanRequestDate()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 2251, lotCode: "LOT-A", onHandQty: 5m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5));

        DateTime requestDate = DateTime.UtcNow.Date;
        CreateTransferResult result = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(
                requestedQty: 5m,
                firstLotId: 2251,
                firstQty: 5m,
                secondLotId: 2251,
                secondQty: 0m,
                requestDate: requestDate,
                requiredDate: DateOnly.FromDateTime(requestDate),
                includeRequiredDate: true),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Required date must be later than request date", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldAllowNullRequiredDate()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 2252, lotCode: "LOT-A", onHandQty: 5m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5));

        DateTime requestDate = DateTime.UtcNow.Date;
        CreateTransferResult result = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(
                requestedQty: 5m,
                firstLotId: 2252,
                firstQty: 5m,
                secondLotId: 2252,
                secondQty: 0m,
                requestDate: requestDate,
                requiredDate: null,
                includeRequiredDate: false),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        TransferOrder order = await harness.DbContext.TransferOrders
            .SingleAsync(x => x.TransferOrderId == result.TransferOrderId!.Value);
        Assert.Null(order.RequiredDate);
    }

    [Fact]
    public async Task GetAvailableQtyAsync_ShouldExcludeLockedAndExpiredLots()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        DateTime now = DateTime.UtcNow;

        harness.DbContext.ProductLots.AddRange(
            new ProductLot
            {
                ProductLotId = 2301,
                WarehouseId = 1,
                ProductId = 1,
                LotCode = "LOT-ACTIVE",
                ReceivedDate = DateOnly.FromDateTime(now.Date.AddDays(-10)),
                ExpiryDate = DateOnly.FromDateTime(now.Date.AddDays(5)),
                UnitCost = 10m,
                InitialQty = 10m,
                RemainingQty = 10m,
                Status = "ACTIVE",
                CreatedAt = now,
                RowVersion = [1]
            },
            new ProductLot
            {
                ProductLotId = 2302,
                WarehouseId = 1,
                ProductId = 1,
                LotCode = "LOT-LOCKED",
                ReceivedDate = DateOnly.FromDateTime(now.Date.AddDays(-8)),
                ExpiryDate = DateOnly.FromDateTime(now.Date.AddDays(5)),
                UnitCost = 10m,
                InitialQty = 5m,
                RemainingQty = 5m,
                Status = "LOCKED",
                CreatedAt = now,
                RowVersion = [1]
            },
            new ProductLot
            {
                ProductLotId = 2303,
                WarehouseId = 1,
                ProductId = 1,
                LotCode = "LOT-EXPIRED",
                ReceivedDate = DateOnly.FromDateTime(now.Date.AddDays(-7)),
                ExpiryDate = DateOnly.FromDateTime(now.Date.AddDays(-1)),
                UnitCost = 10m,
                InitialQty = 4m,
                RemainingQty = 4m,
                Status = "ACTIVE",
                CreatedAt = now,
                RowVersion = [1]
            },
            new ProductLot
            {
                ProductLotId = 2304,
                WarehouseId = 1,
                ProductId = 1,
                LotCode = "LOT-NO-EXP",
                ReceivedDate = DateOnly.FromDateTime(now.Date.AddDays(-6)),
                ExpiryDate = null,
                UnitCost = 10m,
                InitialQty = 3m,
                RemainingQty = 3m,
                Status = "ACTIVE",
                CreatedAt = now,
                RowVersion = [1]
            });

        harness.DbContext.StockBalances.AddRange(
            new StockBalance
            {
                WarehouseId = 1,
                ProductId = 1,
                ProductLotId = 2301,
                OnHandQty = 10m,
                AllocatedQty = 2m,
                AvailableQty = null,
                UpdatedAt = now,
                RowVersion = [1]
            },
            new StockBalance
            {
                WarehouseId = 1,
                ProductId = 1,
                ProductLotId = 2302,
                OnHandQty = 5m,
                AllocatedQty = 0m,
                AvailableQty = null,
                UpdatedAt = now,
                RowVersion = [1]
            },
            new StockBalance
            {
                WarehouseId = 1,
                ProductId = 1,
                ProductLotId = 2303,
                OnHandQty = 4m,
                AllocatedQty = 0m,
                AvailableQty = null,
                UpdatedAt = now,
                RowVersion = [1]
            },
            new StockBalance
            {
                WarehouseId = 1,
                ProductId = 1,
                ProductLotId = 2304,
                OnHandQty = 3m,
                AllocatedQty = 1m,
                AvailableQty = null,
                UpdatedAt = now,
                RowVersion = [1]
            });

        await harness.DbContext.SaveChangesAsync();

        decimal availableQty = await harness.Service.GetAvailableQtyAsync(
            sourceWarehouseId: 1,
            productId: 1,
            requestDate: now.Date,
            actorUserId: 1,
            CancellationToken.None);

        Assert.Equal(10m, availableQty);
    }

    [Fact]
    public async Task GetAvailableQtyAsync_ShouldRejectStaffFromDifferentWarehouse()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.Service.GetAvailableQtyAsync(
                sourceWarehouseId: 2,
                productId: 1,
                requestDate: DateTime.UtcNow.Date,
                actorUserId: 1,
                CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmSourceDispatchAsync_ShouldDeductSourceStockAndConsumeReservation()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 3001, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 3002, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        CreateTransferResult createResult = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 3001, firstQty: 4m, secondLotId: 3002, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);

        ConfirmSourceDispatchResult dispatchResult = await harness.Service.ConfirmSourceDispatchAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(dispatchResult.IsSuccess);

        TransferOrder order = await harness.DbContext.TransferOrders
            .Include(x => x.TransferOrderLines)
            .SingleAsync(x => x.TransferOrderId == createResult.TransferOrderId!.Value);

        StockBalance lotA = await harness.DbContext.StockBalances.SingleAsync(x => x.WarehouseId == 1 && x.ProductLotId == 3001);
        StockBalance lotB = await harness.DbContext.StockBalances.SingleAsync(x => x.WarehouseId == 1 && x.ProductLotId == 3002);

        Assert.Equal("SOURCE_DISPATCHED", order.TransferStatus);
        Assert.Equal(5m, order.TransferOrderLines.Single().DispatchedQty);
        Assert.Equal(0m, lotA.OnHandQty);
        Assert.Equal(0m, lotA.AllocatedQty);
        Assert.Equal(5m, lotB.OnHandQty);
        Assert.Equal(0m, lotB.AllocatedQty);

        bool hasTransferOut = await harness.DbContext.StockTransactions
            .AnyAsync(transaction => transaction.TransactionType == "TRANSFER_OUT" && transaction.ReferenceNo == order.TransferNo);
        Assert.True(hasTransferOut);
        Assert.Contains("CONFIRM_SOURCE_DISPATCH", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task ConfirmDestinationReceiptAsync_ShouldIncreaseDestinationStockAndCreateTransferIn()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 4001, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 4002, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        CreateTransferResult createResult = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 4001, firstQty: 4m, secondLotId: 4002, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        ConfirmSourceDispatchResult dispatchResult = await harness.Service.ConfirmSourceDispatchAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(dispatchResult.IsSuccess);

        harness.CurrentUserContext.SetUser(
            userId: 2,
            username: "staff02",
            fullName: "Staff 02",
            roleCodes: ["WAREHOUSE_STAFF"]);

        ConfirmDestinationReceiptResult receiptResult = await harness.Service.ConfirmDestinationReceiptAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 2,
            CancellationToken.None);
        Assert.True(receiptResult.IsSuccess);

        TransferOrder order = await harness.DbContext.TransferOrders
            .Include(x => x.TransferOrderLines)
            .SingleAsync(x => x.TransferOrderId == createResult.TransferOrderId!.Value);

        Assert.Equal("DESTINATION_RECEIVED", order.TransferStatus);
        Assert.Equal(5m, order.TransferOrderLines.Single().ReceivedQty);

        ProductLot destinationLotA = await harness.DbContext.ProductLots
            .SingleAsync(lot => lot.WarehouseId == 2 && lot.ProductId == 1 && lot.LotCode == "LOT-A");
        ProductLot destinationLotB = await harness.DbContext.ProductLots
            .SingleAsync(lot => lot.WarehouseId == 2 && lot.ProductId == 1 && lot.LotCode == "LOT-B");

        StockBalance destinationBalanceA = await harness.DbContext.StockBalances
            .SingleAsync(balance => balance.WarehouseId == 2 && balance.ProductLotId == destinationLotA.ProductLotId);
        StockBalance destinationBalanceB = await harness.DbContext.StockBalances
            .SingleAsync(balance => balance.WarehouseId == 2 && balance.ProductLotId == destinationLotB.ProductLotId);

        Assert.Equal(4m, destinationBalanceA.OnHandQty);
        Assert.Equal(1m, destinationBalanceB.OnHandQty);

        bool hasTransferIn = await harness.DbContext.StockTransactions
            .AnyAsync(transaction => transaction.TransactionType == "TRANSFER_IN" && transaction.ReferenceNo == order.TransferNo);
        Assert.True(hasTransferIn);
        Assert.Contains("CONFIRM_DESTINATION_RECEIPT", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task CancelCreatedTransferAsync_ShouldReleaseReservedQty()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 5001, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 5002, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        CreateTransferResult createResult = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 5001, firstQty: 4m, secondLotId: 5002, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        CancelTransferResult cancelResult = await harness.Service.CancelCreatedTransferAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(cancelResult.IsSuccess);

        TransferOrder order = await harness.DbContext.TransferOrders
            .SingleAsync(x => x.TransferOrderId == createResult.TransferOrderId!.Value);
        StockBalance lotA = await harness.DbContext.StockBalances.SingleAsync(x => x.WarehouseId == 1 && x.ProductLotId == 5001);
        StockBalance lotB = await harness.DbContext.StockBalances.SingleAsync(x => x.WarehouseId == 1 && x.ProductLotId == 5002);

        Assert.Equal("CANCELLED", order.TransferStatus);
        Assert.Equal(0m, lotA.AllocatedQty);
        Assert.Equal(0m, lotB.AllocatedQty);
        Assert.Contains("CANCEL_TRANSFER", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task ConfirmSourceDispatchAsync_ShouldRejectStaffFromDifferentWarehouse()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 6001, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 6002, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        CreateTransferResult createResult = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 6001, firstQty: 4m, secondLotId: 6002, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        harness.CurrentUserContext.SetUser(
            userId: 2,
            username: "staff02",
            fullName: "Staff 02",
            roleCodes: ["WAREHOUSE_STAFF"]);

        ConfirmSourceDispatchResult dispatchResult = await harness.Service.ConfirmSourceDispatchAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 2,
            CancellationToken.None);

        Assert.False(dispatchResult.IsSuccess);
        Assert.Contains("Access denied", dispatchResult.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task ConfirmDestinationReceiptAsync_ShouldRejectStaffFromDifferentWarehouse()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedSourceLots(
            harness.DbContext,
            (lotId: 6101, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 6102, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        CreateTransferResult createResult = await harness.Service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 6101, firstQty: 4m, secondLotId: 6102, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        ConfirmSourceDispatchResult dispatchResult = await harness.Service.ConfirmSourceDispatchAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(dispatchResult.IsSuccess);

        ConfirmDestinationReceiptResult receiptResult = await harness.Service.ConfirmDestinationReceiptAsync(
            createResult.TransferOrderId!.Value,
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(receiptResult.IsSuccess);
        Assert.Contains("Access denied", receiptResult.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldReturnClearMessageWhenConcurrencyConflictOccurs()
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"TransferServiceTests-Concurrency-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new ThrowConcurrencyOnceDbContext(options);
        SeedReferenceData(dbContext);
        SeedSourceLots(
            dbContext,
            (lotId: 7001, lotCode: "LOT-A", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 7002, lotCode: "LOT-B", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        var currentUserContext = new CurrentUserContext();
        currentUserContext.SetUser(1, "staff01", "Staff 01", ["WAREHOUSE_STAFF"]);

        var auditLogService = new RecordingAuditLogService();
        var sessionValidationService = new AlwaysValidSessionValidationService();
        var service = new TransferService(dbContext, sessionValidationService, currentUserContext, auditLogService);

        dbContext.ThrowConcurrencyOnNextSave = true;

        CreateTransferResult result = await service.CreateTransferAsync(
            BuildCreateRequest(requestedQty: 5m, firstLotId: 7001, firstQty: 4m, secondLotId: 7002, secondQty: 1m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Stock changed while reserving transfer lots", result.ErrorMessage ?? string.Empty);
    }

    private static TestHarness CreateHarness(int userId, IReadOnlyCollection<string> roleCodes)
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"TransferServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new Sp26inventoryManagementDbContext(options);
        SeedReferenceData(dbContext);

        var currentUserContext = new CurrentUserContext();
        currentUserContext.SetUser(userId, $"user{userId}", $"User {userId}", roleCodes);

        var auditLogService = new RecordingAuditLogService();
        var sessionValidationService = new AlwaysValidSessionValidationService();
        var service = new TransferService(dbContext, sessionValidationService, currentUserContext, auditLogService);

        return new TestHarness(dbContext, service, currentUserContext, auditLogService);
    }

    private static void SeedReferenceData(Sp26inventoryManagementDbContext dbContext)
    {
        DateTime now = DateTime.UtcNow;

        dbContext.Categories.Add(new Category
        {
            CategoryId = 1,
            CategoryCode = "CAT01",
            CategoryName = "Category",
            IsActive = true,
            CreatedAt = now,
            RowVersion = [1]
        });

        dbContext.Products.Add(new Product
        {
            ProductId = 1,
            Sku = "SKU-01",
            ProductName = "Transfer Product",
            CategoryId = 1,
            BaseUom = "PCS",
            TrackExpiry = true,
            IsActive = true,
            CreatedAt = now,
            RowVersion = [1]
        });

        dbContext.Warehouses.AddRange(
            new Warehouse
            {
                WarehouseId = 1,
                WarehouseCode = "WH01",
                WarehouseName = "Source WH",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new Warehouse
            {
                WarehouseId = 2,
                WarehouseCode = "WH02",
                WarehouseName = "Destination WH",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            });

        dbContext.Users.AddRange(
            new User
            {
                UserId = 1,
                Username = "staff01",
                PasswordHash = "x",
                FullName = "Staff 01",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 2,
                Username = "staff02",
                PasswordHash = "x",
                FullName = "Staff 02",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            });

        dbContext.UserWarehouseAssignments.AddRange(
            new UserWarehouseAssignment
            {
                UserId = 1,
                WarehouseId = 1,
                AssignedAt = now,
                AssignedByUserId = null,
                RowVersion = [1]
            },
            new UserWarehouseAssignment
            {
                UserId = 2,
                WarehouseId = 2,
                AssignedAt = now,
                AssignedByUserId = null,
                RowVersion = [1]
            });

        dbContext.SaveChanges();
    }

    private static void SeedSourceLots(
        Sp26inventoryManagementDbContext dbContext,
        params (long lotId, string lotCode, decimal onHandQty, decimal unitCost, int receivedDaysAgo, int? expiryInDays)[] lots)
    {
        DateTime now = DateTime.UtcNow;
        foreach (var lotSeed in lots)
        {
            DateOnly receivedDate = DateOnly.FromDateTime(now.Date.AddDays(-lotSeed.receivedDaysAgo));
            DateOnly? expiryDate = lotSeed.expiryInDays.HasValue
                ? DateOnly.FromDateTime(now.Date.AddDays(lotSeed.expiryInDays.Value))
                : null;

            dbContext.ProductLots.Add(new ProductLot
            {
                ProductLotId = lotSeed.lotId,
                WarehouseId = 1,
                ProductId = 1,
                LotCode = lotSeed.lotCode,
                ReceivedDate = receivedDate,
                ExpiryDate = expiryDate,
                UnitCost = lotSeed.unitCost,
                InitialQty = lotSeed.onHandQty,
                RemainingQty = lotSeed.onHandQty,
                Status = "ACTIVE",
                CreatedAt = now,
                RowVersion = [1]
            });

            dbContext.StockBalances.Add(new StockBalance
            {
                WarehouseId = 1,
                ProductId = 1,
                ProductLotId = lotSeed.lotId,
                OnHandQty = lotSeed.onHandQty,
                AllocatedQty = 0,
                UpdatedAt = now,
                RowVersion = [1]
            });
        }

        dbContext.SaveChanges();
    }

    private static TransferSuggestionRequestDto BuildSuggestionRequest(decimal qty)
    {
        return new TransferSuggestionRequestDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 2,
            RequestDate = DateTime.UtcNow.Date,
            Lines =
            [
                new TransferSuggestionLineDto
                {
                    ProductId = 1,
                    RequestedQty = qty
                }
            ]
        };
    }

    private static TransferCreateRequestDto BuildCreateRequest(
        decimal requestedQty,
        long firstLotId,
        decimal firstQty,
        long secondLotId,
        decimal secondQty,
        DateTime? requestDate = null,
        DateOnly? requiredDate = null,
        bool includeRequiredDate = true)
    {
        DateTime resolvedRequestDate = (requestDate ?? DateTime.UtcNow).Date;
        DateOnly? resolvedRequiredDate = includeRequiredDate
            ? (requiredDate ?? DateOnly.FromDateTime(resolvedRequestDate.AddDays(1)))
            : null;

        List<TransferLotSelectionDto> selections =
        [
            new TransferLotSelectionDto
            {
                SourceProductLotId = firstLotId,
                Qty = firstQty
            }
        ];

        if (secondQty > 0)
        {
            selections.Add(new TransferLotSelectionDto
            {
                SourceProductLotId = secondLotId,
                Qty = secondQty
            });
        }

        return new TransferCreateRequestDto
        {
            SourceWarehouseId = 1,
            DestinationWarehouseId = 2,
            RequestDate = resolvedRequestDate,
            RequiredDate = resolvedRequiredDate,
            Remarks = "Unit test transfer",
            Lines =
            [
                new TransferCreateLineDto
                {
                    ProductId = 1,
                    RequestedQty = requestedQty,
                    LotSelections = selections
                }
            ]
        };
    }

    private sealed class TestHarness(
        Sp26inventoryManagementDbContext dbContext,
        TransferService service,
        CurrentUserContext currentUserContext,
        RecordingAuditLogService auditLogService)
    {
        public Sp26inventoryManagementDbContext DbContext { get; } = dbContext;

        public TransferService Service { get; } = service;

        public CurrentUserContext CurrentUserContext { get; } = currentUserContext;

        public RecordingAuditLogService AuditLogService { get; } = auditLogService;
    }

    private sealed class AlwaysValidSessionValidationService : ISessionValidationService
    {
        public Task<OperationResult> EnsureCurrentSessionAsync(string? requiredRoleCode, CancellationToken ct)
        {
            return Task.FromResult(OperationResult.Success());
        }

        public Task<OperationResult> EnsureSessionForUserAsync(
            int expectedUserId,
            string? requiredRoleCode,
            CancellationToken ct,
            bool forceRevalidation = false)
        {
            return Task.FromResult(OperationResult.Success());
        }
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<string> ActionTypes { get; } = [];

        public Task LogAsync(
            string actionType,
            string entityName,
            string? entityId,
            int? userId,
            bool isSuccess,
            string severity,
            object? details,
            string? clientIp,
            string? clientApp,
            CancellationToken ct)
        {
            ActionTypes.Add(actionType);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowConcurrencyOnceDbContext(DbContextOptions<Sp26inventoryManagementDbContext> options)
        : Sp26inventoryManagementDbContext(options)
    {
        public bool ThrowConcurrencyOnNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrencyOnNextSave)
            {
                ThrowConcurrencyOnNextSave = false;
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
