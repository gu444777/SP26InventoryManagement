namespace SP26InventoryManagement.DTOs;

public class StockSnapshotDto
{
    public int WarehouseId { get; init; }

    public string WarehouseCode { get; init; } = string.Empty;

    public string WarehouseName { get; init; } = string.Empty;

    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long ProductLotId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public decimal UnitCost { get; init; }

    public decimal OnHandQty { get; init; }

    public decimal AllocatedQty { get; init; }

    public decimal AvailableQty { get; init; }

    public DateTime? LastMovementAt { get; init; }

    public string WarehouseDisplay => $"{WarehouseCode} - {WarehouseName}";

    public string ProductDisplay => $"{Sku} - {ProductName}";

    public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";
}
