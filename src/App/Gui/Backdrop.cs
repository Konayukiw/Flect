using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

using Optimizer.Main.Config;

namespace Optimizer.Gui;

internal static class Backdrop
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private const int DwmwaSystemBackdropType = 38;

    private const int DwmsbtMainWindow = 2;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Apply(Window window)
    {
        if (window.IsLoaded)
        {
            ApplyCore(window);
            return;
        }

        window.SourceInitialized += (_, _) => ApplyCore(window);
    }

    private static void ApplyCore(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        Set(hwnd, DwmwaUseImmersiveDarkMode, Theme.IsDark ? 1 : 0);

        if (Environment.OSVersion.Version.Build >= 22000)
        {
            Set(hwnd, DwmwaSystemBackdropType, DwmsbtMainWindow);
        }
    }

    private static void Set(IntPtr hwnd, int attr, int value) =>
        DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int));
}