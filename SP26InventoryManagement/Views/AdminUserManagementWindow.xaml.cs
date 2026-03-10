using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement;

public partial class AdminUserManagementWindow : Window
{
    public AdminUserManagementWindow(AdminUserManagementViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
        ViewModel.LogoutRequested += OnLogoutRequested;
    }

    public AdminUserManagementViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            await ViewModel.InitializeAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize Admin User Management screen.\n\n{ex.Message}",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private void OnLogoutRequested()
    {
        if (Application.Current is App app)
        {
            app.NavigateToLoginAfterLogout(this);
        }
    }
}
