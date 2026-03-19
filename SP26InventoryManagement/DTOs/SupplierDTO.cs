using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SP26InventoryManagement.DTOs
{
    public class SupplierDTO : INotifyPropertyChanged
    {
        private string _supplierName;
        private string _phoneNumber;
        private string _email;
        private string _addressLine;

        public string SupplierName
        {
            get => _supplierName;
            set
            {
                _supplierName = value;
                OnPropertyChanged();
            }
        }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                _phoneNumber = value;
                OnPropertyChanged();
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
            }
        }

        public string AddressLine
        {
            get => _addressLine;
            set
            {
                _addressLine = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}