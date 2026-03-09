using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class StockBalance
{
    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    public long ProductLotId { get; set; }

    public decimal OnHandQty { get; set; }

    public decimal AllocatedQty { get; set; }

    public decimal? AvailableQty { get; set; }

    public DateTime? LastMovementAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductLot ProductLot { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
