using System.Collections.Generic;
using System.Linq;
using SP26InventoryManagement.Models;


public class SupplierService : ISupplierService
{
  

    private readonly Sp26inventoryManagementDbContext _context;

    public SupplierService(Sp26inventoryManagementDbContext context)
    {
        _context = context;
    }

    public List<Supplier> GetAll()
    {
        return _context.Suppliers.ToList();
    }

    public void Add(Supplier supplier)
    {
        supplier.SupplierCode = GenerateSupplierCode();
        supplier.CreatedAt = DateTime.Now;   // 🔥 thêm
        supplier.UpdatedAt = null;

        _context.Suppliers.Add(supplier);
        _context.SaveChanges();
    }
    public void Update(Supplier supplier)
    {
        var existing = _context.Suppliers.Find(supplier.SupplierId);

        if (existing != null)
        {
            existing.SupplierName = supplier.SupplierName;
            existing.PhoneNumber = supplier.PhoneNumber;
            existing.Email = supplier.Email;
            existing.AddressLine = supplier.AddressLine;

            existing.UpdatedAt = DateTime.Now; // 🔥 quan trọng

            _context.SaveChanges();
        }
    }

    public void Delete(int id)
    {
        var supplier = _context.Suppliers.FirstOrDefault(x => x.SupplierId == id);
        if (supplier != null)
        {
            _context.Suppliers.Remove(supplier);
            _context.SaveChanges();
        }
    }
    public string GenerateSupplierCode()
    {
        var lastSupplier = _context.Suppliers
            .OrderByDescending(s => s.SupplierId)
            .FirstOrDefault();

        if (lastSupplier == null)
            return "SUP-001";

        var lastCode = lastSupplier.SupplierCode;

        int number = 1;

        if (!string.IsNullOrEmpty(lastCode) && lastCode.StartsWith("SUP-"))
        {
            var numPart = lastCode.Substring(4);

            if (int.TryParse(numPart, out int n))
            {
                number = n + 1;
            }
        }

        return $"SUP-{number:D3}";
    }
}