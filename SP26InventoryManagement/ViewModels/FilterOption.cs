namespace SP26InventoryManagement.ViewModels;

public class FilterOption<T>
{
    public required string Label { get; init; }

    public required T Value { get; init; }

    public override string ToString()
    {
        return Label;
    }
}
