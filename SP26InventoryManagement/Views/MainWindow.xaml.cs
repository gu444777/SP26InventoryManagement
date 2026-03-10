using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            ViewModel.LogoutRequested += OnLogoutRequested;
        }

        public MainWindowViewModel ViewModel { get; }

        private void OnLogoutRequested()
        {
            if (Application.Current is App app)
            {
                app.NavigateToLoginAfterLogout(this);
            }
        }
    }
}
