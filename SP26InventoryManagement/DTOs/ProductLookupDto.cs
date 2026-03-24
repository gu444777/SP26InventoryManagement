namespace SP26InventoryManagement.DTOs;

public class ProductLookupDto
{
    public int ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string BaseUom { get; init; } = string.Empty;

    public string Display => $"{Sku} - {ProductName}";
}
