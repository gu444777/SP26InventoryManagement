using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class UserRole
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }

    public int? AssignedByUserId { get; set; }

    public virtual User? AssignedByUser { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
