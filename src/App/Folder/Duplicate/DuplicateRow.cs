using System.Collections.ObjectModel;
using System.Windows;

namespace Flect.Folder.Duplicate;

public sealed record DuplicateRow(string Note, string Text, bool IsHeader, bool IsRemoving);
