using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class TransferLotAllocation
{
    public long TransferLotAllocationId { get; set; }

    public long TransferOrderLineId { get; set; }

    public long SourceProductLotId { get; set; }

    public long? DestinationProductLotId { get; set; }

    public string LotCodeSnapshot { get; set; } = null!;

    public DateOnly ReceivedDateSnapshot { get; set; }

    public DateOnly? ExpiryDateSnapshot { get; set; }

    public decimal UnitCost { get; set; }

    public decimal DispatchedQty { get; set; }

    public decimal ReceivedQty { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ProductLot? DestinationProductLot { get; set; }

    public virtual ProductLot SourceProductLot { get; set; } = null!;

    public virtual TransferOrderLine TransferOrderLine { get; set; } = null!;
}
