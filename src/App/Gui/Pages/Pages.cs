using System.Globalization;
using System.Windows.Controls;

namespace Optimizer.Gui.Pages;

public sealed record Choice(object Value, string Label)
{
    public override string ToString() => Label;
}

internal interface ISettingsPage
{
    void Load(Settings settings);

    void Store(Settings settings);
}

internal static class Fields
{
    public static void Fill<T>(ComboBox box, params (T Value, string Key)[] items)
        where T : struct
    {
        box.DisplayMemberPath = nameof(Choice.Label);
        box.ItemsSource = items.Select(item => new Choice(item.Value, Loc.T(item.Key))).ToList();
    }

    public static void FillRaw<T>(ComboBox box, params (T Value, string Label)[] items)
        where T : struct
    {
        box.DisplayMemberPath = nameof(Choice.Label);
        box.ItemsSource = items.Select(item => new Choice(item.Value, item.Label)).ToList();
    }

    public static void FillText(ComboBox box, IEnumerable<(string Value, string Label)> items)
    {
        box.DisplayMemberPath = nameof(Choice.Label);
        box.ItemsSource = items.Select(item => new Choice(item.Value, item.Label)).ToList();
    }

    public static void ShowOptionalBytes(TextBox box, long value) =>
        box.Text = value > 0 ? Formatting.Bytes(value) : string.Empty;

    public static long ReadOptionalBytes(TextBox box, long fallback)
    {
        if (box.Text.Trim().Length == 0) return 0;
        return Formatting.TryParseBytes(box.Text, out var value) ? value : fallback;
    }

    public static void Select<T>(ComboBox box, T value)
    {
        foreach (var item in box.Items.OfType<Choice>())
        {
            if (Equals(item.Value, value))
            {
                box.SelectedItem = item;
                return;
            }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    public static T Selected<T>(ComboBox box, T fallback) =>
        box.SelectedItem is Choice choice && choice.Value is T value ? value : fallback;

    public static int Int(TextBox box, int fallback, int minimum = int.MinValue,
                          int maximum = int.MaxValue) =>
        int.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    public static double Number(TextBox box, double fallback, double minimum, double maximum) =>
        double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;

    public static long Bytes(TextBox box, long fallback) =>
        Formatting.TryParseBytes(box.Text, out var value) ? value : fallback;

    public static void ShowNumber(TextBox box, double value) =>
        box.Text = value.ToString("0.####", CultureInfo.CurrentCulture);

    public static void ShowInt(TextBox box, int value) =>
        box.Text = value.ToString(CultureInfo.CurrentCulture);

    public static void ShowBytes(TextBox box, long value) => box.Text = Formatting.Bytes(value);

    public static void LoadRules(ExclusionRules rules, TextBox folders, TextBox files,
                                 TextBox extensions)
    {
        folders.Text = rules.Folders;
        files.Text = rules.Files;
        extensions.Text = rules.Extensions;
    }

    public static ExclusionRules ReadRules(TextBox folders, TextBox files, TextBox extensions) =>
        new()
        {
            Folders = Tidy(folders.Text),
            Files = Tidy(files.Text),
            Extensions = Tidy(extensions.Text),
        };

    private static string Tidy(string text) => string.Join('\n', ExclusionFilter.Split(text));
}
