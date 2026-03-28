namespace SP26InventoryManagement.DTOs;

public class CancelReceiptResult : OperationResult
{
    public long? TransactionId { get; init; }

    public string? TransactionNo { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public static CancelReceiptResult Success(long transactionId, string transactionNo, DateTime cancelledAtUtc)
    {
        return new CancelReceiptResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            TransactionNo = transactionNo,
            CancelledAtUtc = cancelledAtUtc
        };
    }

    public new static CancelReceiptResult Failure(string errorMessage)
    {
        return new CancelReceiptResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
