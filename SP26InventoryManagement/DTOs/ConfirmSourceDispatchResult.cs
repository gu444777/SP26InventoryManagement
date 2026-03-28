namespace SP26InventoryManagement.DTOs;

public class ConfirmSourceDispatchResult : OperationResult
{
    public long? TransferOrderId { get; init; }

    public string? TransferNo { get; init; }

    public DateTime? ConfirmedAtUtc { get; init; }

    public static ConfirmSourceDispatchResult Success(long transferOrderId, string transferNo, DateTime confirmedAtUtc)
    {
        return new ConfirmSourceDispatchResult
        {
            IsSuccess = true,
            TransferOrderId = transferOrderId,
            TransferNo = transferNo,
            ConfirmedAtUtc = confirmedAtUtc
        };
    }

    public new static ConfirmSourceDispatchResult Failure(string errorMessage)
    {
        return new ConfirmSourceDispatchResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
