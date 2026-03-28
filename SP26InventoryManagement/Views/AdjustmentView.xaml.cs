using System.Windows;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.Views
{
    public partial class AdjustmentView : Window
    {
        public AdjustmentView(AdjustmentViewModel viewModel)
        {
            InitializeComponent();
this.DataContext =  viewModel;
        }
    }
}