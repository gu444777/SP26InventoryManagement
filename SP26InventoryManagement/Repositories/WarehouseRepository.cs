using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace SP26InventoryManagement.Repositories
{
    public class WarehouseRepository : IWarehouseRepository
    {
        private readonly Sp26inventoryManagementDbContext _context;

        public WarehouseRepository(Sp26inventoryManagementDbContext context)
        {
            _context = context;
        }

        public List<Warehouse> GetAll() => _context.Warehouses.ToList();

        public Warehouse? GetById(int id) => _context.Warehouses.Find(id);

        public void Add(Warehouse warehouse)
        {
            _context.Warehouses.Add(warehouse);
        }

        public void Update(Warehouse warehouse)
        {
            // 1. Tìm thực thể "chính chủ" đang nằm trong Database hoặc bộ nhớ Local
            var existingEntity = _context.Warehouses.Find(warehouse.WarehouseId);

            if (existingEntity != null)
            {
                // 2. Nếu tìm thấy, ghi đè các giá trị mới từ giao diện vào thực thể đó
                // Cách này tránh hoàn toàn lỗi Tracking và Concurrency
                _context.Entry(existingEntity).CurrentValues.SetValues(warehouse);
            }
            else
            {
                // 3. Trường hợp dự phòng nếu không tìm thấy (hiếm khi xảy ra)
                _context.Warehouses.Update(warehouse);
            }
        }

        public void Delete(Warehouse warehouse)
        {
            _context.Warehouses.Remove(warehouse);
        }

        public void SaveChanges()
        {
            _context.SaveChanges();
        }
    }
}