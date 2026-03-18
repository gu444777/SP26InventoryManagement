namespace SP26InventoryManagement.DTOs;

public class WarehouseLookupDto
{
    public int WarehouseId { get; init; }

    public string WarehouseCode { get; init; } = string.Empty;

    public string WarehouseName { get; init; } = string.Empty;

    public string Display => $"{WarehouseCode} - {WarehouseName}";
}
