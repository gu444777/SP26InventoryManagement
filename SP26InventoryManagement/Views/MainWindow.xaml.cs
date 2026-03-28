using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.ViewModels;
using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace SP26InventoryManagement
{
    public partial class MainWindow : Window
    {
        private const string StaffRoleCode = "WAREHOUSE_STAFF";
        private const string ManagerRoleCode = "MANAGER";
        private const string AdminRoleCode = "ADMIN";

        private readonly CurrentUserContext _currentUserContext;
        private readonly DispatcherTimer _sessionMonitorTimer;
        private bool _isNavigatingToLogin;

        public MainWindow(MainWindowViewModel viewModel, CurrentUserContext currentUserContext)
        {
            InitializeComponent();
            ViewModel = viewModel;
            _currentUserContext = currentUserContext;
            DataContext = viewModel;
            ViewModel.LogoutRequested += OnLogoutRequested;
            _sessionMonitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _sessionMonitorTimer.Tick += OnSessionMonitorTick;
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        public MainWindowViewModel ViewModel { get; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _sessionMonitorTimer.Start();
        }

        private void OpenIssueStaffButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            bool canOpenStaffWindow = _currentUserContext.IsInRole(StaffRoleCode);
            if (!canOpenStaffWindow)
            {
                MessageBox.Show(
                    "Only WAREHOUSE_STAFF can open Issue Staff screen.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<IssueStaffWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenManageUserButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            bool canOpenAdminWindow = _currentUserContext.IsInRole(AdminRoleCode);
            if (!canOpenAdminWindow)
            {
                MessageBox.Show(
                    "Only ADMIN can open User Management screen.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<AdminUserManagementWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenIssueManagerButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            bool canOpenManagerWindow = _currentUserContext.IsInRole(ManagerRoleCode);
            if (!canOpenManagerWindow)
            {
                MessageBox.Show(
                    "Only MANAGER can open Issue Manager screen.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<IssueManagerWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenReceiptStaffButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            bool canOpenStaffWindow = _currentUserContext.IsInRole(StaffRoleCode) || _currentUserContext.IsInRole(AdminRoleCode);
            if (!canOpenStaffWindow)
            {
                MessageBox.Show(
                    "Only WAREHOUSE_STAFF or ADMIN can open Receipt Staff screen.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<ReceiptStaffWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenTransferButton_OnClick(object sender, RoutedEventArgs e)
{
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }
bool canOpenTransferWindow =
                _currentUserContext.IsInRole(StaffRoleCode) ||
                _currentUserContext.IsInRole(AdminRoleCode);
            if (!canOpenTransferWindow)
            {
                MessageBox.Show(
                    "Only WAREHOUSE_STAFF or ADMIN can open Transfer screen.",
"Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }
var window = app.Services.GetRequiredService<TransferWindow>();
window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenReceiptManagerButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            bool canOpenManagerWindow = _currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode);
            if (!canOpenManagerWindow)
            {
                MessageBox.Show(
                    "Only MANAGER or ADMIN can open Receipt Manager screen.",
                    "Access Denied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<ReceiptManagerWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenStockSnapshotButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<StockSnapshotWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenStockLedgerButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<StockLedgerWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
        }

        private void OpenExpiryAlertsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_currentUserContext.IsAuthenticated)
            {
                NavigateToLogin();
                return;
            }

            if (Application.Current is not App app)
            {
                return;
            }

            var window = app.Services.GetRequiredService<ExpiryAlertWindow>();
            window.Owner = this;
            window.Show();
            window.Activate();
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
            if (_isNavigatingToLogin || _currentUserContext.IsAuthenticated)
            {
                return;
            }

            NavigateToLogin();
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

            if (Application.Current is App app)
            {
                _isNavigatingToLogin = true;
                _sessionMonitorTimer.Stop();
                app.NavigateToLoginAfterLogout(this);
            }
        }

       
    }
}
