using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class ProductLot
{
    public long ProductLotId { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    public string LotCode { get; set; } = null!;

    public DateOnly ReceivedDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public decimal UnitCost { get; set; }

    public decimal InitialQty { get; set; }

    public decimal RemainingQty { get; set; }

    public int? SupplierId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();

    public virtual ICollection<StockTransactionLine> StockTransactionLines { get; set; } = new List<StockTransactionLine>();

    public virtual Supplier? Supplier { get; set; }

    public virtual ICollection<TransferLotAllocation> TransferLotAllocationDestinationProductLots { get; set; } = new List<TransferLotAllocation>();

    public virtual ICollection<TransferLotAllocation> TransferLotAllocationSourceProductLots { get; set; } = new List<TransferLotAllocation>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}
