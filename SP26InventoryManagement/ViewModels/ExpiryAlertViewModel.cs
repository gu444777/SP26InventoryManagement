using System.Collections.ObjectModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class ExpiryAlertViewModel : ObservableObject
{
    private readonly IExpiryAlertService _expiryAlertService;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedWarehouseFilter = "All";
    private string _selectedStatusFilter = "All";

    public ExpiryAlertViewModel(
        IExpiryAlertService expiryAlertService,
        IMessageService messageService,
        IAuthService authService,
        CurrentUserContext currentUserContext)
    {
        _expiryAlertService = expiryAlertService;
        _messageService = messageService;
        _authService = authService;
        _currentUserContext = currentUserContext;

        AllAlerts = [];
        FilteredAlerts = [];
        WarehouseFilters = ["All"];
        StatusFilters = ["All"];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<ExpiryAlertDto> AllAlerts { get; }
    public ObservableCollection<ExpiryAlertDto> FilteredAlerts { get; }
    public ObservableCollection<string> WarehouseFilters { get; }
    public ObservableCollection<string> StatusFilters { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedWarehouseFilter
    {
        get => _selectedWarehouseFilter;
        set
        {
            if (SetProperty(ref _selectedWarehouseFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilters();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand => _refreshCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!_currentUserContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Session expired. Please log in again.");
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            IReadOnlyList<ExpiryAlertDto> alerts = await _expiryAlertService.GetExpiryAlertsAsync(CancellationToken.None);
            ReplaceCollection(AllAlerts, alerts);
            RefreshFilterOptions(alerts);
            ApplyFilters();
            StatusMessage = $"{FilteredAlerts.Count} expiry alert rows loaded.";
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshFilterOptions(IReadOnlyList<ExpiryAlertDto> alerts)
    {
        string previousWarehouse = SelectedWarehouseFilter;
        string previousStatus = SelectedStatusFilter;

        List<string> warehouses = alerts
            .Select(alert => alert.WarehouseDisplay)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> statuses = alerts
            .Select(alert => alert.ExpiryStatus)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(WarehouseFilters, new[] { "All" }.Concat(warehouses));
        ReplaceCollection(StatusFilters, new[] { "All" }.Concat(statuses));

        SelectedWarehouseFilter = WarehouseFilters.Contains(previousWarehouse) ? previousWarehouse : "All";
        SelectedStatusFilter = StatusFilters.Contains(previousStatus) ? previousStatus : "All";
    }

    private void ApplyFilters()
    {
        IEnumerable<ExpiryAlertDto> query = AllAlerts;

        if (!string.Equals(SelectedWarehouseFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(alert => string.Equals(alert.WarehouseDisplay, SelectedWarehouseFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedStatusFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(alert => string.Equals(alert.ExpiryStatus, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string keyword = SearchText.Trim();
            query = query.Where(alert =>
                alert.WarehouseCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                alert.Sku.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                alert.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                alert.LotCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                alert.ExpiryStatus.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceCollection(FilteredAlerts, query);
    }

    private void TriggerLogout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    private bool CanRefresh()
    {
        return !IsBusy && _currentUserContext.IsAuthenticated;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }
    }
}
