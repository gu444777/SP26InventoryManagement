namespace SP26InventoryManagement.DTOs;

public class ConfirmDestinationReceiptResult : OperationResult
{
    public long? TransferOrderId { get; init; }

    public string? TransferNo { get; init; }

    public DateTime? ConfirmedAtUtc { get; init; }

    public static ConfirmDestinationReceiptResult Success(long transferOrderId, string transferNo, DateTime confirmedAtUtc)
    {
        return new ConfirmDestinationReceiptResult
        {
            IsSuccess = true,
            TransferOrderId = transferOrderId,
            TransferNo = transferNo,
            ConfirmedAtUtc = confirmedAtUtc
        };
    }

    public new static ConfirmDestinationReceiptResult Failure(string errorMessage)
    {
        return new ConfirmDestinationReceiptResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
