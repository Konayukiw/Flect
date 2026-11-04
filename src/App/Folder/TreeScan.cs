using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Hashing;

namespace Flect.Folder;

internal sealed class TreeScan
{
    public List<ScannedFile> Files { get; } = [];
    public List<string> EmptyDirectories { get; } = [];
    public int DirectoryCount { get; set; }

    public long TotalBytes => Files.Sum(file => file.Size);
}
