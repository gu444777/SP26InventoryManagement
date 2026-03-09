using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<User> InverseCreatedByUser { get; set; } = new List<User>();

    public virtual ICollection<StockTransaction> StockTransactionCreatedByUsers { get; set; } = new List<StockTransaction>();

    public virtual ICollection<StockTransaction> StockTransactionPostedByUsers { get; set; } = new List<StockTransaction>();

    public virtual ICollection<TransferOrder> TransferOrderCreatedByUsers { get; set; } = new List<TransferOrder>();

    public virtual ICollection<TransferOrder> TransferOrderDestinationConfirmedByUsers { get; set; } = new List<TransferOrder>();

    public virtual ICollection<TransferOrder> TransferOrderSourceConfirmedByUsers { get; set; } = new List<TransferOrder>();

    public virtual ICollection<UserRole> UserRoleAssignedByUsers { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRole> UserRoleUsers { get; set; } = new List<UserRole>();
}
