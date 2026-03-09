using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
