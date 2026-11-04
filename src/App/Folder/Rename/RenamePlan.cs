using System.Windows;
using System.Windows.Controls;

namespace Flect.Folder.Rename;

internal sealed record RenamePlan(string Prefix, string Name, string Suffix, bool Numbered);
