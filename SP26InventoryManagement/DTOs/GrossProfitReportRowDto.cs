namespace SP26InventoryManagement.DTOs;

public class GrossProfitReportRowDto
{
    public long TransactionId { get; init; }
    public string TransactionNo { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public DateTime? PostedAt { get; init; }
    public int WarehouseId { get; init; }
    public string WarehouseCode { get; init; } = string.Empty;
    public string WarehouseName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public int LineNo { get; init; }
    public int ProductId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public long ProductLotId { get; init; }
    public string LotCode { get; init; } = string.Empty;
    public decimal Qty { get; init; }
    public decimal UnitCost { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal SalesAmount { get; init; }
    public decimal CogsAmount { get; init; }
    public decimal GrossProfitAmount { get; init; }
    public decimal GrossMarginPct { get; init; }

    public string WarehouseDisplay => $"{WarehouseCode} - {WarehouseName}";
    public string ProductDisplay => $"{Sku} - {ProductName}";
}
