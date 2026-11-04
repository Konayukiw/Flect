using System.Text;

namespace Flect.Inspect;

internal sealed class NameInfo
{
    public required string Extension { get; init; }
    public required string ClaimedKind { get; init; }
    public string? ShownName { get; init; }
    public string? MarkedName { get; init; }
    public string? DisguiseAs { get; init; }
    public required IReadOnlyList<string> Alerts { get; init; }
}
