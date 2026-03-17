using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SP26InventoryManagement.Services;

public class MessageService : IMessageService
{
    public void ShowInfo(string message, string title = "Information")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool Confirm(string message, string title = "Confirm")
    {
        MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public void ShowPasswordWithCopy(string username, string password, string title = "Temporary Password")
    {
        var usernameText = new TextBlock
        {
            Text = $"Username: {username}",
            Margin = new Thickness(0, 0, 0, 8),
            FontWeight = FontWeights.SemiBold
        };

        var hintText = new TextBlock
        {
            Text = "Save this password now. It is shown only once.",
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Brushes.DimGray
        };

        var passwordTextBox = new TextBox
        {
            Text = password,
            IsReadOnly = true,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14
        };

        var copiedText = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = Brushes.ForestGreen
        };

        var copyButton = new Button
        {
            Content = "Copy Password",
            MinWidth = 120,
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 90,
            Padding = new Thickness(10, 6),
            IsDefault = true
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonPanel.Children.Add(copyButton);
        buttonPanel.Children.Add(closeButton);

        var rootPanel = new StackPanel
        {
            Margin = new Thickness(16)
        };
        rootPanel.Children.Add(usernameText);
        rootPanel.Children.Add(hintText);
        rootPanel.Children.Add(passwordTextBox);
        rootPanel.Children.Add(copiedText);
        rootPanel.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 250,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = rootPanel
        };

        dialog.Owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive) ?? Application.Current.MainWindow;

        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(password);
            copiedText.Text = "Password copied to clipboard.";
            passwordTextBox.SelectAll();
            passwordTextBox.Focus();
        };

        closeButton.Click += (_, _) => dialog.Close();

        dialog.ShowDialog();
    }
}
