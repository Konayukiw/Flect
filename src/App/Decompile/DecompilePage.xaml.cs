using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Flect.Decompile;

public partial class DecompilePage : UserControl, ISettings
{
    public DecompilePage()
    {
        InitializeComponent();
    }

    void ISettings.Load(Preferences settings)
    {
        var code = settings.Code;

        DecompileOutputBox.Text = code.Decompile.OutputPath;
    }

    void ISettings.Store(Preferences settings)
    {
        var code = settings.Code;

        code.Decompile.OutputPath = DecompileOutputBox.Text.Trim();
    }

    private void OnBrowseDecompile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();

        var current = DecompileOutputBox.Text.Trim();
        if (current.Length > 0)
        {
            try
            {
                var full = Path.GetFullPath(current);
                if (Directory.Exists(full)) dialog.InitialDirectory = full;
            }
            catch (Exception) { /* ignore */ }
        }

        if (dialog.ShowDialog(Window.GetWindow(this)) is true)
        {
            DecompileOutputBox.Text = dialog.FolderName;
        }
    }
}
