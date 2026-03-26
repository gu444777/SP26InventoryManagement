using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services.Interfaces;

namespace SP26InventoryManagement.Services;

public class CustomerService : ICustomerService
{
    private readonly Sp26inventoryManagementDbContext _context;

    public CustomerService(Sp26inventoryManagementDbContext context)
    {
        _context = context;
    }

    public List<Customer> GetAll()
    {
        return _context.Customers
            .OrderBy(c => c.CustomerName)
            .ToList();
    }

    public OperationResult Add(Customer customer)
    {
        if (string.IsNullOrWhiteSpace(customer.CustomerName))
        {
            return OperationResult.Failure("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            return OperationResult.Failure("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return OperationResult.Failure("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.AddressLine))
        {
            return OperationResult.Failure("Address is required.");
        }

        if (EmailExists(customer.Email, null))
        {
            return OperationResult.Failure("Email already exists.");
        }

        try
        {
            customer.CustomerCode = GenerateCustomerCode();
            customer.CreatedAt = DateTime.Now;
            customer.UpdatedAt = null;
            customer.IsActive = true;

            _context.Customers.Add(customer);
            _context.SaveChanges();
            return OperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Unable to add customer because the database rejected the change.");
        }
        catch (Exception)
        {
            return OperationResult.Failure("An unexpected error occurred while adding the customer.");
        }
    }

    public OperationResult Update(Customer customer)
    {
        Customer? existing = _context.Customers.Find(customer.CustomerId);
        if (existing == null)
        {
            return OperationResult.Failure("Customer not found.");
        }

        if (string.IsNullOrWhiteSpace(customer.CustomerName))
        {
            return OperationResult.Failure("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.PhoneNumber))
        {
            return OperationResult.Failure("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            return OperationResult.Failure("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(customer.AddressLine))
        {
            return OperationResult.Failure("Address is required.");
        }

        if (EmailExists(customer.Email, customer.CustomerId))
        {
            return OperationResult.Failure("Email already exists.");
        }

        try
        {
            existing.CustomerName = customer.CustomerName;
            existing.PhoneNumber = customer.PhoneNumber;
            existing.Email = customer.Email;
            existing.AddressLine = customer.AddressLine;
            existing.UpdatedAt = DateTime.Now;

            _context.SaveChanges();
            return OperationResult.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("Customer data changed while you were editing. Please reload and try again.");
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Unable to update customer because the database rejected the change.");
        }
        catch (Exception)
        {
            return OperationResult.Failure("An unexpected error occurred while updating the customer.");
        }
    }

    public OperationResult Delete(int id)
    {
        Customer? customer = _context.Customers.FirstOrDefault(c => c.CustomerId == id);
        if (customer == null)
        {
            return OperationResult.Failure("Customer not found.");
        }

        try
        {
            _context.Customers.Remove(customer);
            _context.SaveChanges();
            return OperationResult.Success();
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Cannot delete this customer because it is being used by other records.");
        }
        catch (Exception)
        {
            return OperationResult.Failure("An unexpected error occurred while deleting the customer.");
        }
    }

    private string GenerateCustomerCode()
    {
        Customer? lastCustomer = _context.Customers
            .OrderByDescending(c => c.CustomerId)
            .FirstOrDefault();

        if (lastCustomer == null)
        {
            return "CUS-001";
        }

        string? lastCode = lastCustomer.CustomerCode;
        int number = 1;

        if (!string.IsNullOrWhiteSpace(lastCode) && lastCode.StartsWith("CUS-", StringComparison.OrdinalIgnoreCase))
        {
            string numPart = lastCode.Substring(4);
            if (int.TryParse(numPart, out int parsedNumber))
            {
                number = parsedNumber + 1;
            }
        }

        return $"CUS-{number:D3}";
    }

    private bool EmailExists(string? email, int? excludingCustomerId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        string normalizedEmail = email.Trim();

        return _context.Customers.Any(c =>
            c.Email != null
            && c.Email == normalizedEmail
            && (!excludingCustomerId.HasValue || c.CustomerId != excludingCustomerId.Value));
    }
}
