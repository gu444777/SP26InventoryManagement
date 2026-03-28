namespace SP26InventoryManagement.DTOs;

public class PreviewCreateTransferLotSuggestionResult : OperationResult
{
    public IReadOnlyList<TransferLotSuggestionItemDto> SuggestionItems { get; init; } = Array.Empty<TransferLotSuggestionItemDto>();

    public IReadOnlyList<TransferSuggestionShortageDto> Shortages { get; init; } = Array.Empty<TransferSuggestionShortageDto>();

    public static PreviewCreateTransferLotSuggestionResult Success(
        IReadOnlyList<TransferLotSuggestionItemDto> suggestionItems)
    {
        return new PreviewCreateTransferLotSuggestionResult
        {
            IsSuccess = true,
            SuggestionItems = suggestionItems
        };
    }

    public static PreviewCreateTransferLotSuggestionResult Failure(
        string errorMessage,
        IReadOnlyList<TransferLotSuggestionItemDto>? suggestionItems = null,
        IReadOnlyList<TransferSuggestionShortageDto>? shortages = null)
    {
        return new PreviewCreateTransferLotSuggestionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            SuggestionItems = suggestionItems ?? Array.Empty<TransferLotSuggestionItemDto>(),
            Shortages = shortages ?? Array.Empty<TransferSuggestionShortageDto>()
        };
    }
}
