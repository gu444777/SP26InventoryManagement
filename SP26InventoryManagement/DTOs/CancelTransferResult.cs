namespace SP26InventoryManagement.DTOs;

public class CancelTransferResult : OperationResult
{
    public long? TransferOrderId { get; init; }

    public string? TransferNo { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public static CancelTransferResult Success(long transferOrderId, string transferNo, DateTime cancelledAtUtc)
    {
        return new CancelTransferResult
        {
            IsSuccess = true,
            TransferOrderId = transferOrderId,
            TransferNo = transferNo,
            CancelledAtUtc = cancelledAtUtc
        };
    }

    public new static CancelTransferResult Failure(string errorMessage)
    {
        return new CancelTransferResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
