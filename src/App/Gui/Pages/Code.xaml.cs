using System.IO;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

namespace Optimizer.Gui.Pages;

public partial class Code : UserControl, ISettingsPage
{
    private const string DefaultEnvKeyPath = "C:/Users/";

    public Code()
    {
        InitializeComponent();
    }

    void ISettingsPage.Load(Settings settings)
    {
        var code = settings.Code;

        DecompileOutputBox.Text = code.Decompile.OutputPath;

        var obf = code.Obfuscate;

        RenameBox.IsChecked = obf.Rename;
        RenameClassesBox.IsChecked = obf.RenameClasses;
        HideImportsBox.IsChecked = obf.HideImports;
        ValueCalcBox.IsChecked = obf.ValueCalc;
        EncryptStringsBox.IsChecked = obf.EncryptStrings;

        Fields.ShowInt(NameMinBox, obf.NameLengthMin);
        Fields.ShowInt(NameMaxBox, obf.NameLengthMax);
        Fields.ShowInt(JunkBox, obf.JunkFrequency);
        SeedBox.Text = obf.Seed;

        EnvKeyBox.IsChecked = obf.UseEnvKey;
        XorBox.IsChecked = obf.UseXor;
        SwapBox.IsChecked = obf.UseSwap;
        RotateBox.IsChecked = obf.UseRotate;
        ShuffleBox.IsChecked = obf.UseByteShuffle;
        Base85Box.IsChecked = obf.UseBase85;
        EnvKeyPathBox.Text = obf.EnvKeyPath;
        Fields.ShowInt(LayersBox, obf.EnvKeyLayers);

        AttrIndirectBox.IsChecked = obf.AttrIndirect;
        BuiltinsTableBox.IsChecked = obf.BuiltinsTable;
        BoolNoneBox.IsChecked = obf.BoolNoneExpr;
        IntegerBox.IsChecked = obf.IntegerEncode;
        ArgReuseBox.IsChecked = obf.ScopeArgReuse;
        WrapJunkBox.IsChecked = obf.WrapJunkIf;
        Fields.ShowInt(PoolBox, obf.ArgPoolSize);
    }

    void ISettingsPage.Store(Settings settings)
    {
        var code = settings.Code;

        code.Decompile.OutputPath = DecompileOutputBox.Text.Trim();

        var obf = code.Obfuscate;

        obf.Rename = RenameBox.IsChecked == true;
        obf.RenameClasses = RenameClassesBox.IsChecked == true;
        obf.HideImports = HideImportsBox.IsChecked == true;
        obf.ValueCalc = ValueCalcBox.IsChecked == true;
        obf.EncryptStrings = EncryptStringsBox.IsChecked == true;

        obf.NameLengthMin = Fields.Int(NameMinBox, obf.NameLengthMin, 1, 64);
        obf.NameLengthMax = Math.Max(Fields.Int(NameMaxBox, obf.NameLengthMax, 1, 64),
                                     obf.NameLengthMin);
        obf.JunkFrequency = Fields.Int(JunkBox, obf.JunkFrequency, 0, 10);
        obf.Seed = SeedBox.Text.Trim();

        obf.UseEnvKey = EnvKeyBox.IsChecked == true;
        obf.UseXor = XorBox.IsChecked == true;
        obf.UseSwap = SwapBox.IsChecked == true;
        obf.UseRotate = RotateBox.IsChecked == true;
        obf.UseByteShuffle = ShuffleBox.IsChecked == true;
        obf.UseBase85 = Base85Box.IsChecked == true;
        obf.EnvKeyPath = EnvKeyPathBox.Text.Trim().Length > 0
            ? EnvKeyPathBox.Text.Trim()
            : DefaultEnvKeyPath;
        obf.EnvKeyLayers = Fields.Int(LayersBox, obf.EnvKeyLayers, 0, 64);

        obf.AttrIndirect = AttrIndirectBox.IsChecked == true;
        obf.BuiltinsTable = BuiltinsTableBox.IsChecked == true;
        obf.BoolNoneExpr = BoolNoneBox.IsChecked == true;
        obf.IntegerEncode = IntegerBox.IsChecked == true;
        obf.ScopeArgReuse = ArgReuseBox.IsChecked == true;
        obf.WrapJunkIf = WrapJunkBox.IsChecked == true;
        obf.ArgPoolSize = Fields.Int(PoolBox, obf.ArgPoolSize, 2, 64);
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
