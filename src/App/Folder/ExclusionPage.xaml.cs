using System.Windows.Controls;

namespace Flect.Folder;

public partial class ExclusionPage : UserControl
{
    public ExclusionPage() => InitializeComponent();

    internal ExclusionRules Value
    {
        get => Flect.Settings.Binding.ReadRules(FolderBox, FileBox, ExtensionBox);
        set => Flect.Settings.Binding.LoadRules(value, FolderBox, FileBox, ExtensionBox);
    }
}
