using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.Services.Interfaces;

namespace SP26InventoryManagement.ViewModels;

public class CustomerViewModel : BaseViewModel
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly ICustomerService _customerService;
    private readonly IMessageService _messageService;

    public ObservableCollection<Customer> Customers { get; } = new();

    private CustomerDTO _form = null!;
    public CustomerDTO Form
    {
        get => _form;
        set
        {
            if (_form != null)
            {
                _form.PropertyChanged -= FormChanged;
            }

            _form = value;
            OnPropertyChanged();

            if (_form != null)
            {
                _form.PropertyChanged += FormChanged;
            }

            RaiseAllCommands();
        }
    }

    private Customer? _selectedCustomer;
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            _selectedCustomer = value;
            OnPropertyChanged();

            if (value != null)
            {
                Form.CustomerName = value.CustomerName;
                Form.PhoneNumber = value.PhoneNumber ?? string.Empty;
                Form.Email = value.Email ?? string.Empty;
                Form.AddressLine = value.AddressLine ?? string.Empty;
            }

            RaiseAllCommands();
        }
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand DeleteCommand { get; }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public CustomerViewModel(ICustomerService customerService, IMessageService messageService)
    {
        _customerService = customerService;
        _messageService = messageService;

        AddCommand = new RelayCommand(AddCustomer, CanAdd);
        UpdateCommand = new RelayCommand(UpdateCustomer, CanUpdate);
        DeleteCommand = new RelayCommand(DeleteCustomer, CanDelete);

        Form = new CustomerDTO();
        LoadData();
    }

    private void FormChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseAllCommands();
    }

    private void RaiseAllCommands()
    {
        AddCommand.RaiseCanExecuteChanged();
        UpdateCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
    }

    private void AddCustomer()
    {
        if (!ValidateForm())
        {
            return;
        }

        var customer = new Customer
        {
            CustomerName = Form.CustomerName.Trim(),
            PhoneNumber = NormalizeOptional(Form.PhoneNumber),
            Email = NormalizeOptional(Form.Email),
            AddressLine = NormalizeOptional(Form.AddressLine)
        };

        OperationResult result = _customerService.Add(customer);
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage ?? "Unable to add customer.");
            return;
        }

        LoadData();
        ClearForm();
        ShowInfo("Customer added successfully.");
    }

    private void UpdateCustomer()
    {
        if (SelectedCustomer == null)
        {
            return;
        }

        if (!ValidateForm())
        {
            return;
        }

        var customer = new Customer
        {
            CustomerId = SelectedCustomer.CustomerId,
            CustomerName = Form.CustomerName.Trim(),
            PhoneNumber = NormalizeOptional(Form.PhoneNumber),
            Email = NormalizeOptional(Form.Email),
            AddressLine = NormalizeOptional(Form.AddressLine)
        };

        OperationResult result = _customerService.Update(customer);
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage ?? "Unable to update customer.");
            return;
        }

        LoadData();
        ShowInfo("Customer updated successfully.");
    }

    private void DeleteCustomer()
    {
        if (SelectedCustomer == null)
        {
            return;
        }

        if (!_messageService.Confirm(
                $"Do you want to delete customer '{SelectedCustomer.CustomerName}'?",
                "Delete Customer"))
        {
            return;
        }

        OperationResult result = _customerService.Delete(SelectedCustomer.CustomerId);
        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage ?? "Unable to delete customer.");
            return;
        }

        LoadData();
        ClearForm();
        ShowInfo("Customer deleted successfully.");
    }

    private void LoadData()
    {
        Customers.Clear();

        foreach (Customer customer in _customerService.GetAll())
        {
            Customers.Add(customer);
        }
    }

    private bool CanAdd()
    {
        return HasRequiredFields();
    }

    private bool CanUpdate()
    {
        return SelectedCustomer != null && HasRequiredFields();
    }

    private bool CanDelete()
    {
        return SelectedCustomer != null;
    }

    private void ClearForm()
    {
        Form = new CustomerDTO();
        SelectedCustomer = null;
        StatusMessage = string.Empty;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool ValidateForm()
    {
        string customerName = Form.CustomerName.Trim();
        string? phoneNumber = NormalizeOptional(Form.PhoneNumber);
        string? email = NormalizeOptional(Form.Email);
        string? addressLine = NormalizeOptional(Form.AddressLine);

        if (string.IsNullOrWhiteSpace(customerName))
        {
            ShowError("Customer name is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            ShowError("Phone number is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Email is required.");
            return false;
        }

        if (!EmailRegex.IsMatch(email))
        {
            ShowError("Email format is invalid.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(addressLine))
        {
            ShowError("Address is required.");
            return false;
        }

        return true;
    }

    private bool HasRequiredFields()
    {
        return !string.IsNullOrWhiteSpace(Form.CustomerName)
            && !string.IsNullOrWhiteSpace(Form.PhoneNumber)
            && !string.IsNullOrWhiteSpace(Form.Email)
            && !string.IsNullOrWhiteSpace(Form.AddressLine);
    }

    private void ShowError(string message)
    {
        StatusMessage = message;
        _messageService.ShowError(message, "Customer Management");
    }

    private void ShowInfo(string message)
    {
        StatusMessage = message;
        _messageService.ShowInfo(message, "Customer Management");
    }
}
