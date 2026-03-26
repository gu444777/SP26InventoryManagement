using System.Collections.ObjectModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class GrossProfitReportViewModel : ObservableObject
{
    private const string ManagerRoleCode = "MANAGER";
    private const string AdminRoleCode = "ADMIN";

    private readonly IGrossProfitReportService _grossProfitReportService;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedWarehouseFilter = "All";
    private string _selectedProductFilter = "All";

    public GrossProfitReportViewModel(
        IGrossProfitReportService grossProfitReportService,
        IMessageService messageService,
        IAuthService authService,
        CurrentUserContext currentUserContext)
    {
        _grossProfitReportService = grossProfitReportService;
        _messageService = messageService;
        _authService = authService;
        _currentUserContext = currentUserContext;

        AllRows = [];
        FilteredRows = [];
        WarehouseFilters = ["All"];
        ProductFilters = ["All"];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<GrossProfitReportRowDto> AllRows { get; }
    public ObservableCollection<GrossProfitReportRowDto> FilteredRows { get; }
    public ObservableCollection<string> WarehouseFilters { get; }
    public ObservableCollection<string> ProductFilters { get; }

    public bool HasReportPermission =>
        _currentUserContext.IsAuthenticated &&
        (_currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode));

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

    public string SelectedProductFilter
    {
        get => _selectedProductFilter;
        set
        {
            if (SetProperty(ref _selectedProductFilter, value))
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

    public decimal TotalSales => FilteredRows.Sum(row => row.SalesAmount);
    public decimal TotalCogs => FilteredRows.Sum(row => row.CogsAmount);
    public decimal TotalGrossProfit => FilteredRows.Sum(row => row.GrossProfitAmount);
    public decimal TotalGrossMarginPct => TotalSales <= 0 ? 0m : decimal.Round((TotalGrossProfit / TotalSales) * 100m, 2, MidpointRounding.AwayFromZero);

    public ICommand RefreshCommand => _refreshCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!_currentUserContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Session expired. Please log in again.");
        }

        if (!HasReportPermission)
        {
            throw new UnauthorizedAccessException("Only MANAGER or ADMIN can view Gross Profit Report.");
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            IReadOnlyList<GrossProfitReportRowDto> rows = await _grossProfitReportService.GetGrossProfitReportAsync(CancellationToken.None);
            ReplaceCollection(AllRows, rows);
            RefreshFilterOptions(rows);
            ApplyFilters();
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

    private void RefreshFilterOptions(IReadOnlyList<GrossProfitReportRowDto> rows)
    {
        string previousWarehouse = SelectedWarehouseFilter;
        string previousProduct = SelectedProductFilter;

        List<string> warehouses = rows
            .Select(row => row.WarehouseDisplay)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> products = rows
            .Select(row => row.ProductDisplay)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ReplaceCollection(WarehouseFilters, new[] { "All" }.Concat(warehouses));
        ReplaceCollection(ProductFilters, new[] { "All" }.Concat(products));

        SelectedWarehouseFilter = WarehouseFilters.Contains(previousWarehouse) ? previousWarehouse : "All";
        SelectedProductFilter = ProductFilters.Contains(previousProduct) ? previousProduct : "All";
    }

    private void ApplyFilters()
    {
        IEnumerable<GrossProfitReportRowDto> query = AllRows;

        if (!string.Equals(SelectedWarehouseFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => string.Equals(row.WarehouseDisplay, SelectedWarehouseFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedProductFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => string.Equals(row.ProductDisplay, SelectedProductFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string keyword = SearchText.Trim();
            query = query.Where(row =>
                row.TransactionNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                row.WarehouseCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                row.WarehouseName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                row.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                row.Sku.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                row.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                row.LotCode.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceCollection(FilteredRows, query);
        StatusMessage = $"{FilteredRows.Count} report rows loaded. Sales: {TotalSales:N2} | COGS: {TotalCogs:N2} | GP: {TotalGrossProfit:N2} | Margin: {TotalGrossMarginPct:N2}%";
        OnPropertyChanged(nameof(TotalSales));
        OnPropertyChanged(nameof(TotalCogs));
        OnPropertyChanged(nameof(TotalGrossProfit));
        OnPropertyChanged(nameof(TotalGrossMarginPct));
    }

    private void TriggerLogout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    private bool CanRefresh()
    {
        return !IsBusy && HasReportPermission;
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
