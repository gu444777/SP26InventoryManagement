using SP26InventoryManagement.Models;
using System.Collections.Generic;

namespace SP26InventoryManagement.Repositories.Interfaces
{
    public interface IWarehouseRepository
    {
        List<Warehouse> GetAll();

        Warehouse? GetById(int id);

        void Add(Warehouse warehouse);

        void Update(Warehouse warehouse);

        // MỚI: Khai báo phương thức xóa để xóa bản ghi khỏi DbSet
        void Delete(Warehouse warehouse);

        // Khai báo phương thức lưu thay đổi thực tế xuống SQL Server
        void SaveChanges();
    }
}