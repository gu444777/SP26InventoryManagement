using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SP26InventoryManagement.Models;

public partial class Sp26inventoryManagementDbContext : DbContext
{
    public Sp26inventoryManagementDbContext()
    {
    }

    public Sp26inventoryManagementDbContext(DbContextOptions<Sp26inventoryManagementDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductLot> ProductLots { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<StockBalance> StockBalances { get; set; }

    public virtual DbSet<StockTransaction> StockTransactions { get; set; }

    public virtual DbSet<StockTransactionLine> StockTransactionLines { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TransferLotAllocation> TransferLotAllocations { get; set; }

    public virtual DbSet<TransferOrder> TransferOrders { get; set; }

    public virtual DbSet<TransferOrderLine> TransferOrderLines { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<VwCurrentStockSnapshot> VwCurrentStockSnapshots { get; set; }

    public virtual DbSet<VwExpiryAlert> VwExpiryAlerts { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs", "inv");

            entity.HasIndex(e => new { e.EntityName, e.EntityId, e.OccurredAt }, "IX_AuditLogs_Entity").IsDescending(false, false, true);

            entity.HasIndex(e => e.OccurredAt, "IX_AuditLogs_OccurredAt").IsDescending();

            entity.Property(e => e.ActionType).HasMaxLength(100);
            entity.Property(e => e.ClientApp).HasMaxLength(120);
            entity.Property(e => e.ClientIp).HasMaxLength(45);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.IsSuccess).HasDefaultValue(true);
            entity.Property(e => e.OccurredAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Severity)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("INFO");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_Users");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.WarehouseId)
                .HasConstraintName("FK_AuditLogs_Warehouses");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories", "inv");

            entity.HasIndex(e => e.CategoryCode, "UQ_Categories_CategoryCode").IsUnique();

            entity.Property(e => e.CategoryCode).HasMaxLength(30);
            entity.Property(e => e.CategoryName).HasMaxLength(150);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("FK_Categories_Parent");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers", "inv");

            entity.HasIndex(e => e.CustomerCode, "UQ_Customers_CustomerCode").IsUnique();

            entity.Property(e => e.AddressLine).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerCode).HasMaxLength(30);
            entity.Property(e => e.CustomerName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", "inv");

            entity.HasIndex(e => e.Sku, "UQ_Products_SKU").IsUnique();

            entity.Property(e => e.BaseUom)
                .HasMaxLength(20)
                .HasDefaultValue("PCS");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Sku)
                .HasMaxLength(50)
                .HasColumnName("SKU");
            entity.Property(e => e.TrackExpiry).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Categories");
        });

        modelBuilder.Entity<ProductLot>(entity =>
        {
            entity.ToTable("ProductLots", "inv");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId, e.ExpiryDate, e.ReceivedDate, e.ProductLotId }, "IX_ProductLots_Allocation").HasFilter("([RemainingQty]>(0))");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId, e.LotCode }, "UQ_ProductLots_Warehouse_Product_LotCode").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.InitialQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.LotCode).HasMaxLength(80);
            entity.Property(e => e.RemainingQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductLots)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductLots_Products");

            entity.HasOne(d => d.Supplier).WithMany(p => p.ProductLots)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_ProductLots_Suppliers");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.ProductLots)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductLots_Warehouses");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles", "inv");

            entity.HasIndex(e => e.RoleCode, "UQ_Roles_RoleCode").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(250);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsSystemRole).HasDefaultValue(true);
            entity.Property(e => e.RoleCode).HasMaxLength(40);
            entity.Property(e => e.RoleName).HasMaxLength(100);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.HasKey(e => new { e.WarehouseId, e.ProductId, e.ProductLotId });

            entity.ToTable("StockBalances", "inv");

            entity.HasIndex(e => new { e.ProductId, e.WarehouseId }, "IX_StockBalances_ProductLookup");

            entity.Property(e => e.AllocatedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.AvailableQty)
                .HasComputedColumnSql("(CONVERT([decimal](18,3),[OnHandQty]-[AllocatedQty]))", true)
                .HasColumnType("decimal(18, 3)");
            entity.Property(e => e.LastMovementAt).HasPrecision(0);
            entity.Property(e => e.OnHandQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Product).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockBalances_Products");

            entity.HasOne(d => d.ProductLot).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.ProductLotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockBalances_ProductLots");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockBalances_Warehouses");
        });

        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId);

            entity.ToTable("StockTransactions", "inv");

            entity.HasIndex(e => new { e.TransactionType, e.DocumentStatus, e.TransactionDate }, "IX_StockTransactions_Type_Status_Date").IsDescending(false, false, true);

            entity.HasIndex(e => new { e.WarehouseId, e.TransactionDate }, "IX_StockTransactions_Warehouse_Date").IsDescending(false, true);

            entity.HasIndex(e => e.TransactionNo, "UQ_StockTransactions_TransactionNo").IsUnique();

            entity.Property(e => e.AdjustmentReason).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DocumentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.PostedAt).HasPrecision(0);
            entity.Property(e => e.ReferenceNo).HasMaxLength(50);
            entity.Property(e => e.ReferenceType).HasMaxLength(40);
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransactionDate).HasPrecision(0);
            entity.Property(e => e.TransactionNo).HasMaxLength(30);
            entity.Property(e => e.TransactionType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.StockTransactionCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransactions_CreatedByUser");

            entity.HasOne(d => d.Customer).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_StockTransactions_Customers");

            entity.HasOne(d => d.PostedByUser).WithMany(p => p.StockTransactionPostedByUsers)
                .HasForeignKey(d => d.PostedByUserId)
                .HasConstraintName("FK_StockTransactions_PostedByUser");

            entity.HasOne(d => d.Supplier).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_StockTransactions_Suppliers");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockTransactions)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransactions_Warehouses");
        });

        modelBuilder.Entity<StockTransactionLine>(entity =>
        {
            entity.ToTable("StockTransactionLines", "inv", tb => tb.HasTrigger("TR_StockTransactionLines_ValidateLotRules"));

            entity.HasIndex(e => e.ProductId, "IX_StockTransactionLines_Product");

            entity.HasIndex(e => e.ProductLotId, "IX_StockTransactionLines_ProductLot");

            entity.HasIndex(e => new { e.TransactionId, e.LineNo }, "UQ_StockTransactionLines_Transaction_LineNo").IsUnique();

            entity.Property(e => e.CogsAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LineAmount)
                .HasComputedColumnSql("(CONVERT([decimal](18,2),round([Qty]*coalesce([UnitPrice],[UnitCost]),(2))))", true)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Notes).HasMaxLength(300);
            entity.Property(e => e.Qty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 4)");

            entity.HasOne(d => d.Product).WithMany(p => p.StockTransactionLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransactionLines_Products");

            entity.HasOne(d => d.ProductLot).WithMany(p => p.StockTransactionLines)
                .HasForeignKey(d => d.ProductLotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransactionLines_ProductLots");

            entity.HasOne(d => d.Transaction).WithMany(p => p.StockTransactionLines)
                .HasForeignKey(d => d.TransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransactionLines_StockTransactions");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("Suppliers", "inv");

            entity.HasIndex(e => e.SupplierCode, "UQ_Suppliers_SupplierCode").IsUnique();

            entity.Property(e => e.AddressLine).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SupplierCode).HasMaxLength(30);
            entity.Property(e => e.SupplierName).HasMaxLength(200);
            entity.Property(e => e.TaxCode).HasMaxLength(30);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<TransferLotAllocation>(entity =>
        {
            entity.ToTable("TransferLotAllocations", "inv");

            entity.HasIndex(e => new { e.TransferOrderLineId, e.SourceProductLotId }, "UQ_TransferLotAllocations_Line_SourceLot").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DispatchedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.LotCodeSnapshot).HasMaxLength(80);
            entity.Property(e => e.ReceivedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 4)");

            entity.HasOne(d => d.DestinationProductLot).WithMany(p => p.TransferLotAllocationDestinationProductLots)
                .HasForeignKey(d => d.DestinationProductLotId)
                .HasConstraintName("FK_TransferLotAllocations_DestinationProductLots");

            entity.HasOne(d => d.SourceProductLot).WithMany(p => p.TransferLotAllocationSourceProductLots)
                .HasForeignKey(d => d.SourceProductLotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLotAllocations_SourceProductLots");

            entity.HasOne(d => d.TransferOrderLine).WithMany(p => p.TransferLotAllocations)
                .HasForeignKey(d => d.TransferOrderLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferLotAllocations_TransferOrderLines");
        });

        modelBuilder.Entity<TransferOrder>(entity =>
        {
            entity.ToTable("TransferOrders", "inv");

            entity.HasIndex(e => new { e.TransferStatus, e.RequestDate }, "IX_TransferOrders_Status_RequestDate").IsDescending(false, true);

            entity.HasIndex(e => e.TransferNo, "UQ_TransferOrders_TransferNo").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DestinationConfirmedAt).HasPrecision(0);
            entity.Property(e => e.Remarks).HasMaxLength(500);
            entity.Property(e => e.RequestDate)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.SourceConfirmedAt).HasPrecision(0);
            entity.Property(e => e.TransferNo).HasMaxLength(30);
            entity.Property(e => e.TransferStatus)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("CREATED");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.TransferOrderCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrders_CreatedByUser");

            entity.HasOne(d => d.DestinationConfirmedByUser).WithMany(p => p.TransferOrderDestinationConfirmedByUsers)
                .HasForeignKey(d => d.DestinationConfirmedByUserId)
                .HasConstraintName("FK_TransferOrders_DestinationConfirmedByUser");

            entity.HasOne(d => d.DestinationWarehouse).WithMany(p => p.TransferOrderDestinationWarehouses)
                .HasForeignKey(d => d.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrders_DestinationWarehouse");

            entity.HasOne(d => d.SourceConfirmedByUser).WithMany(p => p.TransferOrderSourceConfirmedByUsers)
                .HasForeignKey(d => d.SourceConfirmedByUserId)
                .HasConstraintName("FK_TransferOrders_SourceConfirmedByUser");

            entity.HasOne(d => d.SourceWarehouse).WithMany(p => p.TransferOrderSourceWarehouses)
                .HasForeignKey(d => d.SourceWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrders_SourceWarehouse");
        });

        modelBuilder.Entity<TransferOrderLine>(entity =>
        {
            entity.ToTable("TransferOrderLines", "inv");

            entity.HasIndex(e => new { e.ProductId, e.TransferOrderId }, "IX_TransferOrderLines_Product");

            entity.HasIndex(e => new { e.TransferOrderId, e.LineNo }, "UQ_TransferOrderLines_TransferOrder_LineNo").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DispatchedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Notes).HasMaxLength(300);
            entity.Property(e => e.ReceivedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RequestedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Product).WithMany(p => p.TransferOrderLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrderLines_Products");

            entity.HasOne(d => d.TransferOrder).WithMany(p => p.TransferOrderLines)
                .HasForeignKey(d => d.TransferOrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TransferOrderLines_TransferOrders");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "inv");

            entity.HasIndex(e => e.Username, "UQ_Users_Username").IsUnique();

            entity.HasIndex(e => e.Email, "UX_Users_Email_NotNull")
                .IsUnique()
                .HasFilter("([Email] IS NOT NULL)");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FullName).HasMaxLength(150);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginAt).HasPrecision(0);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(30);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.InverseCreatedByUser)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_Users_CreatedByUser");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.ToTable("UserRoles", "inv");

            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.UserRoleAssignedByUsers)
                .HasForeignKey(d => d.AssignedByUserId)
                .HasConstraintName("FK_UserRoles_AssignedByUser");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Users");
        });

        modelBuilder.Entity<VwCurrentStockSnapshot>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_CurrentStockSnapshot", "inv");

            entity.Property(e => e.AllocatedQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.AvailableQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.LastMovementAt).HasPrecision(0);
            entity.Property(e => e.LotCode).HasMaxLength(80);
            entity.Property(e => e.OnHandQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.Sku)
                .HasMaxLength(50)
                .HasColumnName("SKU");
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18, 4)");
            entity.Property(e => e.WarehouseCode).HasMaxLength(30);
            entity.Property(e => e.WarehouseName).HasMaxLength(150);
        });

        modelBuilder.Entity<VwExpiryAlert>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ExpiryAlerts", "inv");

            entity.Property(e => e.ExpiryStatus).HasMaxLength(13);
            entity.Property(e => e.LotCode).HasMaxLength(80);
            entity.Property(e => e.ProductName).HasMaxLength(200);
            entity.Property(e => e.RemainingQty).HasColumnType("decimal(18, 3)");
            entity.Property(e => e.Sku)
                .HasMaxLength(50)
                .HasColumnName("SKU");
            entity.Property(e => e.WarehouseCode).HasMaxLength(30);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("Warehouses", "inv");

            entity.HasIndex(e => e.WarehouseCode, "UQ_Warehouses_WarehouseCode").IsUnique();

            entity.Property(e => e.AddressLine).HasMaxLength(300);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.WarehouseCode).HasMaxLength(30);
            entity.Property(e => e.WarehouseName).HasMaxLength(150);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
