using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class TransferOrderLine
{
    public long TransferOrderLineId { get; set; }

    public long TransferOrderId { get; set; }

    public int LineNo { get; set; }

    public int ProductId { get; set; }

    public decimal RequestedQty { get; set; }

    public decimal DispatchedQty { get; set; }

    public decimal ReceivedQty { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<TransferLotAllocation> TransferLotAllocations { get; set; } = new List<TransferLotAllocation>();

    public virtual TransferOrder TransferOrder { get; set; } = null!;
}
