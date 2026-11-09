using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint WM_APP = 0x8000;
    private const uint WM_TRAYCALLBACK = WM_APP + 101;
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_NULL = 0x0000;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const int SW_RESTORE = 9;

    private const int CMD_OPEN = 1001;
    private const int CMD_SCAN = 1002;
    private const int CMD_EXIT = 1003;

    private readonly IntPtr _hWnd;
    private readonly AppWindow _appWindow;
    private readonly SubclassProc _subclassProc;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _installed, _disposed;
    public bool IsExiting { get; private set; }

    public TrayIconService(IntPtr hWnd, AppWindow appWindow)
    {
        _hWnd = hWnd;
        _appWindow = appWindow;
        _subclassProc = WndProc;
        Install();
    }

    private void Install()
    {
        if (_installed) return;
        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icoPath))
            _hIcon = LoadImage(IntPtr.Zero, icoPath, 1, 16, 16, 0x00000010);

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYCALLBACK,
            hIcon = _hIcon,
            szTip = "Purge"
        };
        _installed = Shell_NotifyIcon(NIM_ADD, ref nid);
        SetWindowSubclass(_hWnd, _subclassProc, (UIntPtr)101, IntPtr.Zero);
    }

    public void UpdateTooltip(string tip)
    {
        if (!_installed || _disposed) return;
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_TIP,
            szTip = tip.Length > 120 ? tip[..120] : tip
        };
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    public void ShowWindow()
    {
        _appWindow.Show();
        ShowWindow(_hWnd, SW_RESTORE);
        SetForegroundWindow(_hWnd);
    }

    public void ExitApplication()
    {
        IsExiting = true;
        Dispose();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;
        AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_OPEN, Loc.T("Ouvrir Purge", "Open Purge"));
        AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_SCAN, Loc.T("Tout analyser", "Scan Everything"));
        AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
        AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_EXIT, Loc.T("Quitter Purge", "Quit Purge"));

        GetCursorPos(out var pt);
        SetForegroundWindow(_hWnd);
        TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON, pt.X, pt.Y, _hWnd, IntPtr.Zero);
        PostMessage(_hWnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(hMenu);
    }

    private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_TRAYCALLBACK)
        {
            uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);
            if (mouseMsg is WM_LBUTTONUP or WM_LBUTTONDBLCLK)
            {
                ShowWindow();
                return IntPtr.Zero;
            }
            if (mouseMsg == WM_RBUTTONUP)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }
        else if (uMsg == WM_COMMAND)
        {
            int cmdId = (int)(wParam.ToInt64() & 0xFFFF);
            if (cmdId == CMD_OPEN) { ShowWindow(); return IntPtr.Zero; }
            if (cmdId == CMD_SCAN) { ShowWindow(); _ = PurgeStore.Shared.ScanEverythingAsync(); return IntPtr.Zero; }
            if (cmdId == CMD_EXIT) { ExitApplication(); return IntPtr.Zero; }
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_installed)
        {
            var nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hWnd, uID = 1 };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            RemoveWindowSubclass(_hWnd, _subclassProc, (UIntPtr)101);
            _installed = false;
        }
        if (_hIcon != IntPtr.Zero) { DestroyIcon(_hIcon); _hIcon = IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);
}
