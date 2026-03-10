using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement;

public partial class CreateUserWindow : Window
{
    public CreateUserWindow(CreateUserViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.CloseRequested += OnCloseRequested;
    }

    public CreateUserViewModel ViewModel { get; }

    private void OnCloseRequested(bool isSuccess)
    {
        DialogResult = isSuccess;
        Close();
    }
}
