using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Services.Interfaces;

public interface ICustomerService
{
    List<Customer> GetAll();
    OperationResult Add(Customer customer);
    OperationResult Update(Customer customer);
    OperationResult Delete(int id);
}
