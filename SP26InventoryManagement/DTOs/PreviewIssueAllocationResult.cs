namespace SP26InventoryManagement.DTOs;

public class PreviewIssueAllocationResult : OperationResult
{
    public IReadOnlyList<IssueAllocationPreviewItemDto> AllocationItems { get; init; } = Array.Empty<IssueAllocationPreviewItemDto>();

    public IReadOnlyList<IssueAllocationShortageDto> Shortages { get; init; } = Array.Empty<IssueAllocationShortageDto>();

    public decimal TotalCogsAmount { get; init; }

    public decimal TotalSalesAmount { get; init; }

    public static PreviewIssueAllocationResult Success(
        IReadOnlyList<IssueAllocationPreviewItemDto> allocationItems,
        decimal totalCogsAmount,
        decimal totalSalesAmount)
    {
        return new PreviewIssueAllocationResult
        {
            IsSuccess = true,
            AllocationItems = allocationItems,
            TotalCogsAmount = totalCogsAmount,
            TotalSalesAmount = totalSalesAmount
        };
    }

    public static PreviewIssueAllocationResult Failure(
        string errorMessage,
        IReadOnlyList<IssueAllocationPreviewItemDto>? allocationItems = null,
        IReadOnlyList<IssueAllocationShortageDto>? shortages = null,
        decimal totalCogsAmount = 0,
        decimal totalSalesAmount = 0)
    {
        return new PreviewIssueAllocationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            AllocationItems = allocationItems ?? Array.Empty<IssueAllocationPreviewItemDto>(),
            Shortages = shortages ?? Array.Empty<IssueAllocationShortageDto>(),
            TotalCogsAmount = totalCogsAmount,
            TotalSalesAmount = totalSalesAmount
        };
    }
}
