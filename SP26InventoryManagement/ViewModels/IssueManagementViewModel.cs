using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class IssueManagementViewModel : ObservableObject
{
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string ManagerRoleCode = "MANAGER";
    private const string AdminRoleCode = "ADMIN";

    private readonly IIssueService _issueService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;
    private readonly SemaphoreSlim _issueServiceCallGate = new(1, 1);

    private readonly RelayCommand _addLineCommand;
    private readonly RelayCommand _removeLineCommand;
    private readonly RelayCommand _clearIssueFormCommand;
    private readonly AsyncRelayCommand _previewAllocationCommand;
    private readonly AsyncRelayCommand _createIssueCommand;
    private readonly AsyncRelayCommand _refreshDraftIssuesCommand;
    private readonly AsyncRelayCommand _postIssueCommand;
    private readonly AsyncRelayCommand _cancelDraftIssueCommand;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _previewStatusMessage = string.Empty;
    private string _addQtyInput = string.Empty;
    private string _addUnitPriceInput = string.Empty;
    private string _referenceNo = string.Empty;
    private string _remarks = string.Empty;
    private string _selectedProductAvailableQtyText = "Available Qty: -";
    private decimal _selectedProductAvailableQty;
    private DateTime _transactionDate = DateTime.Today;
    private int _availableQtyRequestVersion;
    private int _draftIssueLineRequestVersion;

    private WarehouseLookupDto? _selectedWarehouse;
    private CustomerLookupDto? _selectedCustomer;
    private ProductLookupDto? _selectedProductToAdd;
    private IssueRequestLineItem? _selectedRequestLine;
    private DraftIssueHeaderDto? _selectedDraftIssue;

    public IssueManagementViewModel(
        IIssueService issueService,
        CurrentUserContext currentUserContext,
        IMessageService messageService,
        IAuthService authService)
    {
        _issueService = issueService;
        _currentUserContext = currentUserContext;
        _messageService = messageService;
        _authService = authService;

        Warehouses = [];
        Customers = [];
        Products = [];
        RequestLines = [];
        PreviewAllocations = [];
        DraftIssues = [];
        DraftIssueLines = [];

        _addLineCommand = new RelayCommand(AddLine, CanAddLine);
        _removeLineCommand = new RelayCommand(RemoveSelectedLine, CanRemoveSelectedLine);
        _clearIssueFormCommand = new RelayCommand(ClearIssueForm, CanClearIssueForm);
        _previewAllocationCommand = new AsyncRelayCommand(PreviewAllocationAsync, CanPreviewAllocation);
        _createIssueCommand = new AsyncRelayCommand(CreateIssueAsync, CanCreateIssue);
        _refreshDraftIssuesCommand = new AsyncRelayCommand(RefreshDraftIssuesAsync, CanRefreshDraftIssues);
        _postIssueCommand = new AsyncRelayCommand(PostSelectedIssueAsync, CanPostSelectedIssue);
        _cancelDraftIssueCommand = new AsyncRelayCommand(CancelSelectedDraftIssueAsync, CanCancelSelectedIssue);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<WarehouseLookupDto> Warehouses { get; }

    public ObservableCollection<CustomerLookupDto> Customers { get; }

    public ObservableCollection<ProductLookupDto> Products { get; }

    public ObservableCollection<IssueRequestLineItem> RequestLines { get; }

    public ObservableCollection<IssueAllocationPreviewItemDto> PreviewAllocations { get; }

    public ObservableCollection<DraftIssueHeaderDto> DraftIssues { get; }

    public ObservableCollection<DraftIssueLineDto> DraftIssueLines { get; }

    public WarehouseLookupDto? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                ClearPreview();
                RaiseCommandStates();
                _ = RefreshSelectedProductAvailableQtyAsync();
            }
        }
    }

    public CustomerLookupDto? SelectedCustomer
    {
        get => _selectedCustomer;
        set => SetProperty(ref _selectedCustomer, value);
    }

    public ProductLookupDto? SelectedProductToAdd
    {
        get => _selectedProductToAdd;
        set
        {
            if (SetProperty(ref _selectedProductToAdd, value))
            {
                _addLineCommand.RaiseCanExecuteChanged();
                _ = RefreshSelectedProductAvailableQtyAsync();
            }
        }
    }

    public IssueRequestLineItem? SelectedRequestLine
    {
        get => _selectedRequestLine;
        set
        {
            if (SetProperty(ref _selectedRequestLine, value))
            {
                _removeLineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DraftIssueHeaderDto? SelectedDraftIssue
    {
        get => _selectedDraftIssue;
        set
        {
            if (SetProperty(ref _selectedDraftIssue, value))
            {
                int requestVersion = Interlocked.Increment(ref _draftIssueLineRequestVersion);
                _ = LoadSelectedDraftIssueLinesAsync(requestVersion);
                _postIssueCommand.RaiseCanExecuteChanged();
                _cancelDraftIssueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AddQtyInput
    {
        get => _addQtyInput;
        set => SetInputAndRefresh(ref _addQtyInput, value);
    }

    public string AddUnitPriceInput
    {
        get => _addUnitPriceInput;
        set => SetInputAndRefresh(ref _addUnitPriceInput, value);
    }

    public string ReferenceNo
    {
        get => _referenceNo;
        set
        {
            if (SetProperty(ref _referenceNo, value))
            {
                _clearIssueFormCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Remarks
    {
        get => _remarks;
        set
        {
            if (SetProperty(ref _remarks, value))
            {
                _clearIssueFormCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime TransactionDate
    {
        get => _transactionDate;
        set
        {
            if (SetProperty(ref _transactionDate, value))
            {
                ClearPreview();
                RaiseCommandStates();
                _ = RefreshSelectedProductAvailableQtyAsync();
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
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string PreviewStatusMessage
    {
        get => _previewStatusMessage;
        private set => SetProperty(ref _previewStatusMessage, value);
    }

    public string SelectedProductAvailableQtyText
    {
        get => _selectedProductAvailableQtyText;
        private set => SetProperty(ref _selectedProductAvailableQtyText, value);
    }

    public bool HasCreateIssuePermission =>
        _currentUserContext.IsAuthenticated &&
        (_currentUserContext.IsInRole(StaffRoleCode) || _currentUserContext.IsInRole(AdminRoleCode));

    public bool HasPostIssuePermission =>
        _currentUserContext.IsAuthenticated &&
        (_currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode));

    public string PermissionSummary =>
        $"Create Draft Issue: {(HasCreateIssuePermission ? "Allowed" : "Not allowed")}. " +
        $"Post Issue: {(HasPostIssuePermission ? "Allowed" : "Not allowed")}.";

    public ICommand AddLineCommand => _addLineCommand;

    public ICommand RemoveLineCommand => _removeLineCommand;

    public ICommand ClearIssueFormCommand => _clearIssueFormCommand;

    public ICommand PreviewAllocationCommand => _previewAllocationCommand;

    public ICommand CreateIssueCommand => _createIssueCommand;

    public ICommand RefreshDraftIssuesCommand => _refreshDraftIssuesCommand;

    public ICommand PostIssueCommand => _postIssueCommand;

    public ICommand CancelDraftIssueCommand => _cancelDraftIssueCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!_currentUserContext.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Session expired. Please log in again.");
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            IReadOnlyList<WarehouseLookupDto> warehouses = await ExecuteIssueServiceCallAsync(
                () => _issueService.GetActiveWarehousesAsync(ct));
            IReadOnlyList<CustomerLookupDto> customers = await ExecuteIssueServiceCallAsync(
                () => _issueService.GetActiveCustomersAsync(ct));
            IReadOnlyList<ProductLookupDto> products = await ExecuteIssueServiceCallAsync(
                () => _issueService.GetActiveProductsAsync(ct));

            Warehouses.Clear();
            foreach (WarehouseLookupDto warehouse in warehouses)
            {
                Warehouses.Add(warehouse);
            }

            Customers.Clear();
            foreach (CustomerLookupDto customer in customers)
            {
                Customers.Add(customer);
            }

            Products.Clear();
            foreach (ProductLookupDto product in products)
            {
                Products.Add(product);
            }

            SelectedWarehouse ??= Warehouses.FirstOrDefault();
            SelectedCustomer ??= Customers.FirstOrDefault();
            SelectedProductToAdd = Products.FirstOrDefault();
            TransactionDate = DateTime.Today;
            await RefreshSelectedProductAvailableQtyAsync();

            OnPropertyChanged(nameof(HasCreateIssuePermission));
            OnPropertyChanged(nameof(HasPostIssuePermission));
            OnPropertyChanged(nameof(PermissionSummary));

            await RefreshDraftIssuesAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddLine()
    {
        if (!HasCreateIssuePermission)
        {
            _messageService.ShowError("You do not have permission to create issue drafts.");
            return;
        }

        if (SelectedProductToAdd is null)
        {
            _messageService.ShowError("Please choose a product.");
            return;
        }

        if (!TryParsePositiveDecimal(AddQtyInput, out decimal qty))
        {
            _messageService.ShowError("Quantity must be a number greater than 0.");
            return;
        }

        if (qty > _selectedProductAvailableQty)
        {
            _messageService.ShowError(
                $"Quantity cannot be greater than available quantity ({_selectedProductAvailableQty:N3}).");
            return;
        }

        if (!TryParseOptionalNonNegativeDecimal(AddUnitPriceInput, out decimal? unitPrice))
        {
            _messageService.ShowError("Unit price must be empty or a number >= 0.");
            return;
        }

        if (RequestLines.Any(line => line.ProductId == SelectedProductToAdd.ProductId))
        {
            _messageService.ShowError("This product already exists in the issue draft lines.");
            return;
        }

        RequestLines.Add(new IssueRequestLineItem
        {
            ProductId = SelectedProductToAdd.ProductId,
            Sku = SelectedProductToAdd.Sku,
            ProductName = SelectedProductToAdd.ProductName,
            Qty = decimal.Round(qty, 3),
            UnitPrice = unitPrice.HasValue ? decimal.Round(unitPrice.Value, 4) : null
        });

        AddUnitPriceInput = string.Empty;
        ClearPreview();
        _clearIssueFormCommand.RaiseCanExecuteChanged();
        RaiseCommandStates();
    }

    private void RemoveSelectedLine()
    {
        if (SelectedRequestLine is null)
        {
            return;
        }

        RequestLines.Remove(SelectedRequestLine);
        SelectedRequestLine = null;
        ClearPreview();
        _clearIssueFormCommand.RaiseCanExecuteChanged();
        RaiseCommandStates();
    }

    private void ClearIssueForm()
    {
        RequestLines.Clear();
        SelectedRequestLine = null;
        AddQtyInput = string.Empty;
        AddUnitPriceInput = string.Empty;
        ReferenceNo = string.Empty;
        Remarks = string.Empty;
        ClearPreview();
        _clearIssueFormCommand.RaiseCanExecuteChanged();
        RaiseCommandStates();
    }

    private async Task PreviewAllocationAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId, requireCreatePermission: true))
        {
            return;
        }

        IssueRequestDto? request = BuildIssueRequest();
        if (request is null)
        {
            _messageService.ShowError("Please select warehouse and add at least one line.");
            return;
        }

        IsBusy = true;
        PreviewStatusMessage = string.Empty;

        try
        {
            PreviewIssueAllocationResult previewResult = await ExecuteIssueServiceCallAsync(
                () => _issueService.PreviewLotAllocationAsync(
                    request,
                    actorUserId,
                    CancellationToken.None));

            ReplaceCollection(PreviewAllocations, previewResult.AllocationItems);

            if (!previewResult.IsSuccess)
            {
                if (HandleAccessOrSessionFailure(previewResult.ErrorMessage))
                {
                    return;
                }

                if (previewResult.Shortages.Count > 0)
                {
                    string shortageSummary = string.Join(
                        "; ",
                        previewResult.Shortages.Select(shortage =>
                            $"{shortage.Sku}: shortage {shortage.MissingQty:N3}"));
                    PreviewStatusMessage =
                        $"{previewResult.ErrorMessage ?? "Unable to allocate."} {shortageSummary}";
                }
                else
                {
                    PreviewStatusMessage = previewResult.ErrorMessage ?? "Unable to preview lot allocation.";
                }

                return;
            }

            PreviewStatusMessage =
                $"Preview success. Lots: {previewResult.AllocationItems.Count}, " +
                $"COGS: {previewResult.TotalCogsAmount:N2}, Amount: {previewResult.TotalSalesAmount:N2}.";
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            PreviewStatusMessage = $"Preview failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateIssueAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId, requireCreatePermission: true))
        {
            return;
        }

        IssueRequestDto? request = BuildIssueRequest();
        if (request is null)
        {
            _messageService.ShowError("Please select warehouse and add at least one line.");
            return;
        }

        IsBusy = true;

        try
        {
            CreateIssueResult result = await ExecuteIssueServiceCallAsync(
                () => _issueService.CreateIssueAsync(
                    request,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                if (HandleAccessOrSessionFailure(result.ErrorMessage))
                {
                    return;
                }

                _messageService.ShowError(result.ErrorMessage ?? "Failed to create issue draft.");
                return;
            }

            _messageService.ShowInfo(
                $"Created draft issue: {result.TransactionNo}\nStatus: DRAFT. Waiting manager to post.",
                "Issue Created");

            RequestLines.Clear();
            SelectedRequestLine = null;
            ClearPreview();
            AddQtyInput = string.Empty;
            AddUnitPriceInput = string.Empty;

            await RefreshDraftIssuesAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshDraftIssuesAsync()
    {
        await RefreshDraftIssuesAsync(setBusyState: true);
    }

    private async Task RefreshDraftIssuesAsync(bool setBusyState)
    {
        if (!_currentUserContext.IsAuthenticated)
        {
            return;
        }

        if (setBusyState && IsBusy)
        {
            return;
        }

        if (setBusyState)
        {
            IsBusy = true;
        }

        try
        {
            long? selectedId = SelectedDraftIssue?.TransactionId;
            IReadOnlyList<DraftIssueHeaderDto> drafts = await ExecuteIssueServiceCallAsync(
                () => _issueService.GetDraftIssuesAsync(CancellationToken.None));
            ReplaceCollection(DraftIssues, drafts);

            if (selectedId.HasValue)
            {
                SelectedDraftIssue = DraftIssues.FirstOrDefault(draft => draft.TransactionId == selectedId.Value)
                    ?? DraftIssues.FirstOrDefault();
            }
            else
            {
                SelectedDraftIssue = DraftIssues.FirstOrDefault();
            }

            if (DraftIssues.Count == 0)
            {
                DraftIssueLines.Clear();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unable to load draft issues: {ex.Message}";
        }
        finally
        {
            if (setBusyState)
            {
                IsBusy = false;
            }
        }
    }

    private async Task PostSelectedIssueAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId, requireCreatePermission: false))
        {
            return;
        }

        if (!HasPostIssuePermission)
        {
            _messageService.ShowError("You do not have permission to post issues.");
            return;
        }

        if (SelectedDraftIssue is null)
        {
            _messageService.ShowError("Please select a draft issue to post.");
            return;
        }

        if (!_messageService.Confirm(
                $"Post issue '{SelectedDraftIssue.TransactionNo}' now?\nThis will deduct stock by lot.",
                "Post Issue"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            PostIssueResult result = await ExecuteIssueServiceCallAsync(
                () => _issueService.PostIssueAsync(
                    SelectedDraftIssue.TransactionId,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                if (HandleAccessOrSessionFailure(result.ErrorMessage))
                {
                    return;
                }

                _messageService.ShowError(result.ErrorMessage ?? "Failed to post issue.");
                return;
            }

            _messageService.ShowInfo(
                $"Issue posted successfully: {result.TransactionNo}",
                "Issue Posted");

            await RefreshDraftIssuesAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelSelectedDraftIssueAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId, requireCreatePermission: false))
        {
            return;
        }

        if (!HasPostIssuePermission)
        {
            _messageService.ShowError("You do not have permission to cancel draft issues.");
            return;
        }

        if (SelectedDraftIssue is null)
        {
            _messageService.ShowError("Please select a draft issue to cancel.");
            return;
        }

        if (!_messageService.Confirm(
                $"Cancel draft issue '{SelectedDraftIssue.TransactionNo}'?\nReserved quantity will be released.",
                "Cancel Draft Issue"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            CancelIssueResult result = await ExecuteIssueServiceCallAsync(
                () => _issueService.CancelDraftIssueAsync(
                    SelectedDraftIssue.TransactionId,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                if (HandleAccessOrSessionFailure(result.ErrorMessage))
                {
                    return;
                }

                _messageService.ShowError(result.ErrorMessage ?? "Failed to cancel draft issue.");
                return;
            }

            _messageService.ShowInfo(
                $"Draft issue cancelled: {result.TransactionNo}",
                "Draft Cancelled");

            await RefreshDraftIssuesAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSelectedDraftIssueLinesAsync(int requestVersion)
    {
        if (SelectedDraftIssue is null || !_currentUserContext.IsAuthenticated)
        {
            if (requestVersion == _draftIssueLineRequestVersion)
            {
                DraftIssueLines.Clear();
            }

            return;
        }

        long transactionId = SelectedDraftIssue.TransactionId;

        try
        {
            IReadOnlyList<DraftIssueLineDto> lines = await ExecuteIssueServiceCallAsync(
                () => _issueService.GetDraftIssueLinesAsync(
                    transactionId,
                    CancellationToken.None));

            if (requestVersion != _draftIssueLineRequestVersion)
            {
                return;
            }

            ReplaceCollection(DraftIssueLines, lines);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (requestVersion != _draftIssueLineRequestVersion)
            {
                return;
            }

            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            if (requestVersion != _draftIssueLineRequestVersion)
            {
                return;
            }

            StatusMessage = $"Unable to load draft issue lines: {ex.Message}";
        }
    }

    private IssueRequestDto? BuildIssueRequest()
    {
        if (SelectedWarehouse is null || RequestLines.Count == 0)
        {
            return null;
        }

        return new IssueRequestDto
        {
            WarehouseId = SelectedWarehouse.WarehouseId,
            CustomerId = SelectedCustomer?.CustomerId,
            TransactionDate = TransactionDate.Date,
            ReferenceNo = string.IsNullOrWhiteSpace(ReferenceNo) ? null : ReferenceNo.Trim(),
            Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim(),
            Lines = RequestLines.Select(line => new IssueRequestLineDto
            {
                ProductId = line.ProductId,
                Qty = line.Qty,
                UnitPrice = line.UnitPrice
            }).ToList()
        };
    }

    private bool EnsureUserSessionForWriteAction(out int actorUserId, bool requireCreatePermission)
    {
        actorUserId = 0;

        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            _messageService.ShowError("Session expired. Please log in again.");
            TriggerLogout();
            return false;
        }

        if (requireCreatePermission && !HasCreateIssuePermission)
        {
            _messageService.ShowError("You do not have permission to create issue drafts.");
            return false;
        }

        actorUserId = _currentUserContext.UserId.Value;
        return true;
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

    private bool CanAddLine()
    {
        if (IsBusy || !HasCreateIssuePermission || SelectedProductToAdd is null)
        {
            return false;
        }

        if (!TryParsePositiveDecimal(AddQtyInput, out decimal qty))
        {
            return false;
        }

        if (qty > _selectedProductAvailableQty)
        {
            return false;
        }

        return true;
    }

    private bool CanRemoveSelectedLine()
    {
        return !IsBusy && HasCreateIssuePermission && SelectedRequestLine is not null;
    }

    private bool CanClearIssueForm()
    {
        return !IsBusy && HasCreateIssuePermission &&
            (RequestLines.Count > 0
             || PreviewAllocations.Count > 0
             || !string.IsNullOrWhiteSpace(ReferenceNo)
             || !string.IsNullOrWhiteSpace(Remarks));
    }

    private bool CanPreviewAllocation()
    {
        return !IsBusy
            && HasCreateIssuePermission
            && SelectedWarehouse is not null
            && RequestLines.Count > 0
            && _currentUserContext.UserId.HasValue;
    }

    private bool CanCreateIssue()
    {
        return !IsBusy
            && HasCreateIssuePermission
            && SelectedWarehouse is not null
            && RequestLines.Count > 0
            && _currentUserContext.UserId.HasValue;
    }

    private bool CanRefreshDraftIssues()
    {
        return !IsBusy && _currentUserContext.IsAuthenticated;
    }

    private bool CanPostSelectedIssue()
    {
        return !IsBusy
            && HasPostIssuePermission
            && _currentUserContext.UserId.HasValue
            && SelectedDraftIssue is not null;
    }

    private bool CanCancelSelectedIssue()
    {
        return !IsBusy
            && HasPostIssuePermission
            && _currentUserContext.UserId.HasValue
            && SelectedDraftIssue is not null;
    }

    private static bool TryParsePositiveDecimal(string input, out decimal value)
    {
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            && value > 0)
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseOptionalNonNegativeDecimal(string input, out decimal? value)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            value = null;
            return true;
        }

        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal parsedValue)
            && parsedValue >= 0)
        {
            value = parsedValue;
            return true;
        }

        value = null;
        return false;
    }

    private void ClearPreview()
    {
        PreviewAllocations.Clear();
        PreviewStatusMessage = string.Empty;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (T item in items)
        {
            collection.Add(item);
        }
    }

    private void SetInputAndRefresh(ref string field, string value)
    {
        if (SetProperty(ref field, value))
        {
            _addLineCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task RefreshSelectedProductAvailableQtyAsync()
    {
        int requestVersion = Interlocked.Increment(ref _availableQtyRequestVersion);

        if (!_currentUserContext.IsAuthenticated)
        {
            _selectedProductAvailableQty = 0;
            SelectedProductAvailableQtyText = "Available Qty: -";
            _addLineCommand.RaiseCanExecuteChanged();
            return;
        }

        WarehouseLookupDto? selectedWarehouse = SelectedWarehouse;
        ProductLookupDto? selectedProduct = SelectedProductToAdd;

        if (selectedWarehouse is null || selectedProduct is null)
        {
            _selectedProductAvailableQty = 0;
            SelectedProductAvailableQtyText = "Available Qty: -";
            _addLineCommand.RaiseCanExecuteChanged();
            return;
        }

        try
        {
            decimal availableQty = await ExecuteIssueServiceCallAsync(
                () => _issueService.GetAvailableQtyAsync(
                    selectedWarehouse.WarehouseId,
                    selectedProduct.ProductId,
                    TransactionDate,
                    CancellationToken.None));

            if (requestVersion != _availableQtyRequestVersion)
            {
                return;
            }

            _selectedProductAvailableQty = availableQty;
            string uom = string.IsNullOrWhiteSpace(selectedProduct.BaseUom)
                ? string.Empty
                : $" {selectedProduct.BaseUom}";
            SelectedProductAvailableQtyText = $"Available Qty: {availableQty:N3}{uom}";
            _addLineCommand.RaiseCanExecuteChanged();
        }
        catch (UnauthorizedAccessException ex)
        {
            if (requestVersion != _availableQtyRequestVersion)
            {
                return;
            }

            _selectedProductAvailableQty = 0;
            SelectedProductAvailableQtyText = "Available Qty: -";
            _addLineCommand.RaiseCanExecuteChanged();
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            if (requestVersion != _availableQtyRequestVersion)
            {
                return;
            }

            _selectedProductAvailableQty = 0;
            SelectedProductAvailableQtyText = $"Available Qty: unavailable ({ex.Message})";
            _addLineCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task<T> ExecuteIssueServiceCallAsync<T>(Func<Task<T>> action)
    {
        await _issueServiceCallGate.WaitAsync(CancellationToken.None);
        try
        {
            return await action();
        }
        finally
        {
            _issueServiceCallGate.Release();
        }
    }

    private void RaiseCommandStates()
    {
        _addLineCommand.RaiseCanExecuteChanged();
        _removeLineCommand.RaiseCanExecuteChanged();
        _clearIssueFormCommand.RaiseCanExecuteChanged();
        _previewAllocationCommand.RaiseCanExecuteChanged();
        _createIssueCommand.RaiseCanExecuteChanged();
        _refreshDraftIssuesCommand.RaiseCanExecuteChanged();
        _postIssueCommand.RaiseCanExecuteChanged();
        _cancelDraftIssueCommand.RaiseCanExecuteChanged();
    }
}
