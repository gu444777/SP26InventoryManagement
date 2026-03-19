namespace SP26InventoryManagement.Services;

public interface IMessageService
{
    void ShowInfo(string message, string title = "Information");

    void ShowError(string message, string title = "Error");

    bool Confirm(string message, string title = "Confirm");

    void ShowPasswordWithCopy(string username, string password, string title = "Temporary Password");
}
