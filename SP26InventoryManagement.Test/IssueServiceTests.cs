using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.Test;

public class IssueServiceTests
{
    [Fact]
    public async Task PreviewLotAllocationAsync_ShouldAllocateByFefoThenFifo()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 1001, warehouseId: 1, lotCode: "LOT-FEFO", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 1002, warehouseId: 1, lotCode: "LOT-FIFO", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        IssueRequestDto request = BuildIssueRequest(warehouseId: 1, qty: 5m, unitPrice: 20m);

        PreviewIssueAllocationResult result = await harness.Service.PreviewLotAllocationAsync(
            request,
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.AllocationItems.Count);
        Assert.Equal("LOT-FEFO", result.AllocationItems[0].LotCode);
        Assert.Equal("FEFO", result.AllocationItems[0].AllocationRule);
        Assert.Equal(4m, result.AllocationItems[0].AllocatedQty);
        Assert.Equal("LOT-FIFO", result.AllocationItems[1].LotCode);
        Assert.Equal("FIFO", result.AllocationItems[1].AllocationRule);
        Assert.Equal(1m, result.AllocationItems[1].AllocatedQty);
        Assert.Empty(result.Shortages);
    }

    [Fact]
    public async Task CreateIssueAsync_ShouldCreateDraftAndReserveAllocatedQty()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 2001, warehouseId: 1, lotCode: "LOT-RESERVE", onHandQty: 10m, unitCost: 9m, receivedDaysAgo: 7, expiryInDays: 15));

        IssueRequestDto request = BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 15m);

        CreateIssueResult result = await harness.Service.CreateIssueAsync(
            request,
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.TransactionId);

        StockTransaction createdTransaction = await harness.DbContext.StockTransactions
            .Include(transaction => transaction.StockTransactionLines)
            .SingleAsync(transaction => transaction.TransactionId == result.TransactionId!.Value);

        StockBalance stockBalance = await harness.DbContext.StockBalances
            .SingleAsync(balance =>
                balance.WarehouseId == 1 &&
                balance.ProductId == 1 &&
                balance.ProductLotId == 2001);

        Assert.Equal("DRAFT", createdTransaction.DocumentStatus);
        Assert.Single(createdTransaction.StockTransactionLines);
        Assert.Equal(6m, createdTransaction.StockTransactionLines.Single().Qty);
        Assert.Equal(10m, stockBalance.OnHandQty);
        Assert.Equal(6m, stockBalance.AllocatedQty);
        Assert.Contains("CREATE_ISSUE", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task PostIssueAsync_ShouldDeductOnHandAndConsumeReservation()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 3001, warehouseId: 1, lotCode: "LOT-POST", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 12));

        CreateIssueResult createResult = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 11m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.TransactionId);

        harness.CurrentUserContext.SetUser(
            userId: 3,
            username: "manager01",
            fullName: "Manager User",
            roleCodes: ["MANAGER"]);

        PostIssueResult postResult = await harness.Service.PostIssueAsync(
            createResult.TransactionId!.Value,
            actorUserId: 3,
            CancellationToken.None);

        Assert.True(postResult.IsSuccess);

        StockBalance stockBalance = await harness.DbContext.StockBalances
            .SingleAsync(balance =>
                balance.WarehouseId == 1 &&
                balance.ProductId == 1 &&
                balance.ProductLotId == 3001);

        ProductLot lot = await harness.DbContext.ProductLots.SingleAsync(productLot => productLot.ProductLotId == 3001);
        StockTransaction postedTransaction = await harness.DbContext.StockTransactions
            .SingleAsync(transaction => transaction.TransactionId == createResult.TransactionId!.Value);

        Assert.Equal(4m, stockBalance.OnHandQty);
        Assert.Equal(0m, stockBalance.AllocatedQty);
        Assert.Equal(4m, lot.RemainingQty);
        Assert.Equal("ACTIVE", lot.Status);
        Assert.Equal("POSTED", postedTransaction.DocumentStatus);
        Assert.Equal(3, postedTransaction.PostedByUserId);
        Assert.Contains("POST_ISSUE", harness.AuditLogService.ActionTypes);
    }

    [Fact]
    public async Task GetDraftIssuesAsync_ShouldReturnOnlyOwnDraftsForStaff()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 4001, warehouseId: 1, lotCode: "LOT-S1", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 5, expiryInDays: 20),
            (lotId: 4002, warehouseId: 2, lotCode: "LOT-S2", onHandQty: 10m, unitCost: 9m, receivedDaysAgo: 5, expiryInDays: 20));

        CreateIssueResult draftByStaff1 = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 4m, unitPrice: 10m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(draftByStaff1.IsSuccess);

        harness.CurrentUserContext.SetUser(2, "staff02", "Staff 02", ["WAREHOUSE_STAFF"]);

        CreateIssueResult draftByStaff2 = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 2, qty: 3m, unitPrice: 10m),
            actorUserId: 2,
            CancellationToken.None);
        Assert.True(draftByStaff2.IsSuccess);

        harness.CurrentUserContext.SetUser(1, "staff01", "Staff 01", ["WAREHOUSE_STAFF"]);

        IReadOnlyList<DraftIssueHeaderDto> drafts = await harness.Service.GetDraftIssuesAsync(
            actorUserId: 1,
            CancellationToken.None);

        Assert.Single(drafts);
        Assert.Equal(draftByStaff1.TransactionId, drafts[0].TransactionId);
    }

    [Fact]
    public async Task GetDraftIssuesAsync_ShouldReturnAllDraftsForManager()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 5001, warehouseId: 1, lotCode: "LOT-S1", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 5, expiryInDays: 20),
            (lotId: 5002, warehouseId: 2, lotCode: "LOT-S2", onHandQty: 10m, unitCost: 9m, receivedDaysAgo: 5, expiryInDays: 20));

        CreateIssueResult draftByStaff1 = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 4m, unitPrice: 10m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(draftByStaff1.IsSuccess);

        harness.CurrentUserContext.SetUser(2, "staff02", "Staff 02", ["WAREHOUSE_STAFF"]);
        CreateIssueResult draftByStaff2 = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 2, qty: 3m, unitPrice: 10m),
            actorUserId: 2,
            CancellationToken.None);
        Assert.True(draftByStaff2.IsSuccess);

        harness.CurrentUserContext.SetUser(3, "manager01", "Manager 01", ["MANAGER"]);

        IReadOnlyList<DraftIssueHeaderDto> drafts = await harness.Service.GetDraftIssuesAsync(
            actorUserId: 3,
            CancellationToken.None);

        Assert.Equal(2, drafts.Count);
        Assert.Contains(drafts, item => item.TransactionId == draftByStaff1.TransactionId);
        Assert.Contains(drafts, item => item.TransactionId == draftByStaff2.TransactionId);
    }

    [Fact]
    public async Task GetDraftIssueLinesAsync_ShouldThrowForStaffAccessingOthersDraft()
    {
        TestHarness harness = CreateHarness(userId: 2, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 6001, warehouseId: 2, lotCode: "LOT-S2", onHandQty: 10m, unitCost: 9m, receivedDaysAgo: 5, expiryInDays: 20));

        CreateIssueResult draftByStaff2 = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 2, qty: 3m, unitPrice: 10m),
            actorUserId: 2,
            CancellationToken.None);
        Assert.True(draftByStaff2.IsSuccess);

        harness.CurrentUserContext.SetUser(1, "staff01", "Staff 01", ["WAREHOUSE_STAFF"]);

        UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.Service.GetDraftIssueLinesAsync(
                draftByStaff2.TransactionId!.Value,
                actorUserId: 1,
                CancellationToken.None));

        Assert.Contains("Access denied.", exception.Message);
    }

    [Fact]
    public async Task PreviewLotAllocationAsync_ShouldFailWhenStaffWarehouseMismatch()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);

        PreviewIssueAllocationResult result = await harness.Service.PreviewLotAllocationAsync(
            BuildIssueRequest(warehouseId: 2, qty: 2m, unitPrice: 10m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Warehouse must match your assignment", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CreateIssueAsync_ShouldFailWhenStaffWarehouseMismatch()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);

        CreateIssueResult result = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 2, qty: 2m, unitPrice: 10m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Warehouse must match your assignment", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task GetActiveWarehousesAsync_ShouldReturnOnlyAssignedWarehouseForStaff()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);

        IReadOnlyList<WarehouseLookupDto> warehouses = await harness.Service.GetActiveWarehousesAsync(
            actorUserId: 1,
            CancellationToken.None);

        Assert.Single(warehouses);
        Assert.Equal(1, warehouses[0].WarehouseId);
    }

    [Fact]
    public async Task GetAvailableQtyAsync_ShouldThrowWhenStaffQueriesDifferentWarehouse()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);

        UnauthorizedAccessException exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            harness.Service.GetAvailableQtyAsync(
                warehouseId: 2,
                productId: 1,
                transactionDate: DateTime.UtcNow.Date,
                actorUserId: 1,
                CancellationToken.None));

        Assert.Contains("Warehouse must match your assignment", exception.Message);
    }

    [Fact]
    public async Task PostIssueAsync_ShouldFailWhenReservationInsufficient()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 7001, warehouseId: 1, lotCode: "LOT-RSV", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 20));

        CreateIssueResult createResult = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 11m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        StockBalance stockBalance = await harness.DbContext.StockBalances.SingleAsync(balance =>
            balance.WarehouseId == 1 && balance.ProductId == 1 && balance.ProductLotId == 7001);
        stockBalance.AllocatedQty = 5m;
        await harness.DbContext.SaveChangesAsync();

        harness.CurrentUserContext.SetUser(3, "manager01", "Manager User", ["MANAGER"]);

        PostIssueResult postResult = await harness.Service.PostIssueAsync(
            createResult.TransactionId!.Value,
            actorUserId: 3,
            CancellationToken.None);

        Assert.False(postResult.IsSuccess);
        Assert.Contains("Reservation", postResult.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task PostIssueAsync_ShouldFailWhenLotExpiredAtTransactionDate()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"]);
        SeedProductLots(
            harness.DbContext,
            (lotId: 8001, warehouseId: 1, lotCode: "LOT-EXP", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 20));

        DateTime transactionDate = DateTime.UtcNow.Date;
        CreateIssueResult createResult = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 11m, transactionDate: transactionDate),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        ProductLot lot = await harness.DbContext.ProductLots.SingleAsync(productLot => productLot.ProductLotId == 8001);
        lot.ExpiryDate = DateOnly.FromDateTime(transactionDate.AddDays(-1));
        await harness.DbContext.SaveChangesAsync();

        harness.CurrentUserContext.SetUser(3, "manager01", "Manager User", ["MANAGER"]);

        PostIssueResult postResult = await harness.Service.PostIssueAsync(
            createResult.TransactionId!.Value,
            actorUserId: 3,
            CancellationToken.None);

        Assert.False(postResult.IsSuccess);
        Assert.Contains("expired lot", postResult.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateIssueAsync_ShouldReturnConcurrencyMessageWhenConflict()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"], useConcurrencyDbContext: true);
        SeedProductLots(
            harness.DbContext,
            (lotId: 9001, warehouseId: 1, lotCode: "LOT-CONC", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 20));

        harness.RequireConcurrencyDbContext().ThrowConcurrencyOnNextSave = true;

        CreateIssueResult result = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 11m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Stock changed while reserving lots for this draft", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task PostIssueAsync_ShouldReturnConcurrencyMessageWhenConflict()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"], useConcurrencyDbContext: true);
        SeedProductLots(
            harness.DbContext,
            (lotId: 9101, warehouseId: 1, lotCode: "LOT-CONC", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 20));

        CreateIssueResult createResult = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 11m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        harness.CurrentUserContext.SetUser(3, "manager01", "Manager User", ["MANAGER"]);
        harness.RequireConcurrencyDbContext().ThrowConcurrencyOnNextSave = true;

        PostIssueResult result = await harness.Service.PostIssueAsync(
            createResult.TransactionId!.Value,
            actorUserId: 3,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Posting failed because data was modified by another user", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CancelDraftIssueAsync_ShouldReturnConcurrencyMessageWhenConflict()
    {
        TestHarness harness = CreateHarness(userId: 1, roleCodes: ["WAREHOUSE_STAFF"], useConcurrencyDbContext: true);
        SeedProductLots(
            harness.DbContext,
            (lotId: 9201, warehouseId: 1, lotCode: "LOT-CONC", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 20));

        CreateIssueResult createResult = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(warehouseId: 1, qty: 6m, unitPrice: 11m),
            actorUserId: 1,
            CancellationToken.None);
        Assert.True(createResult.IsSuccess);

        harness.CurrentUserContext.SetUser(3, "manager01", "Manager User", ["MANAGER"]);
        harness.RequireConcurrencyDbContext().ThrowConcurrencyOnNextSave = true;

        CancelIssueResult result = await harness.Service.CancelDraftIssueAsync(
            createResult.TransactionId!.Value,
            actorUserId: 3,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Cancellation failed because data was modified by another user", result.ErrorMessage ?? string.Empty);
    }

    private static TestHarness CreateHarness(
        int userId,
        IReadOnlyCollection<string> roleCodes,
        bool useConcurrencyDbContext = false)
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"IssueServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Sp26inventoryManagementDbContext dbContext = useConcurrencyDbContext
            ? new ThrowConcurrencyOnceDbContext(options)
            : new Sp26inventoryManagementDbContext(options);
        SeedReferenceData(dbContext);

        var currentUserContext = new CurrentUserContext();
        currentUserContext.SetUser(userId, $"user{userId}", $"User {userId}", roleCodes);

        var auditLogService = new RecordingAuditLogService();
        var sessionValidationService = new AlwaysValidSessionValidationService();
        var service = new IssueService(dbContext, sessionValidationService, currentUserContext, auditLogService);

        return new TestHarness(dbContext, service, currentUserContext, auditLogService);
    }

    private static void SeedReferenceData(Sp26inventoryManagementDbContext dbContext)
    {
        DateTime now = DateTime.UtcNow;

        dbContext.Categories.Add(new Category
        {
            CategoryId = 1,
            CategoryCode = "CAT01",
            CategoryName = "Default Category",
            IsActive = true,
            CreatedAt = now,
            RowVersion = [1]
        });

        dbContext.Products.Add(new Product
        {
            ProductId = 1,
            Sku = "SKU-01",
            ProductName = "Test Product",
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
                WarehouseName = "Main Warehouse",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new Warehouse
            {
                WarehouseId = 2,
                WarehouseCode = "WH02",
                WarehouseName = "Secondary Warehouse",
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
                FullName = "Staff User 01",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 2,
                Username = "staff02",
                PasswordHash = "x",
                FullName = "Staff User 02",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 3,
                Username = "manager01",
                PasswordHash = "x",
                FullName = "Manager User",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 4,
                Username = "admin01",
                PasswordHash = "x",
                FullName = "Admin User",
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

    private static void SeedProductLots(
        Sp26inventoryManagementDbContext dbContext,
        params (long lotId, int warehouseId, string lotCode, decimal onHandQty, decimal unitCost, int receivedDaysAgo, int? expiryInDays)[] lots)
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
                WarehouseId = lotSeed.warehouseId,
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
                WarehouseId = lotSeed.warehouseId,
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

    private static IssueRequestDto BuildIssueRequest(
        int warehouseId,
        decimal qty,
        decimal? unitPrice,
        DateTime? transactionDate = null)
    {
        return new IssueRequestDto
        {
            WarehouseId = warehouseId,
            TransactionDate = (transactionDate ?? DateTime.UtcNow.Date).Date,
            ReferenceNo = "REF-UNIT",
            Remarks = "Unit Test",
            Lines =
            [
                new IssueRequestLineDto
                {
                    ProductId = 1,
                    Qty = qty,
                    UnitPrice = unitPrice
                }
            ]
        };
    }

    private sealed class TestHarness(
        Sp26inventoryManagementDbContext dbContext,
        IssueService service,
        CurrentUserContext currentUserContext,
        RecordingAuditLogService auditLogService)
    {
        public Sp26inventoryManagementDbContext DbContext { get; } = dbContext;

        public IssueService Service { get; } = service;

        public CurrentUserContext CurrentUserContext { get; } = currentUserContext;

        public RecordingAuditLogService AuditLogService { get; } = auditLogService;

        public ThrowConcurrencyOnceDbContext RequireConcurrencyDbContext()
        {
            return Assert.IsType<ThrowConcurrencyOnceDbContext>(DbContext);
        }
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
