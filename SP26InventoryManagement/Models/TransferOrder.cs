using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class TransferOrder
{
    public long TransferOrderId { get; set; }

    public string TransferNo { get; set; } = null!;

    public int SourceWarehouseId { get; set; }

    public int DestinationWarehouseId { get; set; }

    public string TransferStatus { get; set; } = null!;

    public DateTime RequestDate { get; set; }

    public DateOnly? RequiredDate { get; set; }

    public string? Remarks { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? SourceConfirmedByUserId { get; set; }

    public DateTime? SourceConfirmedAt { get; set; }

    public int? DestinationConfirmedByUserId { get; set; }

    public DateTime? DestinationConfirmedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual User? DestinationConfirmedByUser { get; set; }

    public virtual Warehouse DestinationWarehouse { get; set; } = null!;

    public virtual User? SourceConfirmedByUser { get; set; }

    public virtual Warehouse SourceWarehouse { get; set; } = null!;

    public virtual ICollection<TransferOrderLine> TransferOrderLines { get; set; } = new List<TransferOrderLine>();
}
