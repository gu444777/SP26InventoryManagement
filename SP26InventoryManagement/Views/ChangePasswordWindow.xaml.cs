using System.Windows;
using System.Windows.Controls;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement;

public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.CloseRequested += OnCloseRequested;
    }

    public ChangePasswordViewModel ViewModel { get; }

    private void CurrentPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            ViewModel.CurrentPassword = passwordBox.Password;
        }
    }

    private void NewPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            ViewModel.NewPassword = passwordBox.Password;
        }
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            ViewModel.ConfirmPassword = passwordBox.Password;
        }
    }

    private void OnCloseRequested(bool isSuccess)
    {
        DialogResult = isSuccess;
        Close();
    }
}
