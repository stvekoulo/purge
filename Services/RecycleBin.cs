using System.Runtime.InteropServices;

namespace PurgeWin.Services;

/// Moves files to the Recycle Bin through the shell, never deleting for good.
/// FOF_WANTNUKEWARNING makes the shell ask before it would delete an item
/// permanently (too big for the bin, or a drive without one), so Purge's
/// "Trash by default, never empties it" promise holds on Windows too.
public static class RecycleBin
{
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_WANTNUKEWARNING = 0x4000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
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

    // 64-bit only build: shellapi.h uses natural packing on Win64.
    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref SHQUERYRBINFO info);

    /// Returns null on success, or a short reason.
    public static string? MoveToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING | FOF_NOERRORUI | FOF_SILENT,
        };
        var result = SHFileOperation(ref op);
        if (op.fAnyOperationsAborted) return "Annulé : l'élément ne pouvait pas aller dans la Corbeille";
        if (File.Exists(path) || Directory.Exists(path))
        {
            return result switch
            {
                0 => "Utilisé par une application ouverte : en partie conservé",
                5 or 0x78 => "Accès refusé (droits administrateur requis)",
                0x20 => "Utilisé par une application ouverte",
                _ => $"Impossible de déplacer (code 0x{result:X})",
            };
        }
        return null;
    }

    /// Bytes already in the Recycle Bin across all drives (sidebar summary).
    public static long Size()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
    }
}
