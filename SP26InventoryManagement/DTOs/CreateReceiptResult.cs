namespace SP26InventoryManagement.DTOs;

public class CreateReceiptResult : OperationResult
{
    public long TransactionId { get; init; }

    public string TransactionNo { get; init; } = string.Empty;

    public static CreateReceiptResult Success(long transactionId, string transactionNo)
    {
        return new CreateReceiptResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            TransactionNo = transactionNo
        };
    }

    public new static CreateReceiptResult Failure(string errorMessage)
    {
        return new CreateReceiptResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
