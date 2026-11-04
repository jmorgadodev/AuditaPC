using System.Windows;

namespace AuditaPC.App.Views;

public static class PhotoDescriptionDialog
{
    public static string? Ask(Window? owner, string title, string prompt) =>
        TextPromptDialog.Ask(owner, title, prompt);
}
