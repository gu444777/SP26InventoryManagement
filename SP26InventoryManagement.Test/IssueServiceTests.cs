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
            (lotId: 1001, lotCode: "LOT-FEFO", onHandQty: 4m, unitCost: 10m, receivedDaysAgo: 10, expiryInDays: 5),
            (lotId: 1002, lotCode: "LOT-FIFO", onHandQty: 6m, unitCost: 12m, receivedDaysAgo: 8, expiryInDays: null));

        IssueRequestDto request = BuildIssueRequest(qty: 5m, unitPrice: 20m);

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
            (lotId: 2001, lotCode: "LOT-RESERVE", onHandQty: 10m, unitCost: 9m, receivedDaysAgo: 7, expiryInDays: 15));

        IssueRequestDto request = BuildIssueRequest(qty: 6m, unitPrice: 15m);

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
            (lotId: 3001, lotCode: "LOT-POST", onHandQty: 10m, unitCost: 8m, receivedDaysAgo: 6, expiryInDays: 12));

        CreateIssueResult createResult = await harness.Service.CreateIssueAsync(
            BuildIssueRequest(qty: 6m, unitPrice: 11m),
            actorUserId: 1,
            CancellationToken.None);

        Assert.True(createResult.IsSuccess);
        Assert.NotNull(createResult.TransactionId);

        harness.CurrentUserContext.SetUser(
            userId: 2,
            username: "manager01",
            fullName: "Manager User",
            roleCodes: ["MANAGER"]);

        PostIssueResult postResult = await harness.Service.PostIssueAsync(
            createResult.TransactionId!.Value,
            actorUserId: 2,
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
        Assert.Equal(2, postedTransaction.PostedByUserId);
        Assert.Contains("POST_ISSUE", harness.AuditLogService.ActionTypes);
    }

    private static TestHarness CreateHarness(int userId, IReadOnlyCollection<string> roleCodes)
    {
        DbContextOptions<Sp26inventoryManagementDbContext> options = new DbContextOptionsBuilder<Sp26inventoryManagementDbContext>()
            .UseInMemoryDatabase($"IssueServiceTests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new Sp26inventoryManagementDbContext(options);
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

        dbContext.Warehouses.Add(new Warehouse
        {
            WarehouseId = 1,
            WarehouseCode = "WH01",
            WarehouseName = "Main Warehouse",
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
                FullName = "Staff User",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            },
            new User
            {
                UserId = 2,
                Username = "manager01",
                PasswordHash = "x",
                FullName = "Manager User",
                IsActive = true,
                CreatedAt = now,
                RowVersion = [1]
            });

        dbContext.SaveChanges();
    }

    private static void SeedProductLots(
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

    private static IssueRequestDto BuildIssueRequest(decimal qty, decimal? unitPrice)
    {
        return new IssueRequestDto
        {
            WarehouseId = 1,
            TransactionDate = DateTime.UtcNow.Date,
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
}
