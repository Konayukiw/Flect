using System.Globalization;
using System.Windows.Controls;

namespace Flect.Settings;

public sealed record Choice(object Value, string Label)
{
    public override string ToString() => Label;
}
