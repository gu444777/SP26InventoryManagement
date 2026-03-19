using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.ViewModels;
using SP26InventoryManagement.Views;
using System;
using System.Windows;
using System.Windows.Threading;

namespace SP26InventoryManagement
{
    public partial class MainWindow : Window
    {
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
