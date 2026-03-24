using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SP26InventoryManagement.DTOs
{
    public class CustomerDTO : INotifyPropertyChanged
    {
        private string _customerName = string.Empty;
        private string _phoneNumber = string.Empty;
        private string _email = string.Empty;
        private string _addressLine = string.Empty;

        public string CustomerName
        {
            get => _customerName;
            set
            {
                _customerName = value;
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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
