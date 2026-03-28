using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.Views;

public partial class CustomerView : Window
{
    public CustomerView(CustomerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
