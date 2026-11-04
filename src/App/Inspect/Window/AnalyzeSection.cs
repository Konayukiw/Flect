using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Flect.Inspect.Window;

internal sealed class AnalyzeSection
{
    internal AnalyzeSection(string title, IReadOnlyList<AnalyzeRow> rows)
    {
        Title = title;
        Rows = rows;
    }

    public string Title { get; }
    public IReadOnlyList<AnalyzeRow> Rows { get; }
}
