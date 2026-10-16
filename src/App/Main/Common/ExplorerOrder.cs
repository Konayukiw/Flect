using System.Runtime.InteropServices;

namespace Optimizer.Main.Common;

internal sealed class ExplorerOrder : IComparer<string?>
{
    public static readonly ExplorerOrder Instance = new();

    public int Compare(string? left, string? right)
    {
        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;
        return StrCmpLogicalW(left, right);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string left, string right);
}
