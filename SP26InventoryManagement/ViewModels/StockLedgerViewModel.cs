using System.Collections.ObjectModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class StockLedgerViewModel : ObservableObject
{
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedWarehouseFilter = "All";
    private string _selectedProductFilter = "All";
    private string _selectedMovementFilter = "All";

    public StockLedgerViewModel(
        IStockLedgerService stockLedgerService,
        IMessageService messageService,
        IAuthService authService,
        CurrentUserContext currentUserContext)
    {
        _stockLedgerService = stockLedgerService;
        _messageService = messageService;
        _authService = authService;
        _currentUserContext = currentUserContext;

        AllEntries = [];
        FilteredEntries = [];
        WarehouseFilters = ["All"];
        ProductFilters = ["All"];
        MovementFilters = ["All", "Inbound", "Outbound"];

        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRefresh);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<StockLedgerEntryDto> AllEntries { get; }
    public ObservableCollection<StockLedgerEntryDto> FilteredEntries { get; }
    public ObservableCollection<string> WarehouseFilters { get; }
    public ObservableCollection<string> ProductFilters { get; }
    public ObservableCollection<string> MovementFilters { get; }

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

    public string SelectedMovementFilter
    {
        get => _selectedMovementFilter;
        set
        {
            if (SetProperty(ref _selectedMovementFilter, value))
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
            IReadOnlyList<StockLedgerEntryDto> entries = await _stockLedgerService.GetStockLedgerAsync(CancellationToken.None);
            ReplaceCollection(AllEntries, entries);
            RefreshFilterOptions(entries);
            ApplyFilters();
            StatusMessage = $"{FilteredEntries.Count} ledger rows loaded.";
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

    private void RefreshFilterOptions(IReadOnlyList<StockLedgerEntryDto> entries)
    {
        string previousWarehouse = SelectedWarehouseFilter;
        string previousProduct = SelectedProductFilter;

        List<string> warehouses = entries
            .Select(entry => entry.WarehouseDisplay)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> products = entries
            .Select(entry => entry.ProductDisplay)
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
        IEnumerable<StockLedgerEntryDto> query = AllEntries;

        if (!string.Equals(SelectedWarehouseFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(entry => string.Equals(entry.WarehouseDisplay, SelectedWarehouseFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedProductFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(entry => string.Equals(entry.ProductDisplay, SelectedProductFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedMovementFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(entry => string.Equals(entry.MovementDisplay, SelectedMovementFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string keyword = SearchText.Trim();
            query = query.Where(entry =>
                entry.TransactionNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.WarehouseCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.WarehouseName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.Sku.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.ProductName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.LotCode.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.ReferenceNo.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.CounterpartyName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.CreatedBy.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                entry.PostedBy.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceCollection(FilteredEntries, query);
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
