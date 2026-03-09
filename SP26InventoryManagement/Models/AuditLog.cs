using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public DateTime OccurredAt { get; set; }

    public int? UserId { get; set; }

    public string ActionType { get; set; } = null!;

    public string EntityName { get; set; } = null!;

    public string? EntityId { get; set; }

    public int? WarehouseId { get; set; }

    public bool IsSuccess { get; set; }

    public string Severity { get; set; } = null!;

    public string? DetailsJson { get; set; }

    public string? ClientIp { get; set; }

    public string? ClientApp { get; set; }

    public virtual User? User { get; set; }

    public virtual Warehouse? Warehouse { get; set; }
}
