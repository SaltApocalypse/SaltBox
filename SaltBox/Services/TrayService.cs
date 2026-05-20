using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Serilog;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Storage;
using WinRT.Interop;

namespace SaltBox.Services;

public class TrayService
{
    private const int WM_TRAYICON = 0xA000 + 100;
    private const uint NIM_ADD = 0;
    private const uint NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1;
    private const uint NIF_ICON = 2;
    private const uint NIF_TIP = 4;
    private const uint SW_HIDE = 0;
    private const uint SW_SHOW = 5;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint MF_STRING = 0;
    private const uint MF_BYPOSITION = 0x0400;
    private const uint MF_SEPARATOR = 0x0800;

    private readonly MainWindow _window;
    private readonly LogService _log;
    private nint _trayHwnd;
    private nint _hIcon;
    private NOTIFYICONDATA _nid;
    private WndProcDelegate _trayWndProc = null!;
    private bool _disposed;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    public TrayService(MainWindow window, LogService log)
    {
        _window = window;
        _log = log;
    }

    public void Initialize()
    {
        var hwnd = WindowNative.GetWindowHandle(_window);
        var hInst = GetWindowLong(hwnd, -6);

        var className = "SaltBoxTrayWnd_" + Guid.NewGuid().ToString("N");

        _trayWndProc = TrayWndProc;
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            hInstance = hInst,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_trayWndProc),
            lpszClassName = className
        };

        if (RegisterClassEx(ref wc) == 0)
        {
            _log.Error("TrayService: RegisterClassEx failed");
            return;
        }

        _trayHwnd = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
        if (_trayHwnd == IntPtr.Zero)
        {
            _log.Error("TrayService: CreateWindowEx failed");
            return;
        }

        _hIcon = LoadTrayIcon();

        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _trayHwnd,
            uID = 100,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "SaltBox"
        };

        if (!Shell_NotifyIcon(NIM_ADD, ref _nid))
            _log.Error("TrayService: Shell_NotifyIcon NIM_ADD failed");

        _window.AppWindow.Closing += OnAppWindowClosing;
        _window.Closed += OnWindowClosed;

        _log.Info("TrayService initialized");
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_disposed)
        {
            args.Cancel = true;
            HideWindow();
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (!_disposed)
        {
            args.Handled = true;
            HideWindow();
        }
    }

    private nint TrayWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_TRAYICON)
        {
            switch ((uint)lParam)
            {
                case WM_LBUTTONDBLCLK:
                case 0x0201: // WM_LBUTTONDOWN
                    ShowWindow();
                    break;
                case WM_RBUTTONDOWN:
                    ShowContextMenu();
                    break;
            }
            return 0;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        InsertMenu(hMenu, 0, MF_STRING | MF_BYPOSITION, 1, "显示 SaltBox");
        InsertMenu(hMenu, 1, MF_SEPARATOR | MF_BYPOSITION, 0, null);
        InsertMenu(hMenu, 2, MF_STRING | MF_BYPOSITION, 2, "设置");
        InsertMenu(hMenu, 3, MF_STRING | MF_BYPOSITION, 3, "退出");

        GetCursorPos(out var pt);

        var cmd = TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, pt.x, pt.y, 0, _trayHwnd, IntPtr.Zero);

        switch (cmd)
        {
            case 1:
                ShowWindow();
                break;
            case 2:
                _window.NavigateToSettings();
                break;
            case 3:
                ExitApp();
                break;
        }

        DestroyMenu(hMenu);
    }

    private void ExitApp()
    {
        _disposed = true;
        Dispose();
        _window.Close();
        Application.Current.Exit();
    }

    private void HideWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(_window);
        ShowWindow(hwnd, SW_HIDE);
        _log.Info("Window hidden to tray");
    }

    private void ShowWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(_window);
        ShowWindow(hwnd, SW_SHOW);
        SetForegroundWindow(hwnd);
        _log.Info("Window restored from tray");
    }

    public void Dispose()
    {
        if (_nid.hWnd != IntPtr.Zero)
            Shell_NotifyIcon(NIM_DELETE, ref _nid);

        if (_trayHwnd != IntPtr.Zero)
            DestroyWindow(_trayHwnd);

        if (_hIcon != IntPtr.Zero)
            DestroyIcon(_hIcon);

        _log.Info("TrayService disposed");
    }

    private nint LoadTrayIcon()
    {
        try
        {
            var icoName = "SaltBoxTray.ico";
            var icoPath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, icoName);

            if (!File.Exists(icoPath))
            {
                try
                {
                    var pngPath = FindPngPath();
                    if (pngPath != null && File.Exists(pngPath))
                        CreateIcoFromPng(File.ReadAllBytes(pngPath), icoPath);
                }
                catch (Exception ex)
                {
                    _log.Warn($"TrayService: icon creation failed: {ex.Message}");
                }
            }

            if (File.Exists(icoPath))
            {
                var icon = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                if (icon != IntPtr.Zero)
                    return icon;
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"TrayService: icon load failed (no package identity): {ex.Message}");
        }

        return IntPtr.Zero;
    }

    private static string? FindPngPath()
    {
        var candidates = new[]
        {
            // With package identity: Package.Current.InstalledLocation points to app dir
            TryGetPkgPath(),
            // Without package identity: use AppContext.BaseDirectory
            Path.Combine(AppContext.BaseDirectory, "Assets", "StoreLogo.png"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? TryGetPkgPath()
    {
        try
        {
            return Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "StoreLogo.png");
        }
        catch (Exception ex)
        {
            Log.Warning("TryGetPkgPath failed: {Message}", ex.Message);
            return null;
        }
    }

    private static void CreateIcoFromPng(byte[] pngData, string outputPath)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        bw.Write((short)0);     // reserved
        bw.Write((short)1);     // type: ICO
        bw.Write((short)1);     // count: 1
        bw.Write((byte)0);      // width: 0 = 256 (to fit any PNG size)
        bw.Write((byte)0);      // height: 0 = 256
        bw.Write((short)0);     // colors
        bw.Write((short)0);     // reserved
        bw.Write((short)32);    // planes * bpp (32bpp)
        bw.Write((int)pngData.Length); // image data size
        bw.Write(22);           // offset (header + dir entry)
        bw.Write(pngData);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint cmd, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint hInst, string lpszName, uint uType, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool InsertMenu(nint hMenu, uint uPosition, uint uFlags, nint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern nint TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern nint GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hBrush;
        public nint lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }
}
