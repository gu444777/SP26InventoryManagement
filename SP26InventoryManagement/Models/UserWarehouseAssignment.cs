using System;

namespace SP26InventoryManagement.Models;

public partial class UserWarehouseAssignment
{
    public int UserId { get; set; }

    public int WarehouseId { get; set; }

    public DateTime AssignedAt { get; set; }

    public int? AssignedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;

    public virtual User? AssignedByUser { get; set; }
}
