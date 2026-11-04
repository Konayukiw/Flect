using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Hashing;

namespace Flect.Folder;

internal sealed record ScannedFile(string Path, long Size, DateTime LastWrite, string Extension);
