namespace SP26InventoryManagement.DTOs;

public class StockLedgerEntryDto
{
    public long TransactionId { get; init; }
    public string TransactionNo { get; init; } = string.Empty;
    public string TransactionType { get; init; } = string.Empty;
    public string DocumentStatus { get; init; } = string.Empty;
    public int LineNo { get; init; }
    public DateTime TransactionDate { get; init; }
    public DateTime? PostedAt { get; init; }
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
    public decimal? UnitPrice { get; init; }
    public decimal QtyIn { get; init; }
    public decimal QtyOut { get; init; }
    public decimal SignedQty { get; init; }
    public decimal? LineAmount { get; init; }
    public decimal? CogsAmount { get; init; }
    public string ReferenceType { get; init; } = string.Empty;
    public string ReferenceNo { get; init; } = string.Empty;
    public string CounterpartyName { get; init; } = string.Empty;
    public string Remarks { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public string PostedBy { get; init; } = string.Empty;

    public string WarehouseDisplay => $"{WarehouseCode} - {WarehouseName}";
    public string ProductDisplay => $"{Sku} - {ProductName}";

    public string MovementDisplay => string.Equals(TransactionType, "RECEIPT", StringComparison.OrdinalIgnoreCase)
        ? "Inbound"
        : "Outbound";

    public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";
}
