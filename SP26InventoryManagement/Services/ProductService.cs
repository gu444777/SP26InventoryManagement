using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories.Interfaces;
using System;
using System.Collections.Generic;

namespace SP26InventoryManagement.Services
{
    public class ProductService
{
    private readonly IProductRepository _repo;
    public ProductService(IProductRepository repo) => _repo = repo;

    public List<Product>  GetAllProducts() => _repo.GetAll();

    public void SaveProduct(Product p)
    {
        if (p.ProductId == 0)
        {
            p.CreatedAt = DateTime.Now;
            p.IsActive = true;
            // BẮT BUỘC: Gán giá trị cho các trường null! trong Model để không lỗi DB
            if (string.IsNullOrEmpty(p.Sku)) p.Sku = "SKU-TEMP";
            if (string.IsNullOrEmpty(p.BaseUom)) p.BaseUom = "PCS";

            _repo.Add(p);
        }
        else
        {
            p.UpdatedAt = DateTime.Now;
            _repo.Update(p);
        }
        _repo.SaveChanges();
    }

    public void DeleteProduct(int id)
    {
        var p = _repo.GetById(id);
        if (p !=  null)
            {
            _repo.Delete(p);
            _repo.SaveChanges();
        }
    }

    public List<Category>  GetAllCategories() => _repo.GetAllCategories();
}
}