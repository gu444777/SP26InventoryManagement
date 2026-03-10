namespace SP26InventoryManagement.DTOs;

public class OperationResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public static OperationResult Success()
    {
        return new OperationResult { IsSuccess = true };
    }

    public static OperationResult Failure(string errorMessage)
    {
        return new OperationResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
