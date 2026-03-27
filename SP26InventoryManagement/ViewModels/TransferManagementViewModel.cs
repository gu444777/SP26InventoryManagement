using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class TransferManagementViewModel : ObservableObject
{
    private const string StaffRoleCode = "WAREHOUSE_STAFF";
    private const string AdminRoleCode = "ADMIN";

    private readonly ITransferService _transferService;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IMessageService _messageService;
    private readonly IAuthService _authService;
    private readonly SemaphoreSlim _serviceCallGate = new(1, 1);

    private readonly RelayCommand _addLineCommand;
    private readonly RelayCommand _removeLineCommand;
    private readonly RelayCommand _clearCreateFormCommand;
    private readonly AsyncRelayCommand _previewSuggestionCommand;
    private readonly AsyncRelayCommand _createTransferCommand;
    private readonly AsyncRelayCommand _refreshSourceDispatchQueueCommand;
    private readonly AsyncRelayCommand _confirmSourceDispatchCommand;
    private readonly AsyncRelayCommand _cancelCreatedTransferCommand;
    private readonly AsyncRelayCommand _refreshDestinationReceiptQueueCommand;
    private readonly AsyncRelayCommand _confirmDestinationReceiptCommand;

    private bool _isBusy;
    private string _createStatusMessage = string.Empty;
    private string _dispatchStatusMessage = string.Empty;
    private string _receiptStatusMessage = string.Empty;
    private string _addQtyInput = string.Empty;
    private string _remarks = string.Empty;
    private DateTime _requestDate = DateTime.Today;
    private DateTime? _requiredDate = DateTime.Today.AddDays(1);

    private int _destinationWarehouseRequestVersion;
    private int _sourceDetailRequestVersion;
    private int _destinationDetailRequestVersion;

    private WarehouseLookupDto? _selectedSourceWarehouse;
    private WarehouseLookupDto? _selectedDestinationWarehouse;
    private ProductLookupDto? _selectedProductToAdd;
    private TransferCreateLineItem? _selectedCreateLine;
    private TransferQueueItemDto? _selectedSourceDispatchTransfer;
    private TransferQueueItemDto? _selectedDestinationReceiptTransfer;

    public TransferManagementViewModel(
        ITransferService transferService,
        CurrentUserContext currentUserContext,
        IMessageService messageService,
        IAuthService authService)
    {
        _transferService = transferService;
        _currentUserContext = currentUserContext;
        _messageService = messageService;
        _authService = authService;

        SourceWarehouses = [];
        DestinationWarehouses = [];
        Products = [];
        CreateLines = [];
        PreviewLotAllocations = [];
        SourceDispatchQueue = [];
        DestinationReceiptQueue = [];
        SourceDispatchDetailLines = [];
        SourceDispatchDetailLots = [];
        DestinationReceiptDetailLines = [];
        DestinationReceiptDetailLots = [];

        _addLineCommand = new RelayCommand(AddLine, CanAddLine);
        _removeLineCommand = new RelayCommand(RemoveSelectedLine, CanRemoveSelectedLine);
        _clearCreateFormCommand = new RelayCommand(ClearCreateForm, CanClearCreateForm);
        _previewSuggestionCommand = new AsyncRelayCommand(PreviewSuggestionAsync, CanPreviewSuggestion);
        _createTransferCommand = new AsyncRelayCommand(CreateTransferAsync, CanCreateTransfer);
        _refreshSourceDispatchQueueCommand = new AsyncRelayCommand(RefreshSourceDispatchQueueAsync, CanRefreshSourceDispatchQueue);
        _confirmSourceDispatchCommand = new AsyncRelayCommand(ConfirmSourceDispatchAsync, CanConfirmSourceDispatch);
        _cancelCreatedTransferCommand = new AsyncRelayCommand(CancelCreatedTransferAsync, CanCancelCreatedTransfer);
        _refreshDestinationReceiptQueueCommand = new AsyncRelayCommand(RefreshDestinationReceiptQueueAsync, CanRefreshDestinationReceiptQueue);
        _confirmDestinationReceiptCommand = new AsyncRelayCommand(ConfirmDestinationReceiptAsync, CanConfirmDestinationReceipt);
    }

    public event Action? LogoutRequested;

    public ObservableCollection<WarehouseLookupDto> SourceWarehouses { get; }

    public ObservableCollection<WarehouseLookupDto> DestinationWarehouses { get; }

    public ObservableCollection<ProductLookupDto> Products { get; }

    public ObservableCollection<TransferCreateLineItem> CreateLines { get; }

    public ObservableCollection<TransferEditableLotAllocationItem> PreviewLotAllocations { get; }

    public ObservableCollection<TransferQueueItemDto> SourceDispatchQueue { get; }

    public ObservableCollection<TransferQueueItemDto> DestinationReceiptQueue { get; }

    public ObservableCollection<TransferDetailLineDto> SourceDispatchDetailLines { get; }

    public ObservableCollection<TransferDetailLotDto> SourceDispatchDetailLots { get; }

    public ObservableCollection<TransferDetailLineDto> DestinationReceiptDetailLines { get; }

    public ObservableCollection<TransferDetailLotDto> DestinationReceiptDetailLots { get; }

    public WarehouseLookupDto? SelectedSourceWarehouse
    {
        get => _selectedSourceWarehouse;
        set
        {
            if (SetProperty(ref _selectedSourceWarehouse, value))
            {
                ClearPreview();
                RaiseCommandStates();
                _ = RefreshDestinationWarehousesAsync();
            }
        }
    }

    public WarehouseLookupDto? SelectedDestinationWarehouse
    {
        get => _selectedDestinationWarehouse;
        set
        {
            if (SetProperty(ref _selectedDestinationWarehouse, value))
            {
                ClearPreview();
                RaiseCommandStates();
            }
        }
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

    public TransferCreateLineItem? SelectedCreateLine
    {
        get => _selectedCreateLine;
        set
        {
            if (SetProperty(ref _selectedCreateLine, value))
            {
                _removeLineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public TransferQueueItemDto? SelectedSourceDispatchTransfer
    {
        get => _selectedSourceDispatchTransfer;
        set
        {
            if (SetProperty(ref _selectedSourceDispatchTransfer, value))
            {
                int requestVersion = Interlocked.Increment(ref _sourceDetailRequestVersion);
                _ = LoadSourceDispatchDetailAsync(requestVersion);
                _confirmSourceDispatchCommand.RaiseCanExecuteChanged();
                _cancelCreatedTransferCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public TransferQueueItemDto? SelectedDestinationReceiptTransfer
    {
        get => _selectedDestinationReceiptTransfer;
        set
        {
            if (SetProperty(ref _selectedDestinationReceiptTransfer, value))
            {
                int requestVersion = Interlocked.Increment(ref _destinationDetailRequestVersion);
                _ = LoadDestinationReceiptDetailAsync(requestVersion);
                _confirmDestinationReceiptCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AddQtyInput
    {
        get => _addQtyInput;
        set => SetInputAndRefresh(ref _addQtyInput, value);
    }

    public string Remarks
    {
        get => _remarks;
        set
        {
            if (SetProperty(ref _remarks, value))
            {
                _clearCreateFormCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DateTime RequestDate
    {
        get => _requestDate;
        set
        {
            if (SetProperty(ref _requestDate, value))
            {
                ClearPreview();
                RaiseCommandStates();
            }
        }
    }

    public DateTime? RequiredDate
    {
        get => _requiredDate;
        set => SetProperty(ref _requiredDate, value);
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

    public string CreateStatusMessage
    {
        get => _createStatusMessage;
        private set => SetProperty(ref _createStatusMessage, value);
    }

    public string DispatchStatusMessage
    {
        get => _dispatchStatusMessage;
        private set => SetProperty(ref _dispatchStatusMessage, value);
    }

    public string ReceiptStatusMessage
    {
        get => _receiptStatusMessage;
        private set => SetProperty(ref _receiptStatusMessage, value);
    }

    public bool HasTransferPermission =>
        _currentUserContext.IsAuthenticated &&
        (_currentUserContext.IsInRole(StaffRoleCode) || _currentUserContext.IsInRole(AdminRoleCode));

    public bool CanChooseSourceWarehouse => _currentUserContext.IsInRole(AdminRoleCode);

    public string PermissionSummary =>
        $"Transfer: {(HasTransferPermission ? "Allowed" : "Not allowed")}. " +
        $"Source warehouse selector: {(CanChooseSourceWarehouse ? "Editable (ADMIN)" : "Fixed by assignment")}.";

    public ICommand AddLineCommand => _addLineCommand;

    public ICommand RemoveLineCommand => _removeLineCommand;

    public ICommand ClearCreateFormCommand => _clearCreateFormCommand;

    public ICommand PreviewSuggestionCommand => _previewSuggestionCommand;

    public ICommand CreateTransferCommand => _createTransferCommand;

    public ICommand RefreshSourceDispatchQueueCommand => _refreshSourceDispatchQueueCommand;

    public ICommand ConfirmSourceDispatchCommand => _confirmSourceDispatchCommand;

    public ICommand CancelCreatedTransferCommand => _cancelCreatedTransferCommand;

    public ICommand RefreshDestinationReceiptQueueCommand => _refreshDestinationReceiptQueueCommand;

    public ICommand ConfirmDestinationReceiptCommand => _confirmDestinationReceiptCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!HasTransferPermission || !_currentUserContext.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Only WAREHOUSE_STAFF or ADMIN can open transfer screen.");
        }

        IsBusy = true;
        CreateStatusMessage = string.Empty;
        DispatchStatusMessage = string.Empty;
        ReceiptStatusMessage = string.Empty;

        try
        {
            IReadOnlyList<WarehouseLookupDto> sourceWarehouses = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetAllowedSourceWarehousesAsync(_currentUserContext.UserId.Value, ct));
            IReadOnlyList<ProductLookupDto> products = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetActiveProductsAsync(ct));

            ReplaceCollection(SourceWarehouses, sourceWarehouses);
            ReplaceCollection(Products, products);

            SelectedSourceWarehouse ??= SourceWarehouses.FirstOrDefault();
            SelectedProductToAdd = Products.FirstOrDefault();

            await RefreshDestinationWarehousesAsync();
            await RefreshSourceDispatchQueueAsync(setBusyState: false);
            await RefreshDestinationReceiptQueueAsync(setBusyState: false);

            OnPropertyChanged(nameof(HasTransferPermission));
            OnPropertyChanged(nameof(CanChooseSourceWarehouse));
            OnPropertyChanged(nameof(PermissionSummary));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddLine()
    {
        if (!HasTransferPermission)
        {
            _messageService.ShowError("You do not have permission to create transfer orders.");
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

        if (CreateLines.Any(line => line.ProductId == SelectedProductToAdd.ProductId))
        {
            _messageService.ShowError("This product already exists in transfer lines.");
            return;
        }

        CreateLines.Add(new TransferCreateLineItem
        {
            ProductId = SelectedProductToAdd.ProductId,
            Sku = SelectedProductToAdd.Sku,
            ProductName = SelectedProductToAdd.ProductName,
            RequestedQty = decimal.Round(qty, 3)
        });

        AddQtyInput = string.Empty;
        ClearPreview();
        RaiseCommandStates();
    }

    private void RemoveSelectedLine()
    {
        if (SelectedCreateLine is null)
        {
            return;
        }

        CreateLines.Remove(SelectedCreateLine);
        SelectedCreateLine = null;
        ClearPreview();
        RaiseCommandStates();
    }

    private void ClearCreateForm()
    {
        CreateLines.Clear();
        SelectedCreateLine = null;
        AddQtyInput = string.Empty;
        Remarks = string.Empty;
        ClearPreview();
        RaiseCommandStates();
    }

    private async Task PreviewSuggestionAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId))
        {
            return;
        }

        if (SelectedSourceWarehouse is null || SelectedDestinationWarehouse is null || CreateLines.Count == 0)
        {
            _messageService.ShowError("Please select source, destination and add at least one product line.");
            return;
        }

        IsBusy = true;
        CreateStatusMessage = string.Empty;

        try
        {
            TransferSuggestionRequestDto request = new()
            {
                SourceWarehouseId = SelectedSourceWarehouse.WarehouseId,
                DestinationWarehouseId = SelectedDestinationWarehouse.WarehouseId,
                RequestDate = RequestDate.Date,
                Lines = CreateLines.Select(line => new TransferSuggestionLineDto
                {
                    ProductId = line.ProductId,
                    RequestedQty = line.RequestedQty
                }).ToList()
            };

            PreviewCreateTransferLotSuggestionResult result = await ExecuteTransferServiceCallAsync(
                () => _transferService.PreviewCreateTransferLotSuggestionAsync(
                    request,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                ReplacePreviewAllocations([]);
                if (result.Shortages.Count > 0)
                {
                    string shortageSummary = string.Join(
                        "; ",
                        result.Shortages.Select(shortage =>
                            $"{shortage.Sku}: shortage {shortage.MissingQty:N3}"));
                    CreateStatusMessage = $"{result.ErrorMessage} {shortageSummary}";
                }
                else
                {
                    CreateStatusMessage = result.ErrorMessage ?? "Unable to preview transfer lot suggestions.";
                }

                return;
            }

            ReplacePreviewAllocations(result.SuggestionItems.Select(suggestion => new TransferEditableLotAllocationItem
            {
                ProductId = suggestion.ProductId,
                Sku = suggestion.Sku,
                ProductName = suggestion.ProductName,
                SourceProductLotId = suggestion.SourceProductLotId,
                LotCode = suggestion.LotCode,
                ReceivedDate = suggestion.ReceivedDate,
                ExpiryDate = suggestion.ExpiryDate,
                AvailableQtyBeforeAllocation = suggestion.AvailableQtyBeforeAllocation,
                SuggestedQty = suggestion.SuggestedQty,
                UnitCost = suggestion.UnitCost,
                AllocationRule = suggestion.AllocationRule,
                SelectedQty = suggestion.SuggestedQty
            }));

            CreateStatusMessage = $"Preview success. Suggested lot rows: {PreviewLotAllocations.Count}.";
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            CreateStatusMessage = $"Preview failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CreateTransferAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId))
        {
            return;
        }

        if (!ValidateManualAllocations(out string errorMessage))
        {
            _messageService.ShowError(errorMessage);
            return;
        }

        if (SelectedSourceWarehouse is null || SelectedDestinationWarehouse is null)
        {
            _messageService.ShowError("Please select source and destination warehouse.");
            return;
        }

        IsBusy = true;

        try
        {
            TransferCreateRequestDto request = BuildCreateTransferRequest();

            CreateTransferResult result = await ExecuteTransferServiceCallAsync(
                () => _transferService.CreateTransferAsync(
                    request,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                _messageService.ShowError(result.ErrorMessage ?? "Failed to create transfer order.");
                CreateStatusMessage = result.ErrorMessage ?? "Failed to create transfer order.";
                return;
            }

            _messageService.ShowInfo(
                $"Created transfer order: {result.TransferNo}\nStatus: CREATED",
                "Transfer Created");

            CreateLines.Clear();
            SelectedCreateLine = null;
            AddQtyInput = string.Empty;
            Remarks = string.Empty;
            ClearPreview();

            await RefreshSourceDispatchQueueAsync(setBusyState: false);
            await RefreshDestinationReceiptQueueAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshSourceDispatchQueueAsync()
    {
        await RefreshSourceDispatchQueueAsync(setBusyState: true);
    }

    private async Task RefreshSourceDispatchQueueAsync(bool setBusyState)
    {
        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
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
            long? selectedId = SelectedSourceDispatchTransfer?.TransferOrderId;

            IReadOnlyList<TransferQueueItemDto> queue = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetSourceDispatchQueueAsync(_currentUserContext.UserId.Value, CancellationToken.None));

            ReplaceCollection(SourceDispatchQueue, queue);
            SelectedSourceDispatchTransfer = selectedId.HasValue
                ? SourceDispatchQueue.FirstOrDefault(item => item.TransferOrderId == selectedId.Value) ?? SourceDispatchQueue.FirstOrDefault()
                : SourceDispatchQueue.FirstOrDefault();

            if (SourceDispatchQueue.Count == 0)
            {
                SourceDispatchDetailLines.Clear();
                SourceDispatchDetailLots.Clear();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            DispatchStatusMessage = $"Unable to refresh source dispatch queue: {ex.Message}";
        }
        finally
        {
            if (setBusyState)
            {
                IsBusy = false;
            }
        }
    }

    private async Task RefreshDestinationReceiptQueueAsync()
    {
        await RefreshDestinationReceiptQueueAsync(setBusyState: true);
    }

    private async Task RefreshDestinationReceiptQueueAsync(bool setBusyState)
    {
        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
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
            long? selectedId = SelectedDestinationReceiptTransfer?.TransferOrderId;

            IReadOnlyList<TransferQueueItemDto> queue = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetDestinationReceiptQueueAsync(_currentUserContext.UserId.Value, CancellationToken.None));

            ReplaceCollection(DestinationReceiptQueue, queue);
            SelectedDestinationReceiptTransfer = selectedId.HasValue
                ? DestinationReceiptQueue.FirstOrDefault(item => item.TransferOrderId == selectedId.Value) ?? DestinationReceiptQueue.FirstOrDefault()
                : DestinationReceiptQueue.FirstOrDefault();

            if (DestinationReceiptQueue.Count == 0)
            {
                DestinationReceiptDetailLines.Clear();
                DestinationReceiptDetailLots.Clear();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            ReceiptStatusMessage = $"Unable to refresh destination receipt queue: {ex.Message}";
        }
        finally
        {
            if (setBusyState)
            {
                IsBusy = false;
            }
        }
    }

    private async Task ConfirmSourceDispatchAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId))
        {
            return;
        }

        if (SelectedSourceDispatchTransfer is null)
        {
            _messageService.ShowError("Please select a transfer order to dispatch.");
            return;
        }

        if (!_messageService.Confirm(
                $"Confirm source dispatch for '{SelectedSourceDispatchTransfer.TransferNo}'?\nStock will be deducted at source warehouse.",
                "Confirm Source Dispatch"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            ConfirmSourceDispatchResult result = await ExecuteTransferServiceCallAsync(
                () => _transferService.ConfirmSourceDispatchAsync(
                    SelectedSourceDispatchTransfer.TransferOrderId,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                DispatchStatusMessage = result.ErrorMessage ?? "Failed to confirm source dispatch.";
                _messageService.ShowError(DispatchStatusMessage);
                return;
            }

            _messageService.ShowInfo(
                $"Source dispatch confirmed: {result.TransferNo}",
                "Source Dispatch Confirmed");

            await RefreshSourceDispatchQueueAsync(setBusyState: false);
            await RefreshDestinationReceiptQueueAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelCreatedTransferAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId))
        {
            return;
        }

        if (SelectedSourceDispatchTransfer is null)
        {
            _messageService.ShowError("Please select a transfer order to cancel.");
            return;
        }

        if (!_messageService.Confirm(
                $"Cancel transfer '{SelectedSourceDispatchTransfer.TransferNo}'?\nReserved quantity will be released.",
                "Cancel Transfer"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            CancelTransferResult result = await ExecuteTransferServiceCallAsync(
                () => _transferService.CancelCreatedTransferAsync(
                    SelectedSourceDispatchTransfer.TransferOrderId,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                DispatchStatusMessage = result.ErrorMessage ?? "Failed to cancel transfer.";
                _messageService.ShowError(DispatchStatusMessage);
                return;
            }

            _messageService.ShowInfo(
                $"Transfer cancelled: {result.TransferNo}",
                "Transfer Cancelled");

            await RefreshSourceDispatchQueueAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConfirmDestinationReceiptAsync()
    {
        if (!EnsureUserSessionForWriteAction(out int actorUserId))
        {
            return;
        }

        if (SelectedDestinationReceiptTransfer is null)
        {
            _messageService.ShowError("Please select a transfer order to receive.");
            return;
        }

        if (!_messageService.Confirm(
                $"Confirm destination receipt for '{SelectedDestinationReceiptTransfer.TransferNo}'?\nStock will be added at destination warehouse.",
                "Confirm Destination Receipt"))
        {
            return;
        }

        IsBusy = true;

        try
        {
            ConfirmDestinationReceiptResult result = await ExecuteTransferServiceCallAsync(
                () => _transferService.ConfirmDestinationReceiptAsync(
                    SelectedDestinationReceiptTransfer.TransferOrderId,
                    actorUserId,
                    CancellationToken.None));

            if (!result.IsSuccess)
            {
                ReceiptStatusMessage = result.ErrorMessage ?? "Failed to confirm destination receipt.";
                _messageService.ShowError(ReceiptStatusMessage);
                return;
            }

            _messageService.ShowInfo(
                $"Destination receipt confirmed: {result.TransferNo}",
                "Destination Receipt Confirmed");

            await RefreshDestinationReceiptQueueAsync(setBusyState: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshDestinationWarehousesAsync()
    {
        int requestVersion = Interlocked.Increment(ref _destinationWarehouseRequestVersion);

        WarehouseLookupDto? sourceWarehouse = SelectedSourceWarehouse;
        if (sourceWarehouse is null)
        {
            if (requestVersion == _destinationWarehouseRequestVersion)
            {
                DestinationWarehouses.Clear();
                SelectedDestinationWarehouse = null;
            }
            return;
        }

        try
        {
            IReadOnlyList<WarehouseLookupDto> destinationWarehouses = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetActiveDestinationWarehousesAsync(
                    sourceWarehouse.WarehouseId,
                    CancellationToken.None));

            if (requestVersion != _destinationWarehouseRequestVersion)
            {
                return;
            }

            int? selectedDestinationId = SelectedDestinationWarehouse?.WarehouseId;
            ReplaceCollection(DestinationWarehouses, destinationWarehouses);
            SelectedDestinationWarehouse = selectedDestinationId.HasValue
                ? DestinationWarehouses.FirstOrDefault(warehouse => warehouse.WarehouseId == selectedDestinationId.Value) ?? DestinationWarehouses.FirstOrDefault()
                : DestinationWarehouses.FirstOrDefault();
        }
        catch (UnauthorizedAccessException ex)
        {
            if (requestVersion != _destinationWarehouseRequestVersion)
            {
                return;
            }

            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            if (requestVersion != _destinationWarehouseRequestVersion)
            {
                return;
            }

            CreateStatusMessage = $"Unable to load destination warehouses: {ex.Message}";
        }
    }

    private async Task LoadSourceDispatchDetailAsync(int requestVersion)
    {
        if (SelectedSourceDispatchTransfer is null || !_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            if (requestVersion == _sourceDetailRequestVersion)
            {
                SourceDispatchDetailLines.Clear();
                SourceDispatchDetailLots.Clear();
            }

            return;
        }

        long transferOrderId = SelectedSourceDispatchTransfer.TransferOrderId;
        try
        {
            TransferDetailDto? detail = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetTransferDetailAsync(
                    transferOrderId,
                    _currentUserContext.UserId.Value,
                    CancellationToken.None));

            if (requestVersion != _sourceDetailRequestVersion)
            {
                return;
            }

            if (detail is null)
            {
                SourceDispatchDetailLines.Clear();
                SourceDispatchDetailLots.Clear();
                return;
            }

            ReplaceCollection(SourceDispatchDetailLines, detail.Lines);
            ReplaceCollection(SourceDispatchDetailLots, detail.Lines.SelectMany(line => line.Lots));
        }
        catch (UnauthorizedAccessException ex)
        {
            if (requestVersion != _sourceDetailRequestVersion)
            {
                return;
            }

            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            if (requestVersion != _sourceDetailRequestVersion)
            {
                return;
            }

            DispatchStatusMessage = $"Unable to load dispatch detail: {ex.Message}";
        }
    }

    private async Task LoadDestinationReceiptDetailAsync(int requestVersion)
    {
        if (SelectedDestinationReceiptTransfer is null || !_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            if (requestVersion == _destinationDetailRequestVersion)
            {
                DestinationReceiptDetailLines.Clear();
                DestinationReceiptDetailLots.Clear();
            }

            return;
        }

        long transferOrderId = SelectedDestinationReceiptTransfer.TransferOrderId;
        try
        {
            TransferDetailDto? detail = await ExecuteTransferServiceCallAsync(
                () => _transferService.GetTransferDetailAsync(
                    transferOrderId,
                    _currentUserContext.UserId.Value,
                    CancellationToken.None));

            if (requestVersion != _destinationDetailRequestVersion)
            {
                return;
            }

            if (detail is null)
            {
                DestinationReceiptDetailLines.Clear();
                DestinationReceiptDetailLots.Clear();
                return;
            }

            ReplaceCollection(DestinationReceiptDetailLines, detail.Lines);
            ReplaceCollection(DestinationReceiptDetailLots, detail.Lines.SelectMany(line => line.Lots));
        }
        catch (UnauthorizedAccessException ex)
        {
            if (requestVersion != _destinationDetailRequestVersion)
            {
                return;
            }

            _messageService.ShowError(ex.Message);
            TriggerLogout();
        }
        catch (Exception ex)
        {
            if (requestVersion != _destinationDetailRequestVersion)
            {
                return;
            }

            ReceiptStatusMessage = $"Unable to load receipt detail: {ex.Message}";
        }
    }

    private TransferCreateRequestDto BuildCreateTransferRequest()
    {
        Dictionary<int, List<TransferLotSelectionDto>> lotSelectionByProduct = PreviewLotAllocations
            .Where(item => item.SelectedQty > 0)
            .GroupBy(item => item.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => new TransferLotSelectionDto
                {
                    SourceProductLotId = item.SourceProductLotId,
                    Qty = decimal.Round(item.SelectedQty, 3)
                }).ToList());

        return new TransferCreateRequestDto
        {
            SourceWarehouseId = SelectedSourceWarehouse!.WarehouseId,
            DestinationWarehouseId = SelectedDestinationWarehouse!.WarehouseId,
            RequestDate = RequestDate.Date,
            RequiredDate = RequiredDate.HasValue ? DateOnly.FromDateTime(RequiredDate.Value.Date) : null,
            Remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks.Trim(),
            Lines = CreateLines.Select(line => new TransferCreateLineDto
            {
                ProductId = line.ProductId,
                RequestedQty = line.RequestedQty,
                LotSelections = lotSelectionByProduct.TryGetValue(line.ProductId, out List<TransferLotSelectionDto>? lotSelections)
                    ? lotSelections
                    : []
            }).ToList()
        };
    }

    private bool EnsureUserSessionForWriteAction(out int actorUserId)
    {
        actorUserId = 0;

        if (!_currentUserContext.IsAuthenticated || !_currentUserContext.UserId.HasValue)
        {
            _messageService.ShowError("Session expired. Please log in again.");
            TriggerLogout();
            return false;
        }

        if (!HasTransferPermission)
        {
            _messageService.ShowError("You do not have permission to access transfer module.");
            return false;
        }

        actorUserId = _currentUserContext.UserId.Value;
        return true;
    }

    private void TriggerLogout()
    {
        _authService.Logout();
        LogoutRequested?.Invoke();
    }

    private bool CanAddLine()
    {
        return !IsBusy
            && HasTransferPermission
            && SelectedProductToAdd is not null
            && TryParsePositiveDecimal(AddQtyInput, out _);
    }

    private bool CanRemoveSelectedLine()
    {
        return !IsBusy && HasTransferPermission && SelectedCreateLine is not null;
    }

    private bool CanClearCreateForm()
    {
        return !IsBusy && HasTransferPermission &&
               (CreateLines.Count > 0 || PreviewLotAllocations.Count > 0 || !string.IsNullOrWhiteSpace(Remarks));
    }

    private bool CanPreviewSuggestion()
    {
        return !IsBusy
            && HasTransferPermission
            && _currentUserContext.UserId.HasValue
            && SelectedSourceWarehouse is not null
            && SelectedDestinationWarehouse is not null
            && CreateLines.Count > 0;
    }

    private bool CanCreateTransfer()
    {
        return !IsBusy
            && HasTransferPermission
            && _currentUserContext.UserId.HasValue
            && SelectedSourceWarehouse is not null
            && SelectedDestinationWarehouse is not null
            && CreateLines.Count > 0
            && PreviewLotAllocations.Count > 0
            && IsManualAllocationValidForCanExecute();
    }

    private bool CanRefreshSourceDispatchQueue()
    {
        return !IsBusy && HasTransferPermission && _currentUserContext.UserId.HasValue;
    }

    private bool CanConfirmSourceDispatch()
    {
        return !IsBusy
            && HasTransferPermission
            && _currentUserContext.UserId.HasValue
            && SelectedSourceDispatchTransfer is not null;
    }

    private bool CanCancelCreatedTransfer()
    {
        return !IsBusy
            && HasTransferPermission
            && _currentUserContext.UserId.HasValue
            && SelectedSourceDispatchTransfer is not null;
    }

    private bool CanRefreshDestinationReceiptQueue()
    {
        return !IsBusy && HasTransferPermission && _currentUserContext.UserId.HasValue;
    }

    private bool CanConfirmDestinationReceipt()
    {
        return !IsBusy
            && HasTransferPermission
            && _currentUserContext.UserId.HasValue
            && SelectedDestinationReceiptTransfer is not null;
    }

    private bool ValidateManualAllocations(out string errorMessage)
    {
        errorMessage = string.Empty;

        if (PreviewLotAllocations.Count == 0)
        {
            errorMessage = "Please preview lot allocation before creating transfer.";
            return false;
        }

        foreach (TransferEditableLotAllocationItem allocation in PreviewLotAllocations)
        {
            if (allocation.SelectedQty < 0)
            {
                errorMessage = $"Selected quantity cannot be negative for lot '{allocation.LotCode}'.";
                return false;
            }

            if (allocation.SelectedQty > allocation.AvailableQtyBeforeAllocation)
            {
                errorMessage =
                    $"Selected quantity cannot be greater than available quantity for lot '{allocation.LotCode}' ({allocation.AvailableQtyBeforeAllocation:N3}).";
                return false;
            }
        }

        Dictionary<int, decimal> selectedQtyByProduct = PreviewLotAllocations
            .Where(allocation => allocation.SelectedQty > 0)
            .GroupBy(allocation => allocation.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.SelectedQty));

        foreach (TransferCreateLineItem line in CreateLines)
        {
            if (!selectedQtyByProduct.TryGetValue(line.ProductId, out decimal selectedQty) || selectedQty <= 0)
            {
                errorMessage = $"Please allocate lots for product '{line.Sku}'.";
                return false;
            }

            if (!IsQtyEqual(selectedQty, line.RequestedQty))
            {
                errorMessage =
                    $"Allocated lot quantity for product '{line.Sku}' must equal requested quantity ({line.RequestedQty:N3}).";
                return false;
            }
        }

        return true;
    }

    private bool IsManualAllocationValidForCanExecute()
    {
        if (PreviewLotAllocations.Count == 0 || CreateLines.Count == 0)
        {
            return false;
        }

        foreach (TransferEditableLotAllocationItem allocation in PreviewLotAllocations)
        {
            if (allocation.SelectedQty < 0 || allocation.SelectedQty > allocation.AvailableQtyBeforeAllocation)
            {
                return false;
            }
        }

        Dictionary<int, decimal> selectedQtyByProduct = PreviewLotAllocations
            .Where(allocation => allocation.SelectedQty > 0)
            .GroupBy(allocation => allocation.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.SelectedQty));

        return CreateLines.All(line =>
            selectedQtyByProduct.TryGetValue(line.ProductId, out decimal selectedQty) &&
            selectedQty > 0 &&
            IsQtyEqual(selectedQty, line.RequestedQty));
    }

    private void ReplacePreviewAllocations(IEnumerable<TransferEditableLotAllocationItem> items)
    {
        foreach (TransferEditableLotAllocationItem existing in PreviewLotAllocations)
        {
            existing.PropertyChanged -= OnPreviewAllocationChanged;
        }

        PreviewLotAllocations.Clear();
        foreach (TransferEditableLotAllocationItem item in items)
        {
            item.PropertyChanged += OnPreviewAllocationChanged;
            PreviewLotAllocations.Add(item);
        }

        _createTransferCommand.RaiseCanExecuteChanged();
        _clearCreateFormCommand.RaiseCanExecuteChanged();
    }

    private void OnPreviewAllocationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransferEditableLotAllocationItem.SelectedQty))
        {
            _createTransferCommand.RaiseCanExecuteChanged();
        }
    }

    private static bool TryParsePositiveDecimal(string input, out decimal value)
    {
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out value) && value > 0)
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsQtyEqual(decimal left, decimal right)
    {
        return Math.Abs(left - right) <= 0.0005m;
    }

    private void ClearPreview()
    {
        ReplacePreviewAllocations([]);
        CreateStatusMessage = string.Empty;
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

    private async Task<T> ExecuteTransferServiceCallAsync<T>(Func<Task<T>> action)
    {
        await _serviceCallGate.WaitAsync(CancellationToken.None);
        try
        {
            return await action();
        }
        finally
        {
            _serviceCallGate.Release();
        }
    }

    private void RaiseCommandStates()
    {
        _addLineCommand.RaiseCanExecuteChanged();
        _removeLineCommand.RaiseCanExecuteChanged();
        _clearCreateFormCommand.RaiseCanExecuteChanged();
        _previewSuggestionCommand.RaiseCanExecuteChanged();
        _createTransferCommand.RaiseCanExecuteChanged();
        _refreshSourceDispatchQueueCommand.RaiseCanExecuteChanged();
        _confirmSourceDispatchCommand.RaiseCanExecuteChanged();
        _cancelCreatedTransferCommand.RaiseCanExecuteChanged();
        _refreshDestinationReceiptQueueCommand.RaiseCanExecuteChanged();
        _confirmDestinationReceiptCommand.RaiseCanExecuteChanged();
    }
}
