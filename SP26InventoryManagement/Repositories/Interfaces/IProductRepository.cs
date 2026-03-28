using SP26InventoryManagement.Models;
using System.Collections.Generic;

namespace SP26InventoryManagement.Repositories.Interfaces
{
    public interface IProductRepository
    {
        List<Product> GetAll();
        Product? GetById(int id);
        void Add(Product product);
        void Update(Product product);
        void Delete(Product product);
        void SaveChanges();

        // THÊM DÒNG NÀY ĐỂ FIX LỖI ĐỎ
        List<Category> GetAllCategories();
    }

}