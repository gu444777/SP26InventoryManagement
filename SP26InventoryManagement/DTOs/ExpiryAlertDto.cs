namespace SP26InventoryManagement.DTOs;

public class ExpiryAlertDto
{
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public int ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public long ProductLotId { get; init; }
    public string LotCode { get; init; } = string.Empty;
    public DateOnly? ExpiryDate { get; init; }
    public decimal RemainingQty { get; init; }
    public string ExpiryStatus { get; init; } = string.Empty;
    public int? DaysToExpiry { get; init; }

    public string WarehouseDisplay => WarehouseCode;

    public string ProductDisplay => $"{Sku} - {ProductName}";

    public string ExpiryDateDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";

    public string DaysToExpiryDisplay => DaysToExpiry.HasValue ? DaysToExpiry.Value.ToString() : "-";
}
