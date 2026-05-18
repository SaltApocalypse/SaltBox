using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.Storage.Pickers;
using Serilog;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Foundation;
using Windows.Storage;
using WinRT.Interop;

namespace SaltBox.Services;

public class DisplayInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public bool IsPrimary { get; set; }
    public nint HMonitor { get; set; }
    public string DisplayText => $"{Index} · {FriendlyName}（{Width}×{Height}）{(IsPrimary ? " · 主显示器" : "")}";
}

public class ScreenshotService
{
    private readonly MainWindow _mainWindow;
    private readonly LogService _log;
    private readonly ThemeService _themeService;

    private static Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static Guid IDXGIDeviceGuid = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    private readonly List<IntPtr> _identifyHwnds = new();
    private static readonly WNDPROC _identifyProc = IdentifyWindowProc;

    public ScreenshotService(MainWindow mainWindow, LogService log, ThemeService themeService)
    {
        _mainWindow = mainWindow;
        _log = log;
        _themeService = themeService;
    }

    public List<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();

        try
        {
            for (uint i = 0; ; i++)
            {
                var d = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(null, i, ref d, 0))
                    break;

                if ((d.StateFlags & DISPLAY_DEVICE_ATTACHED) == 0)
                    continue;

                var m = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                EnumDisplayDevices(d.DeviceName, 0, ref m, 0);

                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
                EnumDisplaySettings(d.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);

                var hMonitor = MonitorFromPoint(
                    new POINT { x = dm.dmPositionX, y = dm.dmPositionY },
                    MONITOR_DEFAULTTONEAREST);

                var idx = displays.Count + 1;
                displays.Add(new DisplayInfo
                {
                    Index = idx,
                    DeviceName = d.DeviceName,
                    FriendlyName = string.IsNullOrEmpty(m.DeviceString) ? $"Display {idx}" : m.DeviceString,
                    Width = dm.dmPelsWidth,
                    Height = dm.dmPelsHeight,
                    PositionX = dm.dmPositionX,
                    PositionY = dm.dmPositionY,
                    IsPrimary = (d.StateFlags & DISPLAY_DEVICE_PRIMARY) != 0,
                    HMonitor = hMonitor,
                });
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Display enumeration failed: {ex.Message}");
        }

        return displays;
    }

    public async Task<string?> PickFolderAsync()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var picker = new FolderPicker(windowId);
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            _log.Error($"FolderPicker failed: {ex.Message}");
            return null;
        }
    }

    private bool _hotkeyRegistered;
    private SUBCLASSPROC? _subclassDelegate;
    private GCHandle _subclassHandle;
    private const int HOTKEY_ID = 1;
    private uint _hotkeyModifier = MOD_WIN;
    private Windows.System.VirtualKey _hotkeyKey = Windows.System.VirtualKey.F2;
    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;
    private const uint MOD_WIN = 0x8;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_GETMINMAXINFO = 0x0024;

    public void RegisterGlobalHotkey()
    {
        if (_hotkeyRegistered) return;

        var hwnd = WindowNative.GetWindowHandle(_mainWindow);

        if (!RegisterHotKey(hwnd, HOTKEY_ID, _hotkeyModifier, (uint)_hotkeyKey))
        {
            _log.Warn("RegisterHotKey failed (hotkey may be in use by another app)");
            return;
        }

        _subclassDelegate = OnWindowMessage;
        _subclassHandle = GCHandle.Alloc(_subclassDelegate);

        if (!SetWindowSubclass(hwnd, _subclassDelegate, (IntPtr)HOTKEY_ID, IntPtr.Zero))
        {
            UnregisterHotKey(hwnd, HOTKEY_ID);
            _subclassHandle.Free();
            _subclassDelegate = null;
            _log.Warn("SetWindowSubclass failed");
            return;
        }

        _hotkeyRegistered = true;
        _log.Info($"Global hotkey registered (modifier={_hotkeyModifier}, key={_hotkeyKey})");
    }

    public void PrepareShortcut(uint modifier, Windows.System.VirtualKey key)
    {
        _hotkeyModifier = modifier;
        _hotkeyKey = key;
    }

    public void UpdateHotkey(uint modifier, Windows.System.VirtualKey key)
    {
        _hotkeyModifier = modifier;
        _hotkeyKey = key;

        if (_hotkeyRegistered)
        {
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            UnregisterHotKey(hwnd, HOTKEY_ID);
            if (!RegisterHotKey(hwnd, HOTKEY_ID, modifier, (uint)key))
            {
                _log.Warn("RegisterHotKey failed after shortcut update, reverting");
                // Try re-registering with old values
                _hotkeyModifier = MOD_WIN;
                _hotkeyKey = Windows.System.VirtualKey.F2;
                RegisterHotKey(hwnd, HOTKEY_ID, _hotkeyModifier, (uint)_hotkeyKey);
            }
        }
    }

    public void UnregisterGlobalHotkey()
    {
        if (!_hotkeyRegistered) return;

        var hwnd = WindowNative.GetWindowHandle(_mainWindow);
        if (_subclassDelegate is not null)
            RemoveWindowSubclass(hwnd, _subclassDelegate, (IntPtr)HOTKEY_ID);
        UnregisterHotKey(hwnd, HOTKEY_ID);
        _subclassHandle.Free();
        _subclassDelegate = null;
        _hotkeyRegistered = false;
        _log.Info("Global hotkey unregistered");
    }

    private IntPtr OnWindowMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr uId, IntPtr refData)
    {
        if (msg == WM_HOTKEY && (int)wParam == HOTKEY_ID)
        {
            _ = HandleHotkeyCaptureAsync();
            return IntPtr.Zero;
        }

        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMinTrackSize = new POINT { x = 643, y = 426 };
            Marshal.StructureToPtr(mmi, lParam, true);
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private async Task HandleHotkeyCaptureAsync()
    {
        try
        {
            if (!ReadIsEnabled())
            {
                _log.Info("Hotkey ignored — screenshot disabled in settings");
                return;
            }

            var displays = GetDisplays();
            var savedDisplay = ReadSelectedDisplay();
            var display = displays.FirstOrDefault(d => d.DeviceName == savedDisplay)
                       ?? displays.FirstOrDefault(d => d.IsPrimary)
                       ?? displays.FirstOrDefault();

            if (display == null)
            {
                _log.Warn("No display available for hotkey capture");
                return;
            }

            var savePath = ReadSavePath() ?? GetDefaultScreenshotPath();
            _log.Info($"Hotkey triggered — capturing {display.FriendlyName} to {savePath}");
            var result = await CaptureScreenshotAsync(display, savePath);
            TrySendNotification(result);
        }
        catch (Exception ex)
        {
            _log.Error($"Hotkey capture failed: {ex.Message}");
            TrySendNotification(null);
        }
    }

    private static bool ReadIsEnabled()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ScreenshotEnabled", out var v) && v is bool b)
                return b;
        }
        catch { }
        return true;
    }

    private static string? ReadSavePath()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ScreenshotSavePath", out var v) && v is string s)
                return s;
        }
        catch { }
        return null;
    }

    private static string? ReadSelectedDisplay()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ScreenshotDisplay", out var v) && v is string s)
                return s;
        }
        catch { }
        return null;
    }

    private static string GetDefaultScreenshotPath()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pictures, "Screenshots");
    }

    private static int ReadNotificationMode()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ScreenshotNotificationMode", out var v) && v is int i)
                return i;
        }
        catch { }
        return 1; // Text
    }

    private void TrySendNotification(string? imagePath)
    {
        var mode = ReadNotificationMode();
        if (mode == 0) // None
            return;

        if (!AppNotificationManager.IsSupported())
            return;

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText("SaltBox Screenshot")
                .AddText(imagePath ?? "Capture failed")
                .SetScenario(AppNotificationScenario.Urgent);

            if (mode == 2 && imagePath != null) // Preview
                builder.SetAppLogoOverride(new Uri(imagePath));

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch { }
    }

    private bool IsEffectivelyDark()
    {
        var theme = _themeService.CurrentTheme;
        if (theme == ElementTheme.Dark) return true;
        if (theme == ElementTheme.Light) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int v)
                return v == 0;
        }
        catch { }
        return false;
    }

    public async Task IdentifyDisplaysAsync()
    {
        if (_identifyHwnds.Count > 0) return;

        var isDark = IsEffectivelyDark();
        var bgColor = isDark ? 0x000000u : 0xFFFFFFu;
        var textColor = isDark ? 0xFFFFFFu : 0x000000u;

        var displays = GetDisplays();
        if (displays.Count == 0) return;

        var moduleHandle = GetModuleHandle(null);

        var wc = new WNDCLASS
        {
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_identifyProc),
            hInstance = moduleHandle,
            lpszClassName = "SaltBoxIdentify",
        };
        if (RegisterClass(ref wc) == 0)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != ERROR_CLASS_ALREADY_EXISTS)
            {
                _log.Warn($"Identify: RegisterClass failed ({err})");
                return;
            }
        }

        foreach (var d in displays)
        {
            var rect = new RECT { left = 0, top = 0, right = 200, bottom = 200 };
            var x = d.PositionX + 50;
            var y = d.PositionY + d.Height - 200 - 50;
            var hwnd = CreateWindowEx(
                WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
                "SaltBoxIdentify", null,
                WS_POPUP,
                x, y, 200, 200,
                IntPtr.Zero, IntPtr.Zero, moduleHandle, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) continue;

            _identifyHwnds.Add(hwnd);
            SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
            SetWindowPos(hwnd, HWND_TOPMOST, x, y, 200, 200, SWP_NOACTIVATE | SWP_SHOWWINDOW);

            var hdc = GetDC(hwnd);
            var bgBrush = CreateSolidBrush(bgColor);
            FillRect(hdc, ref rect, bgBrush);
            DeleteObject(bgBrush);

            var font = CreateFont(120, 0, 0, 0, FW_BOLD, 0, 0, 0,
                DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                ANTIALIASED_QUALITY, DEFAULT_PITCH | FF_DONTCARE, "Segoe UI");
            SelectObject(hdc, font);
            SetBkMode(hdc, TRANSPARENT);
            SetTextColor(hdc, textColor);

            DrawText(hdc, d.Index.ToString(), -1, ref rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);

            DeleteObject(font);
            ReleaseDC(hwnd, hdc);
            ShowWindow(hwnd, SW_SHOW);
        }

        await Task.Delay(3000);

        foreach (var hwnd in _identifyHwnds)
            DestroyWindow(hwnd);
        _identifyHwnds.Clear();
    }

    private static IntPtr IdentifyWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => DefWindowProc(hWnd, msg, wParam, lParam);

    public async Task<string?> CaptureScreenshotAsync(DisplayInfo display, string saveFolder)
    {
        Directory.CreateDirectory(saveFolder);

        // Try D3D capture first; fall back to GDI BitBlt if D3D is unavailable
        try
        {
            return await CaptureWithD3DAsync(display, saveFolder);
        }
        catch (Exception ex)
        {
            _log.Warn($"D3D capture failed ({ex.Message}), falling back to GDI");
            return await CaptureWithGDIAsync(display, saveFolder);
        }
    }

    private async Task<string?> CaptureWithD3DAsync(DisplayInfo display, string saveFolder)
    {
        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? session = null;

        try
        {
            var d3dDevice = CreateDirect3DDevice();
            var captureItem = CreateCaptureItemForMonitor(display.HMonitor);

            framePool = Direct3D11CaptureFramePool.Create(
                d3dDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                captureItem.Size);

            session = framePool.CreateCaptureSession(captureItem);

            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();

            TypedEventHandler<Direct3D11CaptureFramePool, object> handler = null!;
            handler = (pool, _) =>
            {
                if (tcs.Task.IsCompleted) return;
                var frame = pool.TryGetNextFrame();
                if (frame != null)
                {
                    pool.FrameArrived -= handler;
                    tcs.TrySetResult(frame);
                }
            };

            framePool.FrameArrived += handler;
            session.StartCapture();

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
            if (completedTask != tcs.Task)
            {
                _log.Warn("Screenshot timed out — no frame within 5s");
                throw new TimeoutException("No frame received within 5 seconds");
            }

            using var frame = await tcs.Task;

            var softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                frame.Surface, BitmapAlphaMode.Ignore);

            return await SaveSoftwareBitmapAsync(softwareBitmap, saveFolder);
        }
        finally
        {
            session?.Dispose();
            framePool?.Dispose();
        }
    }

    private async Task<string?> CaptureWithGDIAsync(DisplayInfo display, string saveFolder)
    {
        var hdcScreen = CreateDC("DISPLAY", null, null, IntPtr.Zero);
        if (hdcScreen == IntPtr.Zero)
            throw new InvalidOperationException("GDI: CreateDC failed");

        try
        {
            var hdcMem = CreateCompatibleDC(hdcScreen);
            if (hdcMem == IntPtr.Zero)
                throw new InvalidOperationException("GDI: CreateCompatibleDC failed");

            try
            {
                var hBitmap = CreateCompatibleBitmap(hdcScreen, display.Width, display.Height);
                if (hBitmap == IntPtr.Zero)
                    throw new InvalidOperationException("GDI: CreateCompatibleBitmap failed");

                try
                {
                    SelectObject(hdcMem, hBitmap);

                    if (!BitBlt(hdcMem, 0, 0, display.Width, display.Height,
                                hdcScreen, display.PositionX, display.PositionY, SRCCOPY))
                        throw new InvalidOperationException("GDI: BitBlt failed");

                    var bmi = new BITMAPINFO
                    {
                        bmiHeader = new BITMAPINFOHEADER
                        {
                            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                            biWidth = display.Width,
                            biHeight = -display.Height,
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = 0,
                        }
                    };

                    var pixels = new byte[display.Width * display.Height * 4];
                    if (GetDIBits(hdcScreen, hBitmap, 0, (uint)display.Height, pixels, ref bmi, DIB_RGB_COLORS) == 0)
                        throw new InvalidOperationException("GDI: GetDIBits failed");

                    for (int i = 3; i < pixels.Length; i += 4)
                        pixels[i] = 255;

                    return await SavePixelsAsync(pixels, display.Width, display.Height, saveFolder);
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
            finally
            {
                DeleteDC(hdcMem);
            }
        }
        finally
        {
            DeleteDC(hdcScreen);
        }
    }

    private static async Task<string?> SaveSoftwareBitmapAsync(SoftwareBitmap softwareBitmap, string saveFolder)
    {
        var now = DateTime.Now;
        var fileName = $"Screenshot_{now:yyyy-MM-dd_HH-mm-ss}.png";
        var folder = await StorageFolder.GetFolderFromPathAsync(saveFolder);
        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
        Log.Information($"Screenshot saved: {file.Path}");
        return file.Path;
    }

    private static async Task<string?> SavePixelsAsync(byte[] pixels, int width, int height, string saveFolder)
    {
        var now = DateTime.Now;
        var fileName = $"Screenshot_{now:yyyy-MM-dd_HH-mm-ss}.png";
        var folder = await StorageFolder.GetFolderFromPathAsync(saveFolder);
        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width,
            (uint)height,
            96, 96,
            pixels);
        await encoder.FlushAsync();
        Log.Information($"Screenshot saved: {file.Path}");
        return file.Path;
    }

    private static IDirect3DDevice CreateDirect3DDevice()
    {
        // Try HARDWARE driver first, fall back to WARP (software) if unavailable
        try
        {
            return CreateD3DDevice(1); // D3D_DRIVER_TYPE_HARDWARE
        }
        catch (Exception ex)
        {
            Log.Warning($"Hardware D3D device failed ({ex.Message}), falling back to WARP");
            return CreateD3DDevice(2); // D3D_DRIVER_TYPE_WARP
        }
    }

    private static IDirect3DDevice CreateD3DDevice(int driverType)
    {
        // Explicit feature level array — some systems fail with null/0 (DXGI_ERROR_UNSUPPORTED)
        var featureLevels = new[] { 0xB000, 0xA100, 0xA000, 0x9300, 0x9200, 0x9100 };
        var flHandle = GCHandle.Alloc(featureLevels, GCHandleType.Pinned);

        int hr;
        IntPtr d3dDevice;
        IntPtr context;
        try
        {
            hr = D3D11CreateDevice(
                IntPtr.Zero,
                driverType,
                IntPtr.Zero,
                0x20,
                flHandle.AddrOfPinnedObject(),
                (uint)featureLevels.Length,
                7,
                out d3dDevice,
                out _,
                out context);
        }
        finally
        {
            flHandle.Free();
        }

        if (hr < 0)
            throw new InvalidOperationException(
                $"D3D11CreateDevice failed (driverType={driverType}): 0x{hr:X8}",
                Marshal.GetExceptionForHR(hr));
        Marshal.Release(context);

        hr = Marshal.QueryInterface(d3dDevice, ref IDXGIDeviceGuid, out var dxgiDevice);
        Marshal.Release(d3dDevice);
        if (hr < 0)
            throw new InvalidOperationException(
                $"QueryInterface(IDXGIDevice) failed: 0x{hr:X8}",
                Marshal.GetExceptionForHR(hr));

        hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable);
        Marshal.Release(dxgiDevice);
        if (hr < 0)
            throw new InvalidOperationException(
                $"CreateDirect3D11DeviceFromDXGIDevice failed: 0x{hr:X8}",
                Marshal.GetExceptionForHR(hr));

        return (IDirect3DDevice)Marshal.GetObjectForIUnknown(inspectable);
    }

    private static GraphicsCaptureItem CreateCaptureItemForMonitor(nint hMonitor)
    {
        var factory = GetCaptureItemFactory();
        var hr = factory.CreateForMonitor(hMonitor, ref GraphicsCaptureItemGuid, out nint ptr);
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
        return GraphicsCaptureItem.FromAbi(ptr);
    }

    private static IGraphicsCaptureItemInterop GetCaptureItemFactory()
    {
        var hString = IntPtr.Zero;
        try
        {
            var hr = WindowsCreateString(
                "Windows.Graphics.Capture.GraphicsCaptureItem",
                "Windows.Graphics.Capture.GraphicsCaptureItem".Length,
                out hString);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            var iid = typeof(IGraphicsCaptureItemInterop).GUID;
            hr = RoGetActivationFactory(hString, ref iid, out nint factoryPtr);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
        }
        finally
        {
            if (hString != IntPtr.Zero)
                WindowsDeleteString(hString);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        int CreateForMonitor(nint hMonitor, ref Guid riid, out nint ppv);
        int CreateForWindow(nint hwnd, ref Guid riid, out nint ppv);
    }

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr hString, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hString);

    [DllImport("combase.dll")]
    private static extern void WindowsDeleteString(IntPtr hString);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int DriverType,
        IntPtr Software,
        uint Flags,
        IntPtr pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        out int pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("windows.graphics.directx.direct3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint DISPLAY_DEVICE_ATTACHED = 0x1;
    private const uint DISPLAY_DEVICE_PRIMARY = 0x4;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    // --- Global hotkey (Win+F2) ---

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, IntPtr uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr uIdSubclass, IntPtr dwRefData);

    // --- Identify overlay (Win32 native windows) ---

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_LAYERED = 0x80000;
    private const uint WS_EX_TRANSPARENT = 0x20;
    private const uint WS_EX_TOPMOST = 0x8;
    private const uint WS_EX_TOOLWINDOW = 0x80;
    private const int SW_SHOW = 5;
    private const uint LWA_COLORKEY = 0x1;
    private const uint LWA_ALPHA = 0x2;
    private const uint DT_CENTER = 0x1;
    private const uint DT_VCENTER = 0x4;
    private const uint DT_SINGLELINE = 0x20;
    private const int TRANSPARENT = 1;
    private const int FW_BOLD = 700;
    private const int DEFAULT_CHARSET = 1;
    private const int OUT_DEFAULT_PRECIS = 0;
    private const int CLIP_DEFAULT_PRECIS = 0;
    private const int ANTIALIASED_QUALITY = 4;
    private const int DEFAULT_PITCH = 0;
    private const int FF_DONTCARE = 0;
    private const int ERROR_CLASS_ALREADY_EXISTS = 1410;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern int DrawText(IntPtr hdc, string lpchText, int nCount, ref RECT lpRect, uint uFormat);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateFont(int nHeight, int nWidth, int nEscapement,
        int nOrientation, int fnWeight, uint fdwItalic, uint fdwUnderline,
        uint fdwStrikeOut, uint fdwCharSet, uint fdwOutputPrecision,
        uint fdwClipPrecision, uint fdwQuality, uint fdwPitchAndFamily,
        string lpszFace);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint color);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    // --- GDI P/Invoke for fallback capture ---

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDC(string lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int x, int y, int cx, int ny, IntPtr hdcSrc, int x1, int y1, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint crColor);

    [DllImport("user32.dll")]
    private static extern IntPtr FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

    private const uint SRCCOPY = 0x00CC0020;
    private const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256 * 4)]
        public byte[]? bmiColors;
    }
}
