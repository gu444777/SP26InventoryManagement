using Microsoft.EntityFrameworkCore;

namespace SP26InventoryManagement.Models;

public partial class Sp26inventoryManagementDbContext
{
    public virtual DbSet<UserWarehouseAssignment> UserWarehouseAssignments { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserWarehouseAssignment>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("UserWarehouseAssignments", "inv");

            entity.HasIndex(e => e.WarehouseId, "IX_UserWarehouseAssignments_Warehouse");

            entity.Property(e => e.AssignedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.AssignedByUser)
                .WithMany()
                .HasForeignKey(d => d.AssignedByUserId)
                .HasConstraintName("FK_UserWarehouseAssignments_AssignedByUser");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserWarehouseAssignments_Users");

            entity.HasOne(d => d.Warehouse)
                .WithMany()
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_UserWarehouseAssignments_Warehouses");
        });
    }
}
