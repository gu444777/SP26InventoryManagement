namespace SP26InventoryManagement.DTOs;

public class IssueRequestLineDto
{
    public int ProductId { get; init; }

    public decimal Qty { get; init; }

    public decimal? UnitPrice { get; init; }
}
