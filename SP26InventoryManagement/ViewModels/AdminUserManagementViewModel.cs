using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class AdminUserManagementViewModel : ObservableObject
{
    private const string AdminRoleCode = "ADMIN";

    private readonly IAuthService _authService;
    private readonly IUserManagementService _userManagementService;
    private readonly IRoleRepository _roleRepository;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IUserDialogService _userDialogService;
    private readonly IMessageService _messageService;

    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand _openCreateUserCommand;
    private readonly AsyncRelayCommand _openStaffWarehouseAssignmentCommand;
    private readonly AsyncRelayCommand _saveRolesCommand;
    private readonly AsyncRelayCommand _resetPasswordCommand;
    private readonly AsyncRelayCommand _deactivateUserCommand;
    private readonly AsyncRelayCommand _reactivateUserCommand;
    private readonly AsyncRelayCommand _openChangePasswordCommand;
    private readonly AsyncRelayCommand _logoutCommand;

    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private int _currentPage = 1;
    private int _totalCount;
    private UserListItemDto? _selectedUser;
    private FilterOption<int?>? _selectedRoleFilter;
    private FilterOption<bool?>? _selectedStatusFilter;

    public AdminUserManagementViewModel(
        IAuthService authService,
        IUserManagementService userManagementService,
        IRoleRepository roleRepository,
        CurrentUserContext currentUserContext,
        IUserDialogService userDialogService,
        IMessageService messageService)
    {
        _authService = authService;
        _userManagementService = userManagementService;
        _roleRepository = roleRepository;
        _currentUserContext = currentUserContext;
        _userDialogService = userDialogService;
        _messageService = messageService;

        Users = [];
        RoleFilterOptions = [];
        StatusFilterOptions =
        [
            new FilterOption<bool?> { Label = "All", Value = null },
            new FilterOption<bool?> { Label = "Active", Value = true },
            new FilterOption<bool?> { Label = "Inactive", Value = false }
        ];
        SelectedStatusFilter = StatusFilterOptions[0];

        RoleSelections = [];

        _searchCommand = new AsyncRelayCommand(() => LoadUsersAsync(1), () => !IsBusy && HasAdminSession());
        _nextPageCommand = new AsyncRelayCommand(() => LoadUsersAsync(CurrentPage + 1), CanMoveToNextPage);
        _previousPageCommand = new AsyncRelayCommand(() => LoadUsersAsync(CurrentPage - 1), CanMoveToPreviousPage);
        _openCreateUserCommand = new AsyncRelayCommand(OpenCreateUserAsync, () => !IsBusy && HasAdminSession());
        _openStaffWarehouseAssignmentCommand = new AsyncRelayCommand(OpenStaffWarehouseAssignmentAsync, () => !IsBusy && HasAdminSession());
        _saveRolesCommand = new AsyncRelayCommand(SaveRolesAsync, CanSaveRoles);
        _resetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, CanOperateOnSelectedUser);
        _deactivateUserCommand = new AsyncRelayCommand(DeactivateUserAsync, CanDeactivateSelectedUser);
        _reactivateUserCommand = new AsyncRelayCommand(ReactivateUserAsync, CanReactivateSelectedUser);
        _openChangePasswordCommand = new AsyncRelayCommand(OpenChangePasswordAsync, () => _currentUserContext.IsAuthenticated && !IsBusy);
        _logoutCommand = new AsyncRelayCommand(LogoutAsync, () => !IsBusy);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<UserListItemDto> Users { get; }

    public ObservableCollection<FilterOption<int?>> RoleFilterOptions { get; }

    public ObservableCollection<FilterOption<bool?>> StatusFilterOptions { get; }

    public ObservableCollection<RoleSelectionItem> RoleSelections { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(PageSummary));
                RaiseCommandStates();
            }
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set
        {
            if (SetProperty(ref _totalCount, value))
            {
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageSummary));
                RaiseCommandStates();
            }
        }
    }

    public int PageSize => 20;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    public string PageSummary => TotalCount == 0 ? "No users found." : $"Page {CurrentPage}/{Math.Max(1, TotalPages)} - {TotalCount} users";

    public UserListItemDto? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                SyncRoleSelectionForSelectedUser();
                RaiseCommandStates();
            }
        }
    }

    public FilterOption<int?>? SelectedRoleFilter
    {
        get => _selectedRoleFilter;
        set => SetProperty(ref _selectedRoleFilter, value);
    }

    public FilterOption<bool?>? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set => SetProperty(ref _selectedStatusFilter, value);
    }

    public ICommand SearchCommand => _searchCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand OpenCreateUserCommand => _openCreateUserCommand;

    public ICommand OpenStaffWarehouseAssignmentCommand => _openStaffWarehouseAssignmentCommand;

    public ICommand SaveRolesCommand => _saveRolesCommand;

    public ICommand ResetPasswordCommand => _resetPasswordCommand;

    public ICommand DeactivateUserCommand => _deactivateUserCommand;

    public ICommand ReactivateUserCommand => _reactivateUserCommand;

    public ICommand OpenChangePasswordCommand => _openChangePasswordCommand;

    public ICommand LogoutCommand => _logoutCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!HasAdminSession())
        {
            throw new UnauthorizedAccessException("Access denied. ADMIN role is required.");
        }

        IReadOnlyList<RoleOptionDto> roles = await _roleRepository.GetActiveRolesAsync(ct);
        RoleFilterOptions.Clear();
        RoleFilterOptions.Add(new FilterOption<int?> { Label = "All roles", Value = null });
        foreach (RoleOptionDto role in roles)
        {
            RoleFilterOptions.Add(new FilterOption<int?> { Label = role.ToString(), Value = role.RoleId });
        }
        SelectedRoleFilter = RoleFilterOptions.FirstOrDefault();

        RoleSelections.Clear();
        foreach (RoleOptionDto role in roles)
        {
            RoleSelectionItem selectionItem = new()
            {
                RoleId = role.RoleId,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName
            };

            selectionItem.PropertyChanged += OnRoleSelectionChanged;
            RoleSelections.Add(selectionItem);
        }

        await LoadUsersAsync(1);
    }

    private async Task LoadUsersAsync(int pageNumber, int? preferredUserId = null)
    {
        if (!HasAdminSession() || !_currentUserContext.UserId.HasValue)
        {
            StatusMessage = "Session is no longer valid for admin actions.";
            return;
        }

        if (IsBusy)
        {
            return;
        }

        if (pageNumber <= 0)
        {
            pageNumber = 1;
        }

        int? selectedUserId = preferredUserId ?? SelectedUser?.UserId;

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            PagedResult<UserListItemDto> result = await _userManagementService.SearchUsersAsync(
                new UserSearchCriteria
                {
                    SearchText = SearchText,
                    RoleId = SelectedRoleFilter?.Value,
                    IsActive = SelectedStatusFilter?.Value,
                    PageNumber = pageNumber,
                    PageSize = PageSize
                },
                actorUserId: _currentUserContext.UserId.Value,
                CancellationToken.None);

            Users.Clear();
            foreach (UserListItemDto item in result.Items)
            {
                Users.Add(item);
            }

            CurrentPage = result.PageNumber;
            TotalCount = result.TotalCount;
            SelectedUser = selectedUserId.HasValue
                ? Users.FirstOrDefault(user => user.UserId == selectedUserId.Value) ?? Users.FirstOrDefault()
                : Users.FirstOrDefault();
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = ex.Message;
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load users: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenCreateUserAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        bool created = await _userDialogService.ShowCreateUserDialogAsync(CancellationToken.None);
        if (created)
        {
            await LoadUsersAsync(CurrentPage);
        }
    }

    private async Task OpenStaffWarehouseAssignmentAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        await _userDialogService.ShowStaffWarehouseAssignmentDialogAsync(CancellationToken.None);
    }

    private async Task SaveRolesAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        if (!_currentUserContext.UserId.HasValue || SelectedUser is null)
        {
            _messageService.ShowError("Please select a user to update roles.");
            return;
        }

        int targetUserId = SelectedUser.UserId;

        OperationResult result = await _userManagementService.SetUserRolesAsync(
            targetUserId: targetUserId,
            roleIds: RoleSelections.Where(role => role.IsSelected).Select(role => role.RoleId).ToArray(),
            actorUserId: _currentUserContext.UserId.Value,
            ct: CancellationToken.None);

        if (!result.IsSuccess)
        {
            if (HandleAccessOrSessionFailure(result.ErrorMessage))
            {
                return;
            }

            _messageService.ShowError(result.ErrorMessage ?? "Failed to update roles.");
            return;
        }

        _messageService.ShowInfo("User roles updated successfully.");
        await LoadUsersAsync(CurrentPage, targetUserId);
    }

    private async Task ResetPasswordAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        if (!_currentUserContext.UserId.HasValue || SelectedUser is null)
        {
            _messageService.ShowError("Please select a user to reset password.");
            return;
        }

        if (!_messageService.Confirm($"Reset password for '{SelectedUser.Username}'?"))
        {
            return;
        }

        ResetPasswordResult result = await _userManagementService.ResetPasswordAsync(
            SelectedUser.UserId,
            _currentUserContext.UserId.Value,
            CancellationToken.None);

        if (!result.IsSuccess)
        {
            if (HandleAccessOrSessionFailure(result.ErrorMessage))
            {
                return;
            }

            _messageService.ShowError(result.ErrorMessage ?? "Failed to reset password.");
            return;
        }

        _messageService.ShowPasswordWithCopy(
            SelectedUser.Username,
            result.GeneratedPassword ?? string.Empty,
            "Password Reset");
    }

    private async Task DeactivateUserAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        if (!_currentUserContext.UserId.HasValue || SelectedUser is null)
        {
            _messageService.ShowError("Please select a user to deactivate.");
            return;
        }

        int targetUserId = SelectedUser.UserId;

        if (!_messageService.Confirm($"Deactivate user '{SelectedUser.Username}'?"))
        {
            return;
        }

        OperationResult result = await _userManagementService.DeactivateUserAsync(
            targetUserId,
            _currentUserContext.UserId.Value,
            CancellationToken.None);

        if (!result.IsSuccess)
        {
            if (HandleAccessOrSessionFailure(result.ErrorMessage))
            {
                return;
            }

            _messageService.ShowError(result.ErrorMessage ?? "Failed to deactivate user.");
            return;
        }

        await LoadUsersAsync(CurrentPage, targetUserId);
    }

    private async Task ReactivateUserAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        if (!_currentUserContext.UserId.HasValue || SelectedUser is null)
        {
            _messageService.ShowError("Please select a user to reactivate.");
            return;
        }

        int targetUserId = SelectedUser.UserId;

        OperationResult result = await _userManagementService.ReactivateUserAsync(
            targetUserId,
            _currentUserContext.UserId.Value,
            CancellationToken.None);

        if (!result.IsSuccess)
        {
            if (HandleAccessOrSessionFailure(result.ErrorMessage))
            {
                return;
            }

            _messageService.ShowError(result.ErrorMessage ?? "Failed to reactivate user.");
            return;
        }

        await LoadUsersAsync(CurrentPage, targetUserId);
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

        TriggerLogout();
        return Task.CompletedTask;
    }

    private void SyncRoleSelectionForSelectedUser()
    {
        HashSet<int> selectedRoleIds = SelectedUser?.Roles.Select(role => role.RoleId).ToHashSet() ?? [];

        foreach (RoleSelectionItem roleSelection in RoleSelections)
        {
            roleSelection.IsSelected = selectedRoleIds.Contains(roleSelection.RoleId);
        }
    }

    private void OnRoleSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoleSelectionItem.IsSelected))
        {
            _saveRolesCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanMoveToNextPage()
    {
        return !IsBusy && HasAdminSession() && TotalPages > 0 && CurrentPage < TotalPages;
    }

    private bool CanMoveToPreviousPage()
    {
        return !IsBusy && HasAdminSession() && CurrentPage > 1;
    }

    private bool CanOperateOnSelectedUser()
    {
        return !IsBusy && HasAdminSession() && SelectedUser is not null && _currentUserContext.UserId.HasValue;
    }

    private bool CanDeactivateSelectedUser()
    {
        return CanOperateOnSelectedUser() && SelectedUser!.IsActive;
    }

    private bool CanReactivateSelectedUser()
    {
        return CanOperateOnSelectedUser() && !SelectedUser!.IsActive;
    }

    private bool CanSaveRoles()
    {
        return !IsBusy && HasAdminSession() && SelectedUser is not null;
    }

    private bool HasAdminSession()
    {
        return _currentUserContext.IsAuthenticated && _currentUserContext.IsInRole(AdminRoleCode);
    }

    private bool EnsureAdminSessionForAction()
    {
        if (HasAdminSession())
        {
            return true;
        }

        _messageService.ShowError("Your admin session has expired or no longer has ADMIN permission.");
        TriggerLogout();
        return false;
    }

    private bool HandleAccessOrSessionFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        if (errorMessage.Contains("Session", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("inactive", StringComparison.OrdinalIgnoreCase))
        {
            _messageService.ShowError(errorMessage);
            TriggerLogout();
            return true;
        }

        return false;
    }

    private void TriggerLogout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    private void RaiseCommandStates()
    {
        _searchCommand.RaiseCanExecuteChanged();
        _nextPageCommand.RaiseCanExecuteChanged();
        _previousPageCommand.RaiseCanExecuteChanged();
        _openCreateUserCommand.RaiseCanExecuteChanged();
        _openStaffWarehouseAssignmentCommand.RaiseCanExecuteChanged();
        _saveRolesCommand.RaiseCanExecuteChanged();
        _resetPasswordCommand.RaiseCanExecuteChanged();
        _deactivateUserCommand.RaiseCanExecuteChanged();
        _reactivateUserCommand.RaiseCanExecuteChanged();
        _openChangePasswordCommand.RaiseCanExecuteChanged();
        _logoutCommand.RaiseCanExecuteChanged();
    }
}
