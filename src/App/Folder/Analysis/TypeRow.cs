using System.Windows;
using Microsoft.Win32;

namespace Flect.Folder.Analysis;

public sealed record TypeRow(string Extension, string Size, string Share, string Count,
                             double BarWidth);
