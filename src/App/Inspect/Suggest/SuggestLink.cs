using System.Globalization;

namespace Flect.Inspect.Suggest;

internal sealed record SuggestLink(string Label, IReadOnlyList<SuggestCommand> Commands, bool Direct);
