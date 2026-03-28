using System.Collections.ObjectModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class StockSnapshotViewModel : ObservableObject
{
    private readonly IStockSnapshotService _stockSnapshotService;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _searchText = string.Empty;
    private bool _onlyAvailableStock = true;
    private string _selectedWarehouseFilter = "All";
    private string _selectedProductFilter = "All";

    public StockSnapshotViewModel(
        IStockSnapshotService stockSnapshotService,
        IMessageService messageService,
        IAuthService authService,
        CurrentUserContext currentUserContext)
    {
        _stockSnapshotService = stockSnapshotService;
        _messageService = messageService;
        _authService = authService;
        _currentUserContext = currentUserContext;

        AllSnapshots = [];
        FilteredSnapshots = [];
        WarehouseFilters = ["All"];
        ProductFilters = ["All"];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<StockSnapshotDto> AllSnapshots { get; }

    public ObservableCollection<StockSnapshotDto> FilteredSnapshots { get; }

    public ObservableCollection<string> WarehouseFilters { get; }

    public ObservableCollection<string> ProductFilters { get; }

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

    public bool OnlyAvailableStock
    {
        get => _onlyAvailableStock;
        set
        {
            if (SetProperty(ref _onlyAvailableStock, value))
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
            IReadOnlyList<StockSnapshotDto> snapshots = await _stockSnapshotService.GetCurrentStockSnapshotAsync(CancellationToken.None);
            ReplaceCollection(AllSnapshots, snapshots);
            RefreshFilterOptions(snapshots);
            ApplyFilters();
            StatusMessage = $"{FilteredSnapshots.Count} stock rows loaded.";
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

    private void RefreshFilterOptions(IReadOnlyList<StockSnapshotDto> snapshots)
    {
        string previousWarehouse = SelectedWarehouseFilter;
        string previousProduct = SelectedProductFilter;

        List<string> warehouses = snapshots
            .Select(snapshot => snapshot.WarehouseDisplay)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> products = snapshots
            .Select(snapshot => snapshot.ProductDisplay)
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
        IEnumerable<StockSnapshotDto> query = AllSnapshots;

        if (!string.Equals(SelectedWarehouseFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(snapshot => string.Equals(snapshot.WarehouseDisplay, SelectedWarehouseFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedProductFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(snapshot => string.Equals(snapshot.ProductDisplay, SelectedProductFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (OnlyAvailableStock)
        {
            query = query.Where(snapshot => snapshot.AvailableQty > 0);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string keyword = SearchText.Trim();
            query = query.Where(snapshot =>
                snapshot.WarehouseName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                snapshot.WarehouseCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                snapshot.Sku.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                snapshot.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                snapshot.LotCode.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceCollection(FilteredSnapshots, query);
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
