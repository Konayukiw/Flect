using System.Windows.Controls;
using Optimizer.Main;

namespace Optimizer.Gui;

public partial class ExclusionEditor : UserControl
{
    public ExclusionEditor() => InitializeComponent();

    internal ExclusionRules Value
    {
        get => Fields.ReadRules(FolderBox, FileBox, ExtensionBox);
        set => Fields.LoadRules(value, FolderBox, FileBox, ExtensionBox);
    }
}
