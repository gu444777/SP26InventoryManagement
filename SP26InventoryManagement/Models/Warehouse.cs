using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class Warehouse
{
    public int WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string WarehouseName { get; set; } = null!;

    public string? AddressLine { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<ProductLot> ProductLots { get; set; } = new List<ProductLot>();

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual ICollection<TransferOrder> TransferOrderDestinationWarehouses { get; set; } = new List<TransferOrder>();

    public virtual ICollection<TransferOrder> TransferOrderSourceWarehouses { get; set; } = new List<TransferOrder>();
}
