using System.Windows;
using System.Windows.Controls;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.Views
{
    public partial class CategoryView : Window
{
    public CategoryView(CategoryViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;
    }

    private void InputControl_Changed(object sender, TextChangedEventArgs e)
    {
        if (this.DataContext is CategoryViewModel vm) vm.RefreshButtons();
    }

    private void InputControl_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (this.DataContext is CategoryViewModel vm) vm.RefreshButtons();
    }
}
}