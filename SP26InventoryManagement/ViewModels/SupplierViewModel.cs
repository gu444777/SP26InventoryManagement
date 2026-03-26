using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels
{
    public class SupplierViewModel : BaseViewModel
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private readonly ISupplierService _supplierService;
        private readonly IMessageService _messageService;

        public ObservableCollection<Supplier> Suppliers { get; } = new();

        private SupplierDTO _form = null!;
        public SupplierDTO Form
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

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                _selectedSupplier = value;
                OnPropertyChanged();

                if (value != null)
                {
                    Form.SupplierName = value.SupplierName;
                    Form.PhoneNumber = value.PhoneNumber ?? string.Empty;
                    Form.Email = value.Email ?? string.Empty;
                    Form.AddressLine = value.AddressLine ?? string.Empty;
                }

                RaiseAllCommands();
            }
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand UpdateCommand { get; }

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

        public SupplierViewModel(ISupplierService supplierService, IMessageService messageService)
        {
            _supplierService = supplierService;
            _messageService = messageService;

            AddCommand = new RelayCommand(AddSupplier, CanAdd);
            UpdateCommand = new RelayCommand(UpdateSupplier, CanUpdate);
            DeleteCommand = new RelayCommand(DeleteSupplier, CanDelete);

            Form = new SupplierDTO();
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

        private void AddSupplier()
        {
            if (!ValidateForm())
            {
                return;
            }

            var supplier = new Supplier
            {
                SupplierName = Form.SupplierName.Trim(),
                PhoneNumber = Form.PhoneNumber.Trim(),
                Email = Form.Email.Trim(),
                AddressLine = Form.AddressLine.Trim(),
                IsActive = true
            };

            OperationResult result = _supplierService.Add(supplier);
            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? "Unable to add supplier.");
                return;
            }

            LoadData();
            ClearForm();
            ShowInfo("Supplier added successfully.");
        }

        private void UpdateSupplier()
        {
            if (SelectedSupplier == null)
            {
                return;
            }

            if (!ValidateForm())
            {
                return;
            }

            var supplier = new Supplier
            {
                SupplierId = SelectedSupplier.SupplierId,
                SupplierName = Form.SupplierName.Trim(),
                PhoneNumber = Form.PhoneNumber.Trim(),
                Email = Form.Email.Trim(),
                AddressLine = Form.AddressLine.Trim()
            };

            OperationResult result = _supplierService.Update(supplier);
            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? "Unable to update supplier.");
                return;
            }

            LoadData();
            ShowInfo("Supplier updated successfully.");
        }

        private void DeleteSupplier()
        {
            if (SelectedSupplier == null)
            {
                return;
            }

            if (!_messageService.Confirm(
                    $"Do you want to delete supplier '{SelectedSupplier.SupplierName}'?",
                    "Delete Supplier"))
            {
                return;
            }

            OperationResult result = _supplierService.Delete(SelectedSupplier.SupplierId);
            if (!result.IsSuccess)
            {
                ShowError(result.ErrorMessage ?? "Unable to delete supplier.");
                return;
            }

            LoadData();
            ClearForm();
            ShowInfo("Supplier deleted successfully.");
        }

        private void LoadData()
        {
            Suppliers.Clear();

            foreach (Supplier item in _supplierService.GetAll())
            {
                Suppliers.Add(item);
            }
        }

        private bool CanAdd()
        {
            return HasRequiredFields();
        }

        private bool CanUpdate()
        {
            return SelectedSupplier != null && HasRequiredFields();
        }

        private bool CanDelete()
        {
            return SelectedSupplier != null;
        }

        private void ClearForm()
        {
            Form = new SupplierDTO();
            SelectedSupplier = null;
            StatusMessage = string.Empty;
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(Form.SupplierName))
            {
                ShowError("Supplier name is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Form.PhoneNumber))
            {
                ShowError("Phone number is required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Form.Email))
            {
                ShowError("Email is required.");
                return false;
            }

            if (!EmailRegex.IsMatch(Form.Email.Trim()))
            {
                ShowError("Email format is invalid.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Form.AddressLine))
            {
                ShowError("Address is required.");
                return false;
            }

            return true;
        }

        private bool HasRequiredFields()
        {
            return !string.IsNullOrWhiteSpace(Form.SupplierName)
                && !string.IsNullOrWhiteSpace(Form.PhoneNumber)
                && !string.IsNullOrWhiteSpace(Form.Email)
                && !string.IsNullOrWhiteSpace(Form.AddressLine);
        }

        private void ShowError(string message)
        {
            StatusMessage = message;
            _messageService.ShowError(message, "Supplier Management");
        }

        private void ShowInfo(string message)
        {
            StatusMessage = message;
            _messageService.ShowInfo(message, "Supplier Management");
        }
    }
}
