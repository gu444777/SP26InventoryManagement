using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public int? ParentCategoryId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<Category> InverseParentCategory { get; set; } = new List<Category>();

    public virtual Category? ParentCategory { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
