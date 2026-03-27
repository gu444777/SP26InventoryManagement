namespace SP26InventoryManagement.DTOs;

public class CreateTransferResult : OperationResult
{
    public long? TransferOrderId { get; init; }

    public string? TransferNo { get; init; }

    public static CreateTransferResult Success(long transferOrderId, string transferNo)
    {
        return new CreateTransferResult
        {
            IsSuccess = true,
            TransferOrderId = transferOrderId,
            TransferNo = transferNo
        };
    }

    public new static CreateTransferResult Failure(string errorMessage)
    {
        return new CreateTransferResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
