using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Models;
using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.Services;
namespace SP26InventoryManagement.ViewModels
{
    public class SupplierViewModel : BaseViewModel

    {

        private readonly ISupplierService _supplierService;
        private readonly Sp26inventoryManagementDbContext _context;

        public ObservableCollection<Supplier> Suppliers { get; set; }

        private SupplierDTO _form;
        public SupplierDTO Form
        {
            get => _form;
            set
            {
                if (_form != null)
                    _form.PropertyChanged -= FormChanged;

                _form = value;
                OnPropertyChanged();

                if (_form != null)
                    _form.PropertyChanged += FormChanged;

                RaiseAllCommands();
            }
        }

        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                _selectedSupplier = value;
                OnPropertyChanged();

                // 👉 đổ dữ liệu lên form
                if (value != null)
                {
                    Form.SupplierName = value.SupplierName;
                    Form.PhoneNumber = value.PhoneNumber;
                    Form.Email = value.Email;
                    Form.AddressLine = value.AddressLine;
                }

                RaiseAllCommands();
            }
        }

        public RelayCommand AddCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }

        public SupplierViewModel(ISupplierService supplierService)
        {
            _supplierService = supplierService;

            Suppliers = new ObservableCollection<Supplier>();

            AddCommand = new RelayCommand(AddSupplier, CanAdd);
            UpdateCommand = new RelayCommand(UpdateSupplier, CanUpdate);
            DeleteCommand = new RelayCommand(DeleteSupplier, CanDelete);

            Form = new SupplierDTO();

            LoadData();
        }

        private void FormChanged(object sender, PropertyChangedEventArgs e)
        {
            RaiseAllCommands();
        }

        private void RaiseAllCommands()
        {
            AddCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
        }

        // ================= CRUD =================

        private void AddSupplier()
        {
            var supplier = new Supplier
            {
                SupplierName = Form.SupplierName,
                PhoneNumber = Form.PhoneNumber,
                Email = Form.Email,
                AddressLine = Form.AddressLine,
                IsActive = true
            };

            _supplierService.Add(supplier);

            LoadData(); // reload lại

            ClearForm();
        }

        private void UpdateSupplier()
        {
            if (SelectedSupplier == null) return;

            var supplier = new Supplier
            {
                SupplierId = SelectedSupplier.SupplierId,
                SupplierName = Form.SupplierName,
                PhoneNumber = Form.PhoneNumber,
                Email = Form.Email,
                AddressLine = Form.AddressLine
            };

            _supplierService.Update(supplier);

            LoadData();
        }

        private void DeleteSupplier()
        {
            if (SelectedSupplier == null) return;

            _supplierService.Delete(SelectedSupplier.SupplierId);

            LoadData();

            ClearForm();
        }

        private void LoadData()
        {
            Suppliers.Clear();

            var list = _supplierService.GetAll();

            foreach (var item in list)
            {
                Suppliers.Add(item);
            }
        }

        // ================= VALIDATION =================

        private bool CanAdd()
        {
            return !string.IsNullOrWhiteSpace(Form.SupplierName);
        }

        private bool CanUpdate()
        {
            return SelectedSupplier != null;
        }

        private bool CanDelete()
        {
            return SelectedSupplier != null;
        }

        // ================= HELPER =================

        private void ClearForm()
        {
            Form = new SupplierDTO();
            SelectedSupplier = null;
        }
    }
}