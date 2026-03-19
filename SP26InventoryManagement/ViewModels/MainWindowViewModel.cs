using System.Windows.Input;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

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

    public MainWindowViewModel(
        IAuthService authService,
        CurrentUserContext currentUserContext,
        IUserDialogService userDialogService,
        IMessageService messageService)
    {
        _authService = authService;
        _currentUserContext = currentUserContext;
        _userDialogService = userDialogService;
        _messageService = messageService;

        _openChangePasswordCommand = new AsyncRelayCommand(OpenChangePasswordAsync, CanOpenChangePassword);
        _logoutCommand = new AsyncRelayCommand(LogoutAsync, CanLogout);
    }

    public event Action? LogoutRequested;

    public string Username => _currentUserContext.Username;

    public string FullName => _currentUserContext.FullName;

    public string RolesDisplay => _currentUserContext.RoleCodes.Count == 0
        ? "No roles"
        : string.Join(", ", _currentUserContext.RoleCodes.OrderBy(roleCode => roleCode, StringComparer.OrdinalIgnoreCase));

    public bool CanManageUsers => _currentUserContext.IsInRole(AdminRoleCode);

    public bool CanOpenIssueStaff => _currentUserContext.IsInRole(StaffRoleCode);

    public bool CanOpenIssueManager => _currentUserContext.IsInRole(ManagerRoleCode);

    public ICommand OpenChangePasswordCommand => _openChangePasswordCommand;

    public ICommand LogoutCommand => _logoutCommand;

    private bool CanOpenChangePassword()
    {
        return _currentUserContext.IsAuthenticated && _currentUserContext.UserId.HasValue;
    }

    private bool CanLogout()
    {
        return true;
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
}
