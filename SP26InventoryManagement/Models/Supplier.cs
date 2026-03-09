using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class Supplier
{
    public int SupplierId { get; set; }

    public string SupplierCode { get; set; } = null!;

    public string SupplierName { get; set; } = null!;

    public string? TaxCode { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? AddressLine { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<ProductLot> ProductLots { get; set; } = new List<ProductLot>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
}
