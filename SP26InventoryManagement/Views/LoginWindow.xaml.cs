using System.Windows;
using System.Windows.Controls;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.LoginSucceeded += OnLoginSucceeded;
    }

    public LoginViewModel ViewModel { get; }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.NotifyPasswordInputChanged();
    }

    private void OnLoginSucceeded(LoginResult _)
    {
        DialogResult = true;
        Close();
    }
}
