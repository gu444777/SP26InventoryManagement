using System.Collections.ObjectModel;
using System.Windows.Input;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.Infrastructure;
using System.Collections.Generic;
using System;
using System.Windows;

namespace SP26InventoryManagement.ViewModels
{
    public class WarehouseViewModel : ObservableObject
    {
        private readonly WarehouseService _service;
        private Warehouse _selectedWarehouse = new Warehouse();

        // Danh sách hiển thị trên DataGrid
        public ObservableCollection<Warehouse> Warehouses { get; set; } = new ObservableCollection<Warehouse>();

        public Warehouse SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                _selectedWarehouse = value;
                OnPropertyChanged();
            }
        }

        // Khai báo các Command
        public ICommand SaveCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ToggleStatusCommand { get; }

        public WarehouseViewModel(WarehouseService service)
        {
            _service = service;

            // Khởi tạo các Command
            SaveCommand = new RelayCommand(Save);
            EditCommand = new RelayCommand<Warehouse>(Edit);
            DeleteCommand = new RelayCommand<Warehouse>(Delete);
            ToggleStatusCommand = new RelayCommand<Warehouse>(ToggleStatus);

            // Tải dữ liệu ngay khi khởi tạo
            LoadData();
        }

        private void LoadData()
        {
            var list = _service.GetAll();
            Warehouses.Clear();
            foreach (var item in list)
            {
                Warehouses.Add(item);
            }
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(SelectedWarehouse.WarehouseCode) ||
                string.IsNullOrWhiteSpace(SelectedWarehouse.WarehouseName))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ mã và tên kho!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Gọi Service để lưu (Thêm mới nếu ID=0, Cập nhật nếu ID > 0)
                _service.Save(SelectedWarehouse);

                // Tải lại danh sách để cập nhật giao diện
                LoadData();

                // Reset form về trạng thái thêm mới để người dùng nhập tiếp
                SelectedWarehouse = new Warehouse();
                MessageBox.Show("Lưu dữ liệu thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Đổ dữ liệu từ dòng được chọn lên các TextBox để sửa
        private void Edit(Warehouse w)
        {
            if (w == null) return;

            // Tạo bản sao độc lập để tránh việc thay đổi trực tiếp trên DataGrid khi người dùng đang gõ (chưa nhấn Save)
            SelectedWarehouse = new Warehouse
            {
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.WarehouseCode,
                WarehouseName = w.WarehouseName,
                IsActive = w.IsActive,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt
            };
        }

        private void Delete(Warehouse w)
        {
            if (w == null) return;

            var result = MessageBox.Show($"Bạn có chắc chắn muốn xóa kho: {w.WarehouseName}?",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _service.Delete(w.WarehouseId);
                LoadData();

                // Nếu đang xóa đúng cái đang hiển thị trên Form Edit thì reset form
                if (SelectedWarehouse.WarehouseId == w.WarehouseId)
                {
                    SelectedWarehouse = new Warehouse();
                }
            }
        }

        private void ToggleStatus(Warehouse w)
        {
            if (w == null) return;
            _service.ToggleStatus(w.WarehouseId);
            LoadData();
        }
    }
}