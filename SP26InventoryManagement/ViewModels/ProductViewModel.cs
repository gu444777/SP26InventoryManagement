using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SP26InventoryManagement;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.ViewModels;

namespace SP26InventoryManagement.ViewModels
{
    public class ProductViewModel : ViewModelBase
{
    private readonly ProductService _service;
    public ObservableCollection<Category> Categories { get; set; } = new();
    public ObservableCollection<Product> Products { get; set; } = new();

    private Product _selectedProduct = new();
    public Product SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (value != null && value.ProductId > 0)
            {
                _selectedProduct = new Product
                {
                    ProductId = value.ProductId,
                    ProductName = value.ProductName,
                    Sku = value.Sku,
                    CategoryId = value.CategoryId,
                    BaseUom = value.BaseUom,
                    IsActive = value.IsActive
                };
            }
            else
            {
                _selectedProduct = new Product { ProductId = 0, IsActive = true, BaseUom = "PCS", Sku = "" };
            }
            OnPropertyChanged();
            RefreshButtons();
        }
    }

    // Phương thức này để ép giao diện kiểm tra lại điều kiện của nút Lưu/Xóa
    public void RefreshButtons()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RefreshCommand { get; }

    public ProductViewModel(ProductService service)
    {
        _service = service;
        // Nút Lưu sáng khi Tên sản phẩm không trống
        SaveCommand = new RelayCommand(ExecuteSave, () => !string.IsNullOrWhiteSpace(SelectedProduct?.ProductName));
        // Nút Xóa sáng khi Sản phẩm đã tồn tại (ID > 0)
        DeleteCommand = new RelayCommand(ExecuteDelete, () => SelectedProduct?.ProductId > 0);
        RefreshCommand = new RelayCommand(LoadData);
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            var cats = _service.GetAllCategories();
            Categories.Clear();
            foreach (var c in cats) Categories.Add(c);

            var prods = _service.GetAllProducts();
            Products.Clear();
            foreach (var p in prods) Products.Add(p);

            SelectedProduct = new Product { ProductId = 0, IsActive = true, BaseUom = "PCS", Sku = "" };
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void ExecuteSave()
    {
        try
        {
            if (SelectedProduct.CategoryId <= 0)
            {
                MessageBox.Show("Vui lòng chọn Loại sản phẩm!");
                return;
            }
            _service.SaveProduct(SelectedProduct);
            MessageBox.Show("Thành công!");
            LoadData();
        }
        catch (Exception ex) { MessageBox.Show("Lỗi: " + ex.Message); }
    }

    private void ExecuteDelete()
    {
        if (MessageBox.Show("Xác nhận xóa?", "Hỏi", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            try
            {
                _service.DeleteProduct(SelectedProduct.ProductId);
                LoadData();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}
}