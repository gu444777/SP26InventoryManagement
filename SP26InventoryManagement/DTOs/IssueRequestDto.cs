namespace SP26InventoryManagement.DTOs;

public class IssueRequestDto
{
    public int WarehouseId { get; init; }

    public int? CustomerId { get; init; }

    public DateTime TransactionDate { get; init; }

    public string? ReferenceNo { get; init; }

    public string? Remarks { get; init; }

    public IReadOnlyCollection<IssueRequestLineDto> Lines { get; init; } = Array.Empty<IssueRequestLineDto>();
}
