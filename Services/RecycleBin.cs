using System.Runtime.InteropServices;

namespace PurgeWin.Services;

/// Moves files to the Recycle Bin through the shell, never deleting for good.
/// Supports x64, ARM64 (natural packing) and x86 (1-byte packing per shellapi.h).
public static partial class RecycleBin
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT64
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    private struct SHFILEOPSTRUCT32
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO64
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SHQUERYRBINFO32
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation64(ref SHFILEOPSTRUCT64 op);

    [DllImport("shell32.dll", EntryPoint = "SHFileOperationW", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation32(ref SHFILEOPSTRUCT32 op);

    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin64(string? rootPath, ref SHQUERYRBINFO64 info);

    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin32(string? rootPath, ref SHQUERYRBINFO32 info);

    /// Returns null on success, or a short reason.
    public static string? MoveToRecycleBin(string path)
    {
        const ushort flags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING | FOF_NOERRORUI | FOF_SILENT;
        int result;
        bool aborted;
        if (IntPtr.Size == 8)
        {
            var op = new SHFILEOPSTRUCT64 { wFunc = FO_DELETE, pFrom = path + "\0\0", fFlags = flags };
            result = SHFileOperation64(ref op);
            aborted = op.fAnyOperationsAborted;
        }
        else
        {
            var op = new SHFILEOPSTRUCT32 { wFunc = FO_DELETE, pFrom = path + "\0\0", fFlags = flags };
            result = SHFileOperation32(ref op);
            aborted = op.fAnyOperationsAborted;
        }

        if (aborted) return "Annulé : l'élément ne pouvait pas aller dans la Corbeille";
        // Do not claim success just because the original path disappeared:
        // Windows may have completed only part of a failing shell operation.
        if (result != 0) return $"Impossible de déplacer (code 0x{result:X})";
        if (File.Exists(path) || Directory.Exists(path))
        {
            return "Utilisé par une application ouverte : en partie conservé";
        }
        return null;
    }

    /// Bytes already in the Recycle Bin across all drives (sidebar summary).
    public static long Size(string? rootPath = null)
    {
        if (IntPtr.Size == 8)
        {
            var info = new SHQUERYRBINFO64 { cbSize = Marshal.SizeOf<SHQUERYRBINFO64>() };
            return SHQueryRecycleBin64(rootPath, ref info) == 0 ? info.i64Size : 0;
        }
        else
        {
            var info = new SHQUERYRBINFO32 { cbSize = Marshal.SizeOf<SHQUERYRBINFO32>() };
            return SHQueryRecycleBin32(rootPath, ref info) == 0 ? info.i64Size : 0;
        }
    }

    public static long Count(string rootPath)
    {
        if (IntPtr.Size == 8)
        {
            var info = new SHQUERYRBINFO64 { cbSize = Marshal.SizeOf<SHQUERYRBINFO64>() };
            return SHQueryRecycleBin64(rootPath, ref info) == 0 ? info.i64NumItems : 0;
        }
        var small = new SHQUERYRBINFO32 { cbSize = Marshal.SizeOf<SHQUERYRBINFO32>() };
        return SHQueryRecycleBin32(rootPath, ref small) == 0 ? small.i64NumItems : 0;
    }
}
