using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SP26InventoryManagement.Services
{
    public class WarehouseService
    {
        private readonly IWarehouseRepository _repo;

        public WarehouseService(IWarehouseRepository repo)
        {
            _repo = repo;
        }

        public List<Warehouse> GetAll()
        {
            return _repo.GetAll().ToList();
        }

        public void Save(Warehouse w)
        {
            if (string.IsNullOrWhiteSpace(w.WarehouseCode))
                throw new Exception("Mã kho không được để trống!");
            if (string.IsNullOrWhiteSpace(w.WarehouseName))
                throw new Exception("Tên kho không được để trống!");

            if (w.WarehouseId == 0)
            {
                // Logic Thêm mới
                w.CreatedAt = DateTime.Now;
                w.IsActive = true;
                _repo.Add(w);
            }
            else
            {
                // Logic Cập nhật
                w.UpdatedAt = DateTime.Now;
                _repo.Update(w);
            }

            _repo.SaveChanges();
        }

        public void ToggleStatus(int id)
        {
            var w = _repo.GetById(id);
            if (w != null)
            {
                w.IsActive = !w.IsActive;
                w.UpdatedAt = DateTime.Now;
                _repo.Update(w);
                _repo.SaveChanges();
            }
        }

        public void Delete(int id)
        {
            var w = _repo.GetById(id);
            if (w != null)
            {
                _repo.Delete(w);
                _repo.SaveChanges();
            }
        }
    }
}