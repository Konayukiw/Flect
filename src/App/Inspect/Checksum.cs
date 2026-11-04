using System.Security.Cryptography;

namespace Flect.Inspect;

internal static class Checksum
{
    public static string Sha256(string path, CancellationToken token)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite | FileShare.Delete,
                                           1024 * 1024, FileOptions.SequentialScan);
        using var hasher = SHA256.Create();
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            hasher.TransformBlock(buffer, 0, read, null, 0);
        }

        hasher.TransformFinalBlock([], 0, 0);
        return System.Convert.ToHexString(hasher.Hash!).ToLowerInvariant();
    }
}
