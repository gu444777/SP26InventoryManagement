using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace SP26InventoryManagement.Repositories
{
    public class ProductRepository : IProductRepository
{
    private readonly Sp26inventoryManagementDbContext _context;

    public ProductRepository(Sp26inventoryManagementDbContext context)
    {
        _context = context;
    }

    public List<Product> GetAll() => _context.Products.Include(p => p.Category).AsNoTracking().ToList();

    public Product? GetById(int id) => _context.Products.Find(id);

    public void Add(Product product)
    {
        product.Category = null!;
        _context.Products.Add(product);
    }

    public void Update(Product product)
    {
        var existing = _context.Products.Find(product.ProductId);
        if (existing != null)
        {
            // SetValues sẽ cập nhật các trường từ View mà không làm hỏng RowVersion của DB
            _context.Entry(existing).CurrentValues.SetValues(product);
        }
    }

    public void Delete(Product product)
    {
        var existing = _context.Products.Find(product.ProductId);
        if (existing != null)
        {
            _context.Products.Remove(existing);
        }
    }

    public List<Category> GetAllCategories() => _context.Categories.AsNoTracking().ToList();

    public void SaveChanges()
    {
        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateException)
        {
            // Lỗi này xảy ra khi Xóa sản phẩm đang nằm trong các bảng Stock hoặc Transaction
            throw new System.Exception("Sản phẩm đã có dữ liệu kho hoặc giao dịch liên quan. Không thể xóa!");
        }
    }
}
}