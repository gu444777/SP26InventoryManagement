namespace SP26InventoryManagement.DTOs;

public class PostIssueResult : OperationResult
{
    public long? TransactionId { get; init; }

    public string? TransactionNo { get; init; }

    public DateTime? PostedAtUtc { get; init; }

    public static PostIssueResult Success(long transactionId, string transactionNo, DateTime postedAtUtc)
    {
        return new PostIssueResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            TransactionNo = transactionNo,
            PostedAtUtc = postedAtUtc
        };
    }

    public new static PostIssueResult Failure(string errorMessage)
    {
        return new PostIssueResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
