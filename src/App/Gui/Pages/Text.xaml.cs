using System.Windows;
using System.Windows.Controls;

namespace Optimizer.Gui.Pages;

public partial class Text : UserControl, ISettingsPage
{
    public Text()
    {
        InitializeComponent();

        Fields.Fill(ComparisonBox,
            (JsonKeyOrder.Ordinal, "settings.text.json.comparison.ordinal"),
            (JsonKeyOrder.IgnoreCase, "settings.text.json.comparison.ignoreCase"),
            (JsonKeyOrder.Natural, "settings.text.json.comparison.natural"));
    }

    void ISettingsPage.Load(Settings settings)
    {
        var text = settings.Text;

        SjisReplaceBox.IsChecked = text.SjisFallback == SjisFallback.ReplaceAndWarn;
        SjisSubstituteBox.IsChecked = text.SjisFallback == SjisFallback.Substitute;
        SjisFailBox.IsChecked = text.SjisFallback == SjisFallback.Fail;
        SubstituteBox.Text = text.SjisSubstitute;
        UpdateSubstituteState();

        Fields.Select(ComparisonBox, text.JsonSort.Comparison);
        DescendingBox.IsChecked = text.JsonSort.Descending;
        RecursiveBox.IsChecked = text.JsonSort.Recursive;
        SortArraysBox.IsChecked = text.JsonSort.SortPrimitiveArrays;
        PinnedKeysBox.Text = text.JsonSort.PinnedKeys;
    }

    void ISettingsPage.Store(Settings settings)
    {
        var text = settings.Text;

        text.SjisFallback = SjisSubstituteBox.IsChecked == true ? SjisFallback.Substitute
                          : SjisFailBox.IsChecked == true ? SjisFallback.Fail
                          : SjisFallback.ReplaceAndWarn;

        var substitute = SubstituteBox.Text.Trim();
        text.SjisSubstitute = substitute.Length > 0 ? substitute[..1] : "?";

        text.JsonSort.Comparison = Fields.Selected(ComparisonBox, text.JsonSort.Comparison);
        text.JsonSort.Descending = DescendingBox.IsChecked == true;
        text.JsonSort.Recursive = RecursiveBox.IsChecked == true;
        text.JsonSort.SortPrimitiveArrays = SortArraysBox.IsChecked == true;
        text.JsonSort.PinnedKeys = string.Join('\n', ExclusionFilter.Split(PinnedKeysBox.Text));
    }

    private void OnSjisModeChanged(object sender, RoutedEventArgs e) => UpdateSubstituteState();

    private void UpdateSubstituteState()
    {
        if (SubstituteBox is null) return;

        bool enabled = SjisSubstituteBox.IsChecked == true;
        SubstituteBox.IsEnabled = enabled;
        SubstituteLabel.Opacity = enabled ? 1 : 0.45;
    }
}
