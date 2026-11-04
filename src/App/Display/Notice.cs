using System.Windows;

namespace Flect.Display;

internal static class Notice
{
    public static void Info(string message) =>
        MessageBox.Show(message, Branding.Name, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string message) =>
        MessageBox.Show(message, Branding.Name, MessageBoxButton.OK, MessageBoxImage.Error);
}
