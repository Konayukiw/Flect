using System.Windows;

namespace Optimizer.Main.Common;

internal static class Report
{
    public static void Info(string message) =>
        MessageBox.Show(message, Branding.Name, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string message) =>
        MessageBox.Show(message, Branding.Name, MessageBoxButton.OK, MessageBoxImage.Error);
}
