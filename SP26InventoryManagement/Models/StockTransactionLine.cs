using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class StockTransactionLine
{
    public long StockTransactionLineId { get; set; }

    public long TransactionId { get; set; }

    public int LineNo { get; set; }

    public int ProductId { get; set; }

    public long ProductLotId { get; set; }

    public decimal Qty { get; set; }

    public decimal UnitCost { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal? LineAmount { get; set; }

    public decimal? CogsAmount { get; set; }

    public DateOnly ReceivedDateSnapshot { get; set; }

    public DateOnly? ExpiryDateSnapshot { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductLot ProductLot { get; set; } = null!;

    public virtual StockTransaction Transaction { get; set; } = null!;
}
