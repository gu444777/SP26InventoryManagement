using System.Windows.Input;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;
using SP26InventoryManagement.Views;

namespace SP26InventoryManagement.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IUserDialogService _userDialogService;
    private readonly IMessageService _messageService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AsyncRelayCommand _openChangePasswordCommand;
    private readonly AsyncRelayCommand _logoutCommand;
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
        _openChangePasswordCommand = new AsyncRelayCommand(OpenChangePasswordAsync, CanOpenChangePassword);
        _logoutCommand = new AsyncRelayCommand(LogoutAsync, CanLogout);
        _openManageCustomersCommand = new RelayCommand(OpenManageCustomers, CanOpenManageCustomers);
        _openManageSuppliersCommand = new RelayCommand(OpenManageSuppliers, CanOpenManageSuppliers);
    }

    public event Action? LogoutRequested;

    public string Username => _currentUserContext.Username;

    public string FullName => _currentUserContext.FullName;

    public ICommand OpenChangePasswordCommand => _openChangePasswordCommand;

    public ICommand LogoutCommand => _logoutCommand;

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
        return _currentUserContext.IsAuthenticated;
    }

    private bool CanOpenManageSuppliers()
    {
        return _currentUserContext.IsAuthenticated;
    }

    private Task OpenChangePasswordAsync()
    {
        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            return Task.CompletedTask;
        }

        return _userDialogService.ShowChangePasswordDialogAsync(
            _currentUserContext.UserId.Value,
            _currentUserContext.Username,
            CancellationToken.None);
    }

    private Task LogoutAsync()
    {
        if (!_messageService.Confirm("Do you want to logout?", "Logout"))
        {
            return Task.CompletedTask;
        }

        _authService.Logout();
        LogoutRequested?.Invoke();
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
