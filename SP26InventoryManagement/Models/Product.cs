using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public string Sku { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public int CategoryId { get; set; }

    public string BaseUom { get; set; } = null!;

    public bool TrackExpiry { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<ProductLot> ProductLots { get; set; } = new List<ProductLot>();

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();

    public virtual ICollection<StockTransactionLine> StockTransactionLines { get; set; } = new List<StockTransactionLine>();

    public virtual ICollection<TransferOrderLine> TransferOrderLines { get; set; } = new List<TransferOrderLine>();
}
