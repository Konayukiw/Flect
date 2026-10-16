using System.IO;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

namespace Optimizer.Gui.Pages;

public partial class Code : UserControl, ISettingsPage
{
    public Code()
    {
        InitializeComponent();
    }

    void ISettingsPage.Load(Settings settings)
    {
        var code = settings.Code;

        DecompileOutputBox.Text = code.Decompile.OutputPath;
    }

    void ISettingsPage.Store(Settings settings)
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
            catch (Exception) {}
        }

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            DecompileOutputBox.Text = dialog.FolderName;
        }
    }
}
