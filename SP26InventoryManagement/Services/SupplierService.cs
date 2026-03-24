using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
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
        return _context.Suppliers
            .OrderBy(s => s.SupplierName)
            .ToList();
    }

    public OperationResult Add(Supplier supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier.SupplierName))
        {
            return OperationResult.Failure("Supplier name is required.");
        }

        if (string.IsNullOrWhiteSpace(supplier.PhoneNumber))
        {
            return OperationResult.Failure("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(supplier.Email))
        {
            return OperationResult.Failure("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(supplier.AddressLine))
        {
            return OperationResult.Failure("Address is required.");
        }

        if (EmailExists(supplier.Email, null))
        {
            return OperationResult.Failure("Email already exists.");
        }

        try
        {
            supplier.SupplierCode = GenerateSupplierCode();
            supplier.CreatedAt = DateTime.Now;
            supplier.UpdatedAt = null;
            supplier.IsActive = true;

            _context.Suppliers.Add(supplier);
            _context.SaveChanges();
            return OperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Unable to add supplier because the database rejected the change.");
        }
        catch (Exception)
        {
            return OperationResult.Failure("An unexpected error occurred while adding the supplier.");
        }
    }

    public OperationResult Update(Supplier supplier)
    {
        Supplier? existing = _context.Suppliers.Find(supplier.SupplierId);
        if (existing == null)
        {
            return OperationResult.Failure("Supplier not found.");
        }

        if (string.IsNullOrWhiteSpace(supplier.SupplierName))
        {
            return OperationResult.Failure("Supplier name is required.");
        }

        if (string.IsNullOrWhiteSpace(supplier.PhoneNumber))
        {
            return OperationResult.Failure("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(supplier.Email))
        {
            return OperationResult.Failure("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(supplier.AddressLine))
        {
            return OperationResult.Failure("Address is required.");
        }

        if (EmailExists(supplier.Email, supplier.SupplierId))
        {
            return OperationResult.Failure("Email already exists.");
        }

        try
        {
            existing.SupplierName = supplier.SupplierName;
            existing.PhoneNumber = supplier.PhoneNumber;
            existing.Email = supplier.Email;
            existing.AddressLine = supplier.AddressLine;
            existing.UpdatedAt = DateTime.Now;

            _context.SaveChanges();
            return OperationResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("Supplier data changed while you were editing. Please reload and try again.");
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Unable to update supplier because the database rejected the change.");
        }
        catch (Exception)
        {
            return OperationResult.Failure("An unexpected error occurred while updating the supplier.");
        }
    }

    public OperationResult Delete(int id)
    {
        Supplier? supplier = _context.Suppliers.FirstOrDefault(x => x.SupplierId == id);
        if (supplier == null)
        {
            return OperationResult.Failure("Supplier not found.");
        }

        try
        {
            _context.Suppliers.Remove(supplier);
            _context.SaveChanges();
            return OperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Cannot delete this supplier because it is being used by other records.");
        }
        catch (Exception)
        {
            return OperationResult.Failure("An unexpected error occurred while deleting the supplier.");
        }
    }

    private string GenerateSupplierCode()
    {
        Supplier? lastSupplier = _context.Suppliers
            .OrderByDescending(s => s.SupplierId)
            .FirstOrDefault();

        if (lastSupplier == null)
        {
            return "SUP-001";
        }

        string? lastCode = lastSupplier.SupplierCode;
        int number = 1;

        if (!string.IsNullOrEmpty(lastCode) && lastCode.StartsWith("SUP-"))
        {
            string numPart = lastCode.Substring(4);

            if (int.TryParse(numPart, out int parsedNumber))
            {
                number = parsedNumber + 1;
            }
        }

        return $"SUP-{number:D3}";
    }

    private bool EmailExists(string? email, int? excludingSupplierId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        string normalizedEmail = email.Trim();

        return _context.Suppliers.Any(s =>
            s.Email != null
            && s.Email == normalizedEmail
            && (!excludingSupplierId.HasValue || s.SupplierId != excludingSupplierId.Value));
    }
}
