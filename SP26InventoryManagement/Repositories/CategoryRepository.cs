using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace SP26InventoryManagement.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly Sp26inventoryManagementDbContext _context;

        public CategoryRepository(Sp26inventoryManagementDbContext context)
        {
            _context = context;
        }

        public List<Category> GetAll()
        {
            return _context.Categories
                           .Include(c => c.ParentCategory) // Bây giờ lệnh này sẽ hết lỗi đỏ
                           .AsNoTracking()
                           .ToList();
        }

        public Category? GetById(int id)
        {
            return _context.Categories.Find(id);
        }

        public void Add(Category category)
        {
            _context.Categories.Add(category);
        }

        public void Update(Category category)
        {
            var existing = _context.Categories.Find(category.CategoryId);
            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(category);
            }
        }

        public void Delete(int id)
        {
            var existing = _context.Categories.Find(id);
            if (existing != null)
            {
                _context.Categories.Remove(existing);
            }
        }

        public void Save()
        {
            _context.SaveChanges();
        }
    }
}