namespace SP26InventoryManagement.DTOs;

public class SupplierLookupDto
{
    public int SupplierId { get; init; }

    public string SupplierCode { get; init; } = string.Empty;

    public string SupplierName { get; init; } = string.Empty;

    public string Display => $"{SupplierCode} - {SupplierName}";
}
