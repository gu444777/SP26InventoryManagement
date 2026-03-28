using System.Collections.ObjectModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class StaffWarehouseAssignmentViewModel : ObservableObject
{
    private const string AdminRoleCode = "ADMIN";
    private const string ConcurrencyConflictPrefix = "Concurrency conflict";

    private readonly IAuthService _authService;
    private readonly IUserManagementService _userManagementService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IMessageService _messageService;

    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand _assignWarehouseCommand;

    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private int _currentPage = 1;
    private int _totalCount;
    private StaffWarehouseAssignmentItemDto? _selectedStaffUser;
    private WarehouseLookupDto? _selectedWarehouse;
    private FilterOption<bool?>? _selectedStatusFilter;

    public StaffWarehouseAssignmentViewModel(
        IAuthService authService,
        IUserManagementService userManagementService,
        CurrentUserContext currentUserContext,
        IMessageService messageService)
    {
        _authService = authService;
        _userManagementService = userManagementService;
        _currentUserContext = currentUserContext;
        _messageService = messageService;

        StaffUsers = [];
        Warehouses = [];
        StatusFilterOptions =
        [
            new FilterOption<bool?> { Label = "All", Value = null },
            new FilterOption<bool?> { Label = "Active", Value = true },
            new FilterOption<bool?> { Label = "Inactive", Value = false }
        ];
        SelectedStatusFilter = StatusFilterOptions[0];

        _searchCommand = new AsyncRelayCommand(() => LoadStaffUsersAsync(1), () => !IsBusy && HasAdminSession());
        _nextPageCommand = new AsyncRelayCommand(() => LoadStaffUsersAsync(CurrentPage + 1), CanMoveToNextPage);
        _previousPageCommand = new AsyncRelayCommand(() => LoadStaffUsersAsync(CurrentPage - 1), CanMoveToPreviousPage);
        _assignWarehouseCommand = new AsyncRelayCommand(AssignWarehouseAsync, CanAssignWarehouse);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<StaffWarehouseAssignmentItemDto> StaffUsers { get; }

    public ObservableCollection<WarehouseLookupDto> Warehouses { get; }

    public ObservableCollection<FilterOption<bool?>> StatusFilterOptions { get; }

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

    public string PageSummary => TotalCount == 0
        ? "No staff users found."
        : $"Page {CurrentPage}/{Math.Max(1, TotalPages)} - {TotalCount} staff users";

    public StaffWarehouseAssignmentItemDto? SelectedStaffUser
    {
        get => _selectedStaffUser;
        set
        {
            if (SetProperty(ref _selectedStaffUser, value))
            {
                SelectWarehouseForSelectedStaff();
                RaiseCommandStates();
            }
        }
    }

    public WarehouseLookupDto? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public FilterOption<bool?>? SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set => SetProperty(ref _selectedStatusFilter, value);
    }

    public ICommand SearchCommand => _searchCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand AssignWarehouseCommand => _assignWarehouseCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!HasAdminSession() || !_currentUserContext.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Access denied. ADMIN role is required.");
        }

        IReadOnlyList<WarehouseLookupDto> warehouses = await _userManagementService.GetActiveWarehousesAsync(_currentUserContext.UserId.Value, ct);

        Warehouses.Clear();
        foreach (WarehouseLookupDto warehouse in warehouses)
        {
            Warehouses.Add(warehouse);
        }

        SelectedWarehouse = Warehouses.FirstOrDefault();

        await LoadStaffUsersAsync(1);
    }

    private async Task LoadStaffUsersAsync(int pageNumber, int? preferredUserId = null)
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

        int? selectedUserId = preferredUserId ?? SelectedStaffUser?.UserId;
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            PagedResult<StaffWarehouseAssignmentItemDto> result = await _userManagementService.GetStaffWarehouseAssignmentsAsync(
                new StaffWarehouseAssignmentSearchCriteria
                {
                    SearchText = SearchText,
                    IsActive = SelectedStatusFilter?.Value,
                    PageNumber = pageNumber,
                    PageSize = PageSize
                },
                _currentUserContext.UserId.Value,
                CancellationToken.None);

            StaffUsers.Clear();
            foreach (StaffWarehouseAssignmentItemDto item in result.Items)
            {
                StaffUsers.Add(item);
            }

            CurrentPage = result.PageNumber;
            TotalCount = result.TotalCount;
            SelectedStaffUser = selectedUserId.HasValue
                ? StaffUsers.FirstOrDefault(item => item.UserId == selectedUserId.Value) ?? StaffUsers.FirstOrDefault()
                : StaffUsers.FirstOrDefault();
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = ex.Message;
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load staff assignments: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AssignWarehouseAsync()
    {
        if (!EnsureAdminSessionForAction())
        {
            return;
        }

        if (!_currentUserContext.UserId.HasValue || SelectedStaffUser is null || SelectedWarehouse is null)
        {
            _messageService.ShowError("Please select a staff user and a warehouse.");
            return;
        }

        OperationResult result = await _userManagementService.AssignOrChangeStaffWarehouseAsync(
            SelectedStaffUser.UserId,
            SelectedWarehouse.WarehouseId,
            SelectedStaffUser.RowVersion,
            _currentUserContext.UserId.Value,
            CancellationToken.None);

        if (!result.IsSuccess)
        {
            if (HandleAccessOrSessionFailure(result.ErrorMessage))
            {
                return;
            }

            if (await HandleConcurrencyConflictAsync(result.ErrorMessage, SelectedStaffUser.UserId))
            {
                return;
            }

            _messageService.ShowError(result.ErrorMessage ?? "Failed to update warehouse assignment.");
            return;
        }

        _messageService.ShowInfo("Staff warehouse assignment updated successfully.");
        await LoadStaffUsersAsync(CurrentPage, SelectedStaffUser.UserId);
    }

    private void SelectWarehouseForSelectedStaff()
    {
        if (SelectedStaffUser?.CurrentWarehouseId is null)
        {
            SelectedWarehouse = Warehouses.FirstOrDefault();
            return;
        }

        SelectedWarehouse = Warehouses.FirstOrDefault(warehouse =>
            warehouse.WarehouseId == SelectedStaffUser.CurrentWarehouseId.Value)
            ?? Warehouses.FirstOrDefault();
    }

    private bool CanMoveToNextPage()
    {
        return !IsBusy && HasAdminSession() && TotalPages > 0 && CurrentPage < TotalPages;
    }

    private bool CanMoveToPreviousPage()
    {
        return !IsBusy && HasAdminSession() && CurrentPage > 1;
    }

    private bool CanAssignWarehouse()
    {
        return !IsBusy &&
               HasAdminSession() &&
               SelectedStaffUser is not null &&
               SelectedWarehouse is not null;
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

    private async Task<bool> HandleConcurrencyConflictAsync(string? errorMessage, int preferredUserId)
    {
        if (!IsConcurrencyConflict(errorMessage))
        {
            return false;
        }

        _messageService.ShowError(errorMessage ?? "Concurrency conflict. Please refresh and retry.");
        await LoadStaffUsersAsync(CurrentPage, preferredUserId);
        return true;
    }

    private static bool IsConcurrencyConflict(string? errorMessage)
    {
        return !string.IsNullOrWhiteSpace(errorMessage) &&
               errorMessage.Contains(ConcurrencyConflictPrefix, StringComparison.OrdinalIgnoreCase);
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
        _assignWarehouseCommand.RaiseCanExecuteChanged();
    }
}
