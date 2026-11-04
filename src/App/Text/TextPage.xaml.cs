using System.Windows;
using System.Windows.Controls;

namespace Flect.Text;

public partial class TextPage : UserControl, ISettings
{
    public TextPage()
    {
        InitializeComponent();

        Flect.Settings.Binding.Fill(ComparisonBox,
            (JsonKeyOrder.Ordinal, "settings.text.json.comparison.ordinal"),
            (JsonKeyOrder.IgnoreCase, "settings.text.json.comparison.ignoreCase"),
            (JsonKeyOrder.Natural, "settings.text.json.comparison.natural"));
    }

    void ISettings.Load(Preferences settings)
    {
        var text = settings.Text;

        SjisReplaceBox.IsChecked = text.SjisFallback == ShiftJISFallback.ReplaceAndWarn;
        SjisSubstituteBox.IsChecked = text.SjisFallback == ShiftJISFallback.Substitute;
        SjisFailBox.IsChecked = text.SjisFallback == ShiftJISFallback.Fail;
        SubstituteBox.Text = text.SjisSubstitute;
        UpdateSubstituteState();

        Flect.Settings.Binding.Select(ComparisonBox, text.JsonSort.Comparison);
        DescendingBox.IsChecked = text.JsonSort.Descending;
        RecursiveBox.IsChecked = text.JsonSort.Recursive;
        SortArraysBox.IsChecked = text.JsonSort.SortPrimitiveArrays;
        PinnedKeysBox.Text = text.JsonSort.PinnedKeys;
    }

    void ISettings.Store(Preferences settings)
    {
        var text = settings.Text;

        text.SjisFallback = SjisSubstituteBox.IsChecked == true ? ShiftJISFallback.Substitute
                          : SjisFailBox.IsChecked == true ? ShiftJISFallback.Fail
                          : ShiftJISFallback.ReplaceAndWarn;

        var substitute = SubstituteBox.Text.Trim();
        text.SjisSubstitute = substitute.Length > 0 ? substitute[..1] : "?";

        text.JsonSort.Comparison = Flect.Settings.Binding.Selected(ComparisonBox, text.JsonSort.Comparison);
        text.JsonSort.Descending = DescendingBox.IsChecked == true;
        text.JsonSort.Recursive = RecursiveBox.IsChecked == true;
        text.JsonSort.SortPrimitiveArrays = SortArraysBox.IsChecked == true;
        text.JsonSort.PinnedKeys = string.Join('\n', Flect.Folder.Exclusion.Split(PinnedKeysBox.Text));
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
