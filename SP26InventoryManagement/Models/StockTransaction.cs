using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class StockTransaction
{
    public long TransactionId { get; set; }

    public string TransactionNo { get; set; } = null!;

    public string TransactionType { get; set; } = null!;

    public string DocumentStatus { get; set; } = null!;

    public int WarehouseId { get; set; }

    public DateTime TransactionDate { get; set; }

    public int? SupplierId { get; set; }

    public int? CustomerId { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceNo { get; set; }

    public string? AdjustmentReason { get; set; }

    public string? Remarks { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? PostedByUserId { get; set; }

    public DateTime? PostedAt { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Customer? Customer { get; set; }

    public virtual User? PostedByUser { get; set; }

    public virtual ICollection<StockTransactionLine> StockTransactionLines { get; set; } = new List<StockTransactionLine>();

    public virtual Supplier? Supplier { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
