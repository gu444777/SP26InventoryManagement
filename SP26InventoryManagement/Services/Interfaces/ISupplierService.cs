using System.Collections.Generic;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

public interface ISupplierService
{
    List<Supplier> GetAll();
    OperationResult Add(Supplier supplier);
    OperationResult Update(Supplier supplier);
    OperationResult Delete(int id);
}
