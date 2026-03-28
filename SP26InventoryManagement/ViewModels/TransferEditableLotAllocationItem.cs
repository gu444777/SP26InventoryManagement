namespace SP26InventoryManagement.ViewModels;

public class TransferEditableLotAllocationItem : ObservableObject
{
    private decimal _selectedQty;

    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public long SourceProductLotId { get; init; }

    public string LotCode { get; init; } = string.Empty;

    public DateOnly ReceivedDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public decimal AvailableQtyBeforeAllocation { get; init; }

    public decimal SuggestedQty { get; init; }

    public decimal UnitCost { get; init; }

    public string AllocationRule { get; init; } = string.Empty;

    public decimal SelectedQty
    {
        get => _selectedQty;
        set => SetProperty(ref _selectedQty, decimal.Round(value, 3));
    }

    public string ExpiryDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "-";
}
