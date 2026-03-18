namespace SP26InventoryManagement.DTOs;

public class CreateIssueResult : OperationResult
{
    public long? TransactionId { get; init; }

    public string? TransactionNo { get; init; }

    public static CreateIssueResult Success(long transactionId, string transactionNo)
    {
        return new CreateIssueResult
        {
            IsSuccess = true,
            TransactionId = transactionId,
            TransactionNo = transactionNo
        };
    }

    public new static CreateIssueResult Failure(string errorMessage)
    {
        return new CreateIssueResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
