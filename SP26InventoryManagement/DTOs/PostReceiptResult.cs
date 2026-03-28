namespace SP26InventoryManagement.DTOs;

public class PostReceiptResult : OperationResult
{
    public long TransactionId { get; init; }

    public string TransactionNo { get; init; } = string.Empty;

    public DateTime PostedAtUtc { get; init; }

    public static PostReceiptResult Success(long transactionId, string transactionNo, DateTime postedAtUtc)
    {
        return new PostReceiptResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            TransactionNo = transactionNo,
            PostedAtUtc = postedAtUtc
        };
    }

    public new static PostReceiptResult Failure(string errorMessage)
    {
        return new PostReceiptResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
