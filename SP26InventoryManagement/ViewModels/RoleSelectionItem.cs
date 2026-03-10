namespace SP26InventoryManagement.ViewModels;

public class RoleSelectionItem : ObservableObject
{
    private bool _isSelected;

    public int RoleId { get; init; }

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Display => $"{RoleName} ({RoleCode})";
}
