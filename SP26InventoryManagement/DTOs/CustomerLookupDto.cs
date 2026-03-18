namespace SP26InventoryManagement.DTOs;

public class CustomerLookupDto
{
    public int CustomerId { get; init; }

    public string CustomerCode { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string Display => $"{CustomerCode} - {CustomerName}";
}
