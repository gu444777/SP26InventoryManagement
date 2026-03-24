namespace SP26InventoryManagement.DTOs;

public class CancelIssueResult : OperationResult
{
    public long? TransactionId { get; init; }

    public string? TransactionNo { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public static CancelIssueResult Success(long transactionId, string transactionNo, DateTime cancelledAtUtc)
    {
        return new CancelIssueResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            TransactionNo = transactionNo,
            CancelledAtUtc = cancelledAtUtc
        };
    }

    public new static CancelIssueResult Failure(string errorMessage)
    {
        return new CancelIssueResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
