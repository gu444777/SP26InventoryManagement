using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class VwExpiryAlert
{
    public int WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public int ProductId { get; set; }

    public string Sku { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public long ProductLotId { get; set; }

    public string LotCode { get; set; } = null!;

    public DateOnly? ExpiryDate { get; set; }

    public decimal RemainingQty { get; set; }

    public string ExpiryStatus { get; set; } = null!;

    public int? DaysToExpiry { get; set; }
}
