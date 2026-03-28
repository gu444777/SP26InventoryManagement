using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.Views;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace SP26InventoryManagement.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string ManagerRoleCode = "MANAGER";
    private const string AdminRoleCode = "ADMIN";

    private readonly IAuthService _authService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IUserDialogService _userDialogService;
    private readonly IMessageService _messageService;

    private readonly AsyncRelayCommand _openChangePasswordCommand;
    private readonly AsyncRelayCommand _logoutCommand;
    private readonly AsyncRelayCommand _openWarehouseCommand;
    private readonly AsyncRelayCommand _openManageProductsCommand;
    private readonly AsyncRelayCommand _openManageCategoriesCommand;
    private readonly AsyncRelayCommand _openAdjustmentCommand; // 1. KHAI BÁO BIẾN CHO ADJUSTMENT
    private readonly IServiceProvider _serviceProvider;
    private readonly RelayCommand _openManageCustomersCommand;
    private readonly RelayCommand _openManageSuppliersCommand;

    public MainWindowViewModel(
        IAuthService authService,
        CurrentUserContext currentUserContext,
        IUserDialogService userDialogService,
        IMessageService messageService,
        IServiceProvider serviceProvider)
    {
        _authService = authService;
        _currentUserContext = currentUserContext;
        _userDialogService = userDialogService;
        _messageService = messageService;
        _serviceProvider = serviceProvider;



        // Khởi tạo các Commands
        _openWarehouseCommand = new AsyncRelayCommand(OpenWarehouseAsync);
        _openManageProductsCommand = new AsyncRelayCommand(OpenManageProductsAsync);
        _openManageCategoriesCommand = new AsyncRelayCommand(OpenManageCategoriesAsync);

        // 2. KHỞI TẠO LỆNH MỞ ADJUSTMENT
        _openAdjustmentCommand = new AsyncRelayCommand(OpenAdjustmentAsync);
        _openManageCustomersCommand = new RelayCommand(OpenManageCustomers, CanOpenManageCustomers);
        _openManageSuppliersCommand = new RelayCommand(OpenManageSuppliers, CanOpenManageSuppliers);

    }

    public event Action? LogoutRequested;

    public string Username => _currentUserContext.Username;
    public string FullName => _currentUserContext.FullName;

    public string RolesDisplay => _currentUserContext.RoleCodes.Count == 0
        ? "No roles"
        : string.Join(", ", _currentUserContext.RoleCodes.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));

    public bool CanManageUsers => _currentUserContext.IsInRole(AdminRoleCode);

    public bool CanManageMasterData =>
        _currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode);

    public bool CanOpenIssueStaff => _currentUserContext.IsInRole(StaffRoleCode);
    public bool CanOpenIssueManager => _currentUserContext.IsInRole(ManagerRoleCode);
    public bool CanManageProducts => _currentUserContext.IsInRole(AdminRoleCode) || _currentUserContext.IsInRole(ManagerRoleCode);
    public bool CanManageWarehouses => _currentUserContext.IsInRole(AdminRoleCode) || _currentUserContext.IsInRole(ManagerRoleCode);

    // 3. QUYỀN MỞ ADJUSTMENT (Hiển thị nút nếu là Staff hoặc Manager)
    public bool CanManageAdjustments => _currentUserContext.IsInRole(StaffRoleCode) || _currentUserContext.IsInRole(ManagerRoleCode);

    // Thuộc tính để Binding ra XAML
    public bool CanOpenTransfer =>
        _currentUserContext.IsInRole(StaffRoleCode) || _currentUserContext.IsInRole(AdminRoleCode);

    public bool CanOpenReceiptStaff => _currentUserContext.IsInRole(StaffRoleCode);

    public bool CanOpenReceiptManager =>
        _currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode);

    public bool CanViewStockSnapshot => _currentUserContext.IsAuthenticated;

    public bool CanViewStockLedger => _currentUserContext.IsAuthenticated;

    public bool CanViewExpiryAlerts => _currentUserContext.IsAuthenticated;

    public bool CanViewGrossProfitReport =>
        _currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode);

    public ICommand OpenChangePasswordCommand => _openChangePasswordCommand;
    public ICommand LogoutCommand => _logoutCommand;
    public ICommand OpenWarehouseCommand => _openWarehouseCommand;
    public ICommand OpenManageProductsCommand => _openManageProductsCommand;
    public ICommand OpenManageCategoriesCommand => _openManageCategoriesCommand;
    public ICommand OpenAdjustmentCommand => _openAdjustmentCommand; // 4. BINDING CHO NÚT ADJUSTMENT


    public ICommand OpenManageCustomersCommand => _openManageCustomersCommand;

    public ICommand OpenManageSuppliersCommand => _openManageSuppliersCommand;

    private bool CanOpenChangePassword()
    {
        return _currentUserContext.IsAuthenticated && _currentUserContext.UserId.HasValue;
    }

    private bool CanLogout()
    {
        return true;
    }

    private bool CanOpenManageCustomers()
    {
        return _currentUserContext.IsAuthenticated && CanManageMasterData;
    }

    private bool CanOpenManageSuppliers()
    {
        return _currentUserContext.IsAuthenticated && CanManageMasterData;
    }

    private Task OpenChangePasswordAsync()
    {
        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
            return Task.CompletedTask;

        return _userDialogService.ShowChangePasswordDialogAsync(
            _currentUserContext.UserId.Value,
            _currentUserContext.Username,
            CancellationToken.None);
    }

    private Task LogoutAsync()
    {
        if (!_messageService.Confirm("Do you want to logout?", "Logout"))
            return Task.CompletedTask;

        _authService.Logout();
        LogoutRequested?.Invoke();
        return Task.CompletedTask;
    }

    private Task OpenWarehouseAsync()
    {
        if (Application.Current is App currentApp)
        {
            var window = currentApp.Services.GetRequiredService<WarehouseView>();
            window.Show();
        }
        return Task.CompletedTask;
    }

    private Task OpenManageProductsAsync()
    {
        if (Application.Current is App currentApp)
        {
            try
            {
                var window = currentApp.Services.GetRequiredService<ProductView>();
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Products: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    private Task OpenManageCategoriesAsync()
    {
        if (Application.Current is App currentApp)
        {
            try
            {
                var window = currentApp.Services.GetRequiredService<CategoryView>();
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Categories: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    // 5. LOGIC MỞ CỬA SỔ ADJUSTMENT
    private Task OpenAdjustmentAsync()
    {
        if (Application.Current is App currentApp)
        {
            try
            {
                var window = currentApp.Services.GetRequiredService<AdjustmentView>();
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Adjustment: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    private void OpenManageCustomers()
    {
        CustomerView window = _serviceProvider.GetRequiredService<CustomerView>();
        Window? owner = GetActiveOwner();

        if (owner != null && owner != window)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }

    private void OpenManageSuppliers()
    {
        SupplierView window = _serviceProvider.GetRequiredService<SupplierView>();
        Window? owner = GetActiveOwner();

        if (owner != null && owner != window)
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }

    private static Window? GetActiveOwner()
    {
        Window? owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(currentWindow => currentWindow.IsActive);
        return owner;
    }
}
