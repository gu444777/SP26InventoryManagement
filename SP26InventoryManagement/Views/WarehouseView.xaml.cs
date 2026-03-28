using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.Views
{
    /// <summary>
    /// Interaction logic for WarehouseView.xaml
    /// </summary>
    public partial class WarehouseView : Window
    {
        // SỬA LỖI: Nhận ViewModel thông qua Constructor để DI tiêm vào
        public WarehouseView(WarehouseViewModel viewModel)
        {
            InitializeComponent();

            // QUAN TRỌNG: Gán DataContext để các Binding {Binding ...} trong XAML hoạt động
            this.DataContext = viewModel;
        }
    }
}