using System.Runtime.InteropServices;

namespace Optimizer.Main.Common;

internal static class Recycler
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    private const int NativeStructSize = 56;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int SHFileOperationW(ref ShFileOpStruct operation);

    public static void Delete(IReadOnlyList<string> paths,
                              DeleteMethod method = DeleteMethod.RecycleBin)
    {
        const int chunkSize = 512;
        for (int offset = 0; offset < paths.Count; offset += chunkSize)
        {
            var chunk = paths.Skip(offset).Take(chunkSize).ToList();
            DeleteChunk(chunk, method);
        }
    }

    private static void DeleteChunk(IReadOnlyList<string> paths, DeleteMethod method)
    {
        int size = Marshal.SizeOf<ShFileOpStruct>();
        if (size != NativeStructSize)
        {
            throw new InvalidOperationException(
                $"SHFILEOPSTRUCTW layout mismatch: expected {NativeStructSize} bytes, got {size}.");
        }

        var buffer = string.Join('\0', paths) + "\0\0";
        var from = Marshal.StringToHGlobalUni(buffer);
        try
        {
            ushort flags = FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT;
            if (method == DeleteMethod.RecycleBin) flags |= FOF_ALLOWUNDO;

            var operation = new ShFileOpStruct
            {
                wFunc = FO_DELETE,
                pFrom = from,
                fFlags = flags,
            };

            int result = SHFileOperationW(ref operation);
            if (result != 0)
            {
                throw new IOException($"The shell could not complete the deletion (code {result}).");
            }
            if (operation.fAnyOperationsAborted != 0)
            {
                throw new OperationCanceledException();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
    }
}
