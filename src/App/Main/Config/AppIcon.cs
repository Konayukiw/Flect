using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Optimizer.Main.Config;

internal static class AppIcon
{
    private const string ResourcePath = "pack://application:,,,/Assets/flect.ico";

    private static readonly ImageSource? Source = Load();

    public static void Register()
    {
        if (Source is null) return;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window && window.Icon is null) window.Icon = Source;
            }));
    }

    private static ImageSource? Load()
    {
        try
        {
            var frame = BitmapFrame.Create(new Uri(ResourcePath, UriKind.Absolute),
                                           BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
