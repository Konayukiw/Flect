namespace Flect.Settings.Output;

internal sealed record OutputFeature(string Key, string CategoryKey, string LabelKey,
                                     IReadOnlyList<string> TaskIds);
