using System.Collections.Generic;
using SP26InventoryManagement.Models;

public interface ISupplierService
{
    List<Supplier> GetAll();
    void Add(Supplier supplier);
    void Update(Supplier supplier);
    void Delete(int id);
}