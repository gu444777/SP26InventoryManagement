using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class ReceiptManagementViewModel : ObservableObject
{
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string ManagerRoleCode = "MANAGER";
    private const string AdminRoleCode = "ADMIN";

    private readonly IReceiptService _receiptService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;

    private readonly RelayCommand _addLineCommand;
    private readonly RelayCommand _removeLineCommand;
    private readonly RelayCommand _clearReceiptFormCommand;
    private readonly AsyncRelayCommand _createReceiptCommand;
    private readonly AsyncRelayCommand _refreshDraftReceiptsCommand;
    private readonly AsyncRelayCommand _postReceiptCommand;
    private readonly AsyncRelayCommand _cancelDraftReceiptCommand;

    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _referenceNo = string.Empty;
    private string _remarks = string.Empty;
    private string _addLotCodeInput = string.Empty;
    private string _addQtyInput = string.Empty;
    private string _addUnitCostInput = string.Empty;
    private DateTime _transactionDate = DateTime.Today;
    private DateTime _addReceivedDate = DateTime.Today;
    private DateTime? _addExpiryDate;
    private SupplierLookupDto? _selectedSupplier;
    private WarehouseLookupDto? _selectedWarehouse;
    private ProductLookupDto? _selectedProductToAdd;
    private ReceiptRequestLineItem? _selectedRequestLine;
    private DraftReceiptHeaderDto? _selectedDraftReceipt;
    private int _draftReceiptLineRequestVersion;

    public ReceiptManagementViewModel(
        IReceiptService receiptService,
        CurrentUserContext currentUserContext,
        IMessageService messageService,
        IAuthService authService)
    {
        _receiptService = receiptService;
        _currentUserContext = currentUserContext;
        _messageService = messageService;
        _authService = authService;

        Warehouses = [];
        Suppliers = [];
        Products = [];
        RequestLines = [];
        DraftReceipts = [];
        DraftReceiptLines = [];

        _addLineCommand = new RelayCommand(AddLine, CanAddLine);
        _removeLineCommand = new RelayCommand(RemoveSelectedLine, CanRemoveSelectedLine);
        _clearReceiptFormCommand = new RelayCommand(ClearReceiptForm, CanClearReceiptForm);
        _createReceiptCommand = new AsyncRelayCommand(CreateReceiptAsync, CanCreateReceipt);
        _refreshDraftReceiptsCommand = new AsyncRelayCommand(RefreshDraftReceiptsAsync, CanRefreshDraftReceipts);
        _postReceiptCommand = new AsyncRelayCommand(PostSelectedReceiptAsync, CanPostSelectedReceipt);
        _cancelDraftReceiptCommand = new AsyncRelayCommand(CancelSelectedDraftReceiptAsync, CanCancelSelectedReceipt);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<WarehouseLookupDto> Warehouses { get; }

    public ObservableCollection<SupplierLookupDto> Suppliers { get; }

    public ObservableCollection<ProductLookupDto> Products { get; }

    public ObservableCollection<ReceiptRequestLineItem> RequestLines { get; }

    public ObservableCollection<DraftReceiptHeaderDto> DraftReceipts { get; }

    public ObservableCollection<DraftReceiptLineDto> DraftReceiptLines { get; }

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

    public SupplierLookupDto? SelectedSupplier
    {
        get => _selectedSupplier;
        set => SetProperty(ref _selectedSupplier, value);
    }

    public ProductLookupDto? SelectedProductToAdd
    {
        get => _selectedProductToAdd;
        set
        {
            if (SetProperty(ref _selectedProductToAdd, value))
            {
                _addLineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ReceiptRequestLineItem? SelectedRequestLine
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

    public DraftReceiptHeaderDto? SelectedDraftReceipt
    {
        get => _selectedDraftReceipt;
        set
        {
            if (SetProperty(ref _selectedDraftReceipt, value))
            {
                int requestVersion = Interlocked.Increment(ref _draftReceiptLineRequestVersion);
                _ = LoadSelectedDraftReceiptLinesAsync(requestVersion);
                _postReceiptCommand.RaiseCanExecuteChanged();
                _cancelDraftReceiptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ReferenceNo
    {
        get => _referenceNo;
        set
        {
            if (SetProperty(ref _referenceNo, value))
            {
                _clearReceiptFormCommand.RaiseCanExecuteChanged();
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
                _clearReceiptFormCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AddLotCodeInput
    {
        get => _addLotCodeInput;
        set
        {
            if (SetProperty(ref _addLotCodeInput, value))
            {
                _addLineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AddQtyInput
    {
        get => _addQtyInput;
        set
        {
            if (SetProperty(ref _addQtyInput, value))
            {
                _addLineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AddUnitCostInput
    {
        get => _addUnitCostInput;
        set
        {
            if (SetProperty(ref _addUnitCostInput, value))
            {
                _addLineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime TransactionDate
    {
        get => _transactionDate;
        set => SetProperty(ref _transactionDate, value);
    }

    public DateTime AddReceivedDate
    {
        get => _addReceivedDate;
        set => SetProperty(ref _addReceivedDate, value);
    }

    public DateTime? AddExpiryDate
    {
        get => _addExpiryDate;
        set => SetProperty(ref _addExpiryDate, value);
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

    public bool HasCreateReceiptPermission =>
        _currentUserContext.IsAuthenticated &&
        _currentUserContext.IsInRole(StaffRoleCode);

    public bool HasPostReceiptPermission =>
        _currentUserContext.IsAuthenticated &&
        (_currentUserContext.IsInRole(ManagerRoleCode) || _currentUserContext.IsInRole(AdminRoleCode));

    public string PermissionSummary =>
        $"Create Draft Receipt: {(HasCreateReceiptPermission ? "Allowed" : "Not allowed")}. " +
        $"Post Receipt: {(HasPostReceiptPermission ? "Allowed" : "Not allowed")}.";

    public ICommand AddLineCommand => _addLineCommand;

    public ICommand RemoveLineCommand => _removeLineCommand;

    public ICommand ClearReceiptFormCommand => _clearReceiptFormCommand;

    public ICommand CreateReceiptCommand => _createReceiptCommand;

    public ICommand RefreshDraftReceiptsCommand => _refreshDraftReceiptsCommand;

    public ICommand PostReceiptCommand => _postReceiptCommand;

    public ICommand CancelDraftReceiptCommand => _cancelDraftReceiptCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Session expired. Please log in again.");
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            IReadOnlyList<WarehouseLookupDto> warehouses = await _receiptService.GetActiveWarehousesAsync(ct);
            IReadOnlyList<SupplierLookupDto> suppliers = await _receiptService.GetActiveSuppliersAsync(ct);
            IReadOnlyList<ProductLookupDto> products = await _receiptService.GetActiveProductsAsync(ct);
            IReadOnlyList<DraftReceiptHeaderDto> draftReceipts = await _receiptService.GetDraftReceiptsAsync(ct);

            ReplaceCollection(Warehouses, warehouses);
            ReplaceCollection(Suppliers, suppliers);
            ReplaceCollection(Products, products);
            ReplaceCollection(DraftReceipts, draftReceipts);

            if (SelectedWarehouse is null && Warehouses.Count > 0)
            {
                SelectedWarehouse = Warehouses[0];
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddLine()
    {
        if (SelectedProductToAdd is null)
        {
            return;
        }

        if (!TryParsePositiveDecimal(AddQtyInput, out decimal qty))
        {
            StatusMessage = "Quantity must be greater than 0.";
            return;
        }

        if (!TryParseNonNegativeDecimal(AddUnitCostInput, out decimal unitCost))
        {
            StatusMessage = "Unit cost must be 0 or greater.";
            return;
        }

        string lotCode = AddLotCodeInput.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(lotCode))
        {
            StatusMessage = "Lot code is required.";
            return;
        }

        DateOnly receivedDate = DateOnly.FromDateTime(AddReceivedDate.Date);
        DateOnly? expiryDate = AddExpiryDate.HasValue ? DateOnly.FromDateTime(AddExpiryDate.Value.Date) : null;
        if (expiryDate.HasValue && expiryDate.Value < receivedDate)
        {
            StatusMessage = "Expiry date cannot be earlier than received date.";
            return;
        }

        bool lineExists = RequestLines.Any(line =>
            line.ProductId == SelectedProductToAdd.ProductId &&
            string.Equals(line.LotCode, lotCode, StringComparison.OrdinalIgnoreCase));
        if (lineExists)
        {
            StatusMessage = $"Lot '{lotCode}' already exists in the draft lines.";
            return;
        }

        RequestLines.Add(new ReceiptRequestLineItem
        {
            ProductId = SelectedProductToAdd.ProductId,
            Sku = SelectedProductToAdd.Sku,
            ProductName = SelectedProductToAdd.ProductName,
            LotCode = lotCode,
            Qty = decimal.Round(qty, 3),
            UnitCost = decimal.Round(unitCost, 4),
            ReceivedDate = receivedDate,
            ExpiryDate = expiryDate
        });

        AddLotCodeInput = string.Empty;
        AddQtyInput = string.Empty;
        AddUnitCostInput = string.Empty;
        AddExpiryDate = null;
        StatusMessage = string.Empty;
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
        RaiseCommandStates();
    }

    private void ClearReceiptForm()
    {
        SelectedSupplier = null;
        SelectedProductToAdd = null;
        AddLotCodeInput = string.Empty;
        AddQtyInput = string.Empty;
        AddUnitCostInput = string.Empty;
        ReferenceNo = string.Empty;
        Remarks = string.Empty;
        AddReceivedDate = DateTime.Today;
        AddExpiryDate = null;
        RequestLines.Clear();
        StatusMessage = string.Empty;
        RaiseCommandStates();
    }

    private async Task CreateReceiptAsync()
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            TriggerLogout();
            return;
        }

        ReceiptRequestDto? request = BuildReceiptRequest();
        if (request is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            CreateReceiptResult result = await _receiptService.CreateReceiptAsync(request, _currentUserContext.UserId.Value, CancellationToken.None);
            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage ?? "Unable to create receipt.";
                return;
            }

            _messageService.ShowInfo($"Draft receipt created successfully.\n\nDocument No: {result.TransactionNo}", "Receipt Created");
            ClearReceiptForm();
            await RefreshDraftReceiptsAsync();
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

    private async Task RefreshDraftReceiptsAsync()
    {
        IsBusy = true;

        try
        {
            IReadOnlyList<DraftReceiptHeaderDto> draftReceipts = await _receiptService.GetDraftReceiptsAsync(CancellationToken.None);
            ReplaceCollection(DraftReceipts, draftReceipts);

            if (SelectedDraftReceipt is not null)
            {
                SelectedDraftReceipt = DraftReceipts.FirstOrDefault(receipt => receipt.TransactionId == SelectedDraftReceipt.TransactionId);
            }
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

    private async Task PostSelectedReceiptAsync()
    {
        if (!_currentUserContext.UserId.HasValue || SelectedDraftReceipt is null)
        {
            return;
        }

        if (!_messageService.Confirm(
                $"Post receipt '{SelectedDraftReceipt.TransactionNo}' and update stock balances?",
                "Post Receipt"))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            PostReceiptResult result = await _receiptService.PostReceiptAsync(
                SelectedDraftReceipt.TransactionId,
                _currentUserContext.UserId.Value,
                CancellationToken.None);

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage ?? "Unable to post receipt.";
                return;
            }

            _messageService.ShowInfo(
                $"Receipt posted successfully.\n\nDocument No: {result.TransactionNo}\nPosted At (UTC): {result.PostedAtUtc:yyyy-MM-dd HH:mm:ss}",
                "Receipt Posted");

            DraftReceiptLines.Clear();
            SelectedDraftReceipt = null;
            await RefreshDraftReceiptsAsync();
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

    private async Task CancelSelectedDraftReceiptAsync()
    {
        if (!_currentUserContext.UserId.HasValue || SelectedDraftReceipt is null)
        {
            return;
        }

        if (!_messageService.Confirm(
                $"Cancel draft receipt '{SelectedDraftReceipt.TransactionNo}'?",
                "Cancel Receipt"))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            CancelReceiptResult result = await _receiptService.CancelDraftReceiptAsync(
                SelectedDraftReceipt.TransactionId,
                _currentUserContext.UserId.Value,
                CancellationToken.None);

            if (!result.IsSuccess)
            {
                StatusMessage = result.ErrorMessage ?? "Unable to cancel receipt.";
                return;
            }

            _messageService.ShowInfo(
                $"Draft receipt cancelled successfully.\n\nDocument No: {result.TransactionNo}",
                "Receipt Cancelled");

            DraftReceiptLines.Clear();
            SelectedDraftReceipt = null;
            await RefreshDraftReceiptsAsync();
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

    private async Task LoadSelectedDraftReceiptLinesAsync(int requestVersion)
    {
        if (SelectedDraftReceipt is null)
        {
            if (requestVersion == _draftReceiptLineRequestVersion)
            {
                DraftReceiptLines.Clear();
            }

            return;
        }

        try
        {
            IReadOnlyList<DraftReceiptLineDto> lines = await _receiptService.GetDraftReceiptLinesAsync(
                SelectedDraftReceipt.TransactionId,
                CancellationToken.None);

            if (requestVersion != _draftReceiptLineRequestVersion)
            {
                return;
            }

            ReplaceCollection(DraftReceiptLines, lines);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (requestVersion != _draftReceiptLineRequestVersion)
            {
                return;
            }

            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            if (requestVersion != _draftReceiptLineRequestVersion)
            {
                return;
            }

            StatusMessage = ex.Message;
        }
    }

    private ReceiptRequestDto? BuildReceiptRequest()
    {
        if (SelectedWarehouse is null)
        {
            StatusMessage = "Please select a warehouse.";
            return null;
        }

        if (RequestLines.Count == 0)
        {
            StatusMessage = "Please add at least one receipt line.";
            return null;
        }

        return new ReceiptRequestDto
        {
            WarehouseId = SelectedWarehouse.WarehouseId,
            SupplierId = SelectedSupplier?.SupplierId,
            TransactionDate = TransactionDate,
            ReferenceNo = ReferenceNo,
            Remarks = Remarks,
            Lines = RequestLines.Select(line => new ReceiptRequestLineDto
            {
                ProductId = line.ProductId,
                LotCode = line.LotCode,
                Qty = line.Qty,
                UnitCost = line.UnitCost,
                ReceivedDate = line.ReceivedDate,
                ExpiryDate = line.ExpiryDate
            }).ToArray()
        };
    }

    private void TriggerLogout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    private bool CanAddLine()
    {
        return !IsBusy
            && HasCreateReceiptPermission
            && SelectedProductToAdd is not null
            && !string.IsNullOrWhiteSpace(AddLotCodeInput)
            && TryParsePositiveDecimal(AddQtyInput, out _)
            && TryParseNonNegativeDecimal(AddUnitCostInput, out _);
    }

    private bool CanRemoveSelectedLine()
    {
        return !IsBusy && HasCreateReceiptPermission && SelectedRequestLine is not null;
    }

    private bool CanClearReceiptForm()
    {
        return !IsBusy && HasCreateReceiptPermission &&
               (RequestLines.Count > 0
                || !string.IsNullOrWhiteSpace(ReferenceNo)
                || !string.IsNullOrWhiteSpace(Remarks)
                || !string.IsNullOrWhiteSpace(AddLotCodeInput)
                || !string.IsNullOrWhiteSpace(AddQtyInput)
                || !string.IsNullOrWhiteSpace(AddUnitCostInput)
                || SelectedSupplier is not null
                || SelectedProductToAdd is not null);
    }

    private bool CanCreateReceipt()
    {
        return !IsBusy && HasCreateReceiptPermission && SelectedWarehouse is not null && RequestLines.Count > 0;
    }

    private bool CanRefreshDraftReceipts()
    {
        return !IsBusy && HasPostReceiptPermission;
    }

    private bool CanPostSelectedReceipt()
    {
        return !IsBusy && HasPostReceiptPermission && SelectedDraftReceipt is not null;
    }

    private bool CanCancelSelectedReceipt()
    {
        return !IsBusy && HasPostReceiptPermission && SelectedDraftReceipt is not null;
    }

    private void RaiseCommandStates()
    {
        _addLineCommand.RaiseCanExecuteChanged();
        _removeLineCommand.RaiseCanExecuteChanged();
        _clearReceiptFormCommand.RaiseCanExecuteChanged();
        _createReceiptCommand.RaiseCanExecuteChanged();
        _refreshDraftReceiptsCommand.RaiseCanExecuteChanged();
        _postReceiptCommand.RaiseCanExecuteChanged();
        _cancelDraftReceiptCommand.RaiseCanExecuteChanged();
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }
    }

    private static bool TryParsePositiveDecimal(string? input, out decimal value)
    {
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0)
        {
            return true;
        }

        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value > 0)
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParseNonNegativeDecimal(string? input, out decimal value)
    {
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0)
        {
            return true;
        }

        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value >= 0)
        {
            return true;
        }

        value = 0;
        return false;
    }
}
