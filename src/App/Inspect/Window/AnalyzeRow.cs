using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Flect.Inspect.Window;

internal sealed class AnalyzeRow
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    public string? Key { get; init; }
    public bool CanCopy { get; init; }
    public string? CopyHint { get; init; }
    public string? ActionLabel { get; init; }
    public bool Direct { get; init; }
    public IReadOnlyList<SuggestCommand> Commands { get; init; } = [];
    public bool HasAction => ActionLabel is not null;
}
