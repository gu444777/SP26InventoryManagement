using System.Windows;
using System.Windows.Threading;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement;

public partial class StockLedgerWindow : Window
{
    private readonly CurrentUserContext _currentUserContext;
    private readonly DispatcherTimer _sessionMonitorTimer;
    private bool _isNavigatingToLogin;

    public StockLedgerWindow(StockLedgerViewModel viewModel, CurrentUserContext currentUserContext)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _currentUserContext = currentUserContext;
        DataContext = viewModel;

        _sessionMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _sessionMonitorTimer.Tick += OnSessionMonitorTick;

        Loaded += OnLoaded;
        Closed += OnClosed;
        ViewModel.LogoutRequested += OnLogoutRequested;
    }

    public StockLedgerViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            await ViewModel.InitializeAsync(CancellationToken.None);
            _sessionMonitorTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize Stock Ledger screen.\n\n{ex.Message}",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _sessionMonitorTimer.Stop();
        _sessionMonitorTimer.Tick -= OnSessionMonitorTick;
        Loaded -= OnLoaded;
        Closed -= OnClosed;
        ViewModel.LogoutRequested -= OnLogoutRequested;
    }

    private void OnSessionMonitorTick(object? sender, EventArgs e)
    {
        if (_currentUserContext.IsAuthenticated)
        {
            return;
        }

        Close();
    }

    private void OnLogoutRequested()
    {
        NavigateToLogin();
    }

    private void NavigateToLogin()
    {
        if (_isNavigatingToLogin)
        {
            return;
        }

        _isNavigatingToLogin = true;
        _sessionMonitorTimer.Stop();

        if (Owner is MainWindow ownerMainWindow && Application.Current is App app)
        {
            app.NavigateToLoginAfterLogout(ownerMainWindow);
        }

        Close();
    }
}
