using System.Windows;
using System.Windows.Controls;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.Views
{
    /// <summary>
    /// Interaction logic for ProductView.xaml
    /// </summary>
    public partial class ProductView : Window
    {
        // Constructor receives ProductViewModel from DI Container
        public ProductView(ProductViewModel viewModel)
        {
            InitializeComponent();

            // Set DataContext so Bindings in XAML can work
            this.DataContext = viewModel;
        }

        /// <summary>
        /// Triggers when text in Name or SKU boxes changes.
        /// Forces the Save command to re-evaluate its "CanExecute" status.
        /// </summary>
        private void InputControl_Changed(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext is ProductViewModel vm)
            {
                vm.RefreshButtons();
            }
        }

        /// <summary>
        /// Triggers when a Category is selected from the ComboBox.
        /// </summary>
        private void InputControl_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataContext is ProductViewModel vm)
            {
                vm.RefreshButtons();
            }
        }
    }
}