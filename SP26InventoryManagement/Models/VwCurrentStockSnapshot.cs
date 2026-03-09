using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class VwCurrentStockSnapshot
{
    public int WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string WarehouseName { get; set; } = null!;

    public int ProductId { get; set; }

    public string Sku { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public long ProductLotId { get; set; }

    public string LotCode { get; set; } = null!;

    public DateOnly ReceivedDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public decimal UnitCost { get; set; }

    public decimal OnHandQty { get; set; }

    public decimal AllocatedQty { get; set; }

    public decimal? AvailableQty { get; set; }

    public DateTime? LastMovementAt { get; set; }
}
