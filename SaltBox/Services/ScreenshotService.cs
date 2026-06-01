using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Serilog;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using SaltBox.Helpers;
using SaltBox.ViewModels;

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
    private readonly DispatcherQueue _uiDispatcher;

    public ScreenshotService(MainWindow mainWindow, LogService log, ThemeService themeService)
    {
        _mainWindow = mainWindow;
        _log = log;
        _themeService = themeService;
        _uiDispatcher = mainWindow.DispatcherQueue;
    }

    public NotificationMode NotificationMode { get; set; } = NotificationMode.Text;
    public bool IsEnabled { get; set; } = true;
    public string SavePath { get; set; } = "";
    public string SelectedDisplay { get; set; } = "";

    private bool _hotkeyRegistered;
    private SUBCLASSPROC? _subclassDelegate;
    private GCHandle _subclassHandle;
    private const int HOTKEY_ID = 1;
    private uint _hotkeyModifier = ModifierHelper.MOD_WIN;
    private Windows.System.VirtualKey _hotkeyKey = Windows.System.VirtualKey.F2;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_GETMINMAXINFO = 0x0024;

    private readonly List<IntPtr> _identifyHwnds = new();
    private readonly WNDPROC _identifyProc = IdentifyWindowProc;

    private bool _useHookFallback;
    private static readonly LowLevelKeyboardProc _hookProc = HookProcCallback;
    private static GCHandle _hookHandle;
    private static IntPtr _hookId = IntPtr.Zero;
    private static ScreenshotService? _hookInstance;

    public void RegisterGlobalHotkey()
    {
        if (_hotkeyRegistered) return;

        // Mouse buttons: RegisterHotKey returns TRUE but never fires WM_HOTKEY
        if (_hotkeyKey is Windows.System.VirtualKey.XButton1 or Windows.System.VirtualKey.XButton2)
        {
            _log.Warn("Mouse button hotkey, using low-level hook");
            InstallHookFallback();
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(_mainWindow);

        if (!RegisterHotKey(hwnd, HOTKEY_ID, _hotkeyModifier, (uint)_hotkeyKey))
        {
            _log.Warn("RegisterHotKey failed, falling back to low-level hook");
            InstallHookFallback();
            return;
        }

        _subclassDelegate = OnWindowMessage;
        _subclassHandle = GCHandle.Alloc(_subclassDelegate);

        if (!SetWindowSubclass(hwnd, _subclassDelegate, (IntPtr)HOTKEY_ID, IntPtr.Zero))
        {
            UnregisterHotKey(hwnd, HOTKEY_ID);
            _subclassHandle.Free();
            _subclassDelegate = null;
            _log.Warn("SetWindowSubclass failed, falling back to low-level hook");
            InstallHookFallback();
            return;
        }

        _hotkeyRegistered = true;
        UninstallHookFallback();
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

        // Mouse buttons: RegisterHotKey returns TRUE but never fires WM_HOTKEY
        if (key is Windows.System.VirtualKey.XButton1 or Windows.System.VirtualKey.XButton2)
        {
            if (_hotkeyRegistered)
            {
                var hwnd = WindowNative.GetWindowHandle(_mainWindow);
                UnregisterHotKey(hwnd, HOTKEY_ID);
                _hotkeyRegistered = false;
            }
            UninstallHookFallback();
            InstallHookFallback();
            return;
        }

        if (_hotkeyRegistered)
        {
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            UnregisterHotKey(hwnd, HOTKEY_ID);
            if (!RegisterHotKey(hwnd, HOTKEY_ID, modifier, (uint)key))
            {
                _log.Warn("RegisterHotKey failed after shortcut update, falling back to hook");
                InstallHookFallback();
            }
            else
            {
                UninstallHookFallback();
            }
        }
        else
        {
            // Not yet registered — try RegisterHotKey, fall back to hook
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            if (!RegisterHotKey(hwnd, HOTKEY_ID, modifier, (uint)key))
            {
                _log.Warn("RegisterHotKey failed for shortcut update, falling back to hook");
                InstallHookFallback();
                _hotkeyRegistered = true;
            }
            else
            {
                _subclassDelegate = OnWindowMessage;
                _subclassHandle = GCHandle.Alloc(_subclassDelegate);

                if (!SetWindowSubclass(hwnd, _subclassDelegate, (IntPtr)HOTKEY_ID, IntPtr.Zero))
                {
                    UnregisterHotKey(hwnd, HOTKEY_ID);
                    _subclassHandle.Free();
                    _subclassDelegate = null;
                    _log.Warn("SetWindowSubclass failed for shortcut update, falling back to hook");
                    InstallHookFallback();
                    _hotkeyRegistered = true;
                }
                else
                {
                    _hotkeyRegistered = true;
                    UninstallHookFallback();
                    _log.Info($"Global hotkey updated (modifier={_hotkeyModifier}, key={_hotkeyKey})");
                }
            }
        }
    }

    public void UnregisterGlobalHotkey()
    {
        UninstallHookFallback();

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

    private void InstallHookFallback()
    {
        if (_useHookFallback) return;
        UninstallHookFallback();

        var isMouse = _hotkeyKey is Windows.System.VirtualKey.XButton1 or Windows.System.VirtualKey.XButton2;
        var hookType = isMouse ? WH_MOUSE_LL : WH_KEYBOARD_LL;

        _hookInstance = this;
        _hookHandle = GCHandle.Alloc(_hookProc);
        _hookId = SetWindowsHookEx(hookType, _hookProc, GetModuleHandle(null), 0);

        if (_hookId == IntPtr.Zero)
        {
            _hookHandle.Free();
            _hookInstance = null;
            _log.Error($"InstallHookFallback failed (type={hookType})");
            return;
        }

        _useHookFallback = true;
        _log.Info($"Fallback hook installed (type={(isMouse ? "WH_MOUSE_LL" : "WH_KEYBOARD_LL")}, key={_hotkeyKey})");
    }

    private void UninstallHookFallback()
    {
        if (!_useHookFallback) return;
        _useHookFallback = false;
        _hookInstance = null;
        if (_hookId != IntPtr.Zero)
            UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _hookHandle.Free();
    }

    private static IntPtr HookProcCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var instance = _hookInstance;
            if (instance != null && instance._useHookFallback)
                instance.ProcessHookEvent(wParam, lParam);
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void ProcessHookEvent(IntPtr wParam, IntPtr lParam)
    {
        var isMouse = _hotkeyKey is Windows.System.VirtualKey.XButton1 or Windows.System.VirtualKey.XButton2;

        if (isMouse)
        {
            var msg = (int)wParam;
            if (msg != WM_XBUTTONDOWN) return;

            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
            var expected = _hotkeyKey switch
            {
                Windows.System.VirtualKey.XButton1 => XBUTTON1,
                Windows.System.VirtualKey.XButton2 => XBUTTON2,
                _ => 0u
            };
            if (xButton != expected) return;
        }
        else
        {
            var msg = (int)wParam;
            if (msg != WM_KEYDOWN && msg != WM_SYSKEYDOWN) return;
            var vkCode = Marshal.ReadInt32(lParam);
            if ((uint)vkCode != (uint)_hotkeyKey) return;
        }

        if (!CheckModifiersPressed()) return;

        _uiDispatcher.TryEnqueue(() => _ = HandleHotkeyCaptureAsync());
    }

    private bool CheckModifiersPressed()
    {
        bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

        var hasAlt = (_hotkeyModifier & ModifierHelper.MOD_ALT) != 0;
        var hasCtrl = (_hotkeyModifier & ModifierHelper.MOD_CONTROL) != 0;
        var hasShift = (_hotkeyModifier & ModifierHelper.MOD_SHIFT) != 0;
        var hasWin = (_hotkeyModifier & ModifierHelper.MOD_WIN) != 0;

        if (hasAlt != IsDown((int)Windows.System.VirtualKey.Menu)) return false;
        if (hasCtrl != IsDown((int)Windows.System.VirtualKey.Control)) return false;
        if (hasShift != IsDown((int)Windows.System.VirtualKey.Shift)) return false;
        if (hasWin != (IsDown((int)Windows.System.VirtualKey.LeftWindows) ||
                       IsDown((int)Windows.System.VirtualKey.RightWindows))) return false;

        // Ensure no extra modifiers are pressed
        var extras = 0;
        if (IsDown((int)Windows.System.VirtualKey.Menu)) extras++;
        if (IsDown((int)Windows.System.VirtualKey.Control)) extras++;
        if (IsDown((int)Windows.System.VirtualKey.Shift)) extras++;
        if (IsDown((int)Windows.System.VirtualKey.LeftWindows)) extras++;
        if (IsDown((int)Windows.System.VirtualKey.RightWindows)) extras++;

        var expected = (hasAlt ? 1 : 0) + (hasCtrl ? 1 : 0) + (hasShift ? 1 : 0) + (hasWin ? 1 : 0);
        return extras == expected;
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
            if (!IsEnabled)
            {
                _log.Info("Hotkey ignored — screenshot disabled in settings");
                return;
            }

            var displays = GetDisplays();
            var display = displays.FirstOrDefault(d => d.DeviceName == SelectedDisplay)
                       ?? displays.FirstOrDefault(d => d.IsPrimary)
                       ?? displays.FirstOrDefault();

            if (display == null)
            {
                _log.Warn("No display available for hotkey capture");
                return;
            }

            var savePath = string.IsNullOrEmpty(SavePath) ? GetDefaultScreenshotPath() : SavePath;
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

    private static string GetDefaultScreenshotPath()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pictures, "Screenshots");
    }

    public List<DisplayInfo> GetDisplays()
    {
        var displays = new List<DisplayInfo>();
        var index = 0;
        DISPLAY_DEVICE dd = default;
        dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        while (EnumDisplayDevices(null, (uint)index, ref dd, 0))
        {
            if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED) != 0)
            {
                DEVMODE dm = default;
                dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                if (EnumDisplaySettings(dd.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                {
                    displays.Add(new DisplayInfo
                    {
                        Index = index,
                        DeviceName = dd.DeviceName,
                        FriendlyName = dd.DeviceString,
                        Width = dm.dmPelsWidth,
                        Height = dm.dmPelsHeight,
                        PositionX = dm.dmPositionX,
                        PositionY = dm.dmPositionY,
                        IsPrimary = (dd.StateFlags & DISPLAY_DEVICE_PRIMARY) != 0,
                        HMonitor = MonitorFromPoint(
                            new POINT { x = dm.dmPositionX + dm.dmPelsWidth / 2, y = dm.dmPositionY + dm.dmPelsHeight / 2 },
                            MONITOR_DEFAULTTONEAREST),
                    });
                }
            }
            index++;
        }
        return displays;
    }

    public async Task<string?> PickFolderAsync()
    {
        try
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            InitializeWithWindow.Initialize(folderPicker, hwnd);
            var folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            _log.Warn($"PickFolderAsync failed: {ex.Message}");
            return null;
        }
    }

    private void TrySendNotification(string? imagePath)
    {
        if (NotificationMode == NotificationMode.None)
            return;

        if (!AppNotificationManager.IsSupported())
            return;

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText("SaltBox Screenshot")
                .AddText(imagePath ?? "Capture failed")
                .SetScenario(AppNotificationScenario.Urgent);

            if (NotificationMode == NotificationMode.Preview && imagePath != null)
                builder.SetAppLogoOverride(new Uri(imagePath));

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            _log.Warn($"Notification show failed: {ex.Message}");
        }
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
        catch (Exception ex)
        {
            _log.Warn($"Failed to read theme registry: {ex.Message}");
        }
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

        var isHdr = await HdrHelper.IsDisplayHdrAsync(_uiDispatcher);

        // Try D3D capture first; fall back to GDI BitBlt if D3D is unavailable
        try
        {
            return await CaptureWithD3DAsync(display, saveFolder, isHdr);
        }
        catch (Exception ex)
        {
            _log.Warn($"D3D capture failed ({ex.Message}), falling back to GDI");
            return await CaptureWithGDIAsync(display, saveFolder);
        }
    }

    private async Task<string?> CaptureWithD3DAsync(DisplayInfo display, string saveFolder, bool isHdr)
    {
        Direct3D11CaptureFramePool? framePool = null;
        GraphicsCaptureSession? session = null;

        try
        {
            var d3dDevice = CreateDirect3DDevice();
            var captureItem = CreateCaptureItemForMonitor(display.HMonitor);

            // Use FP16 pixel format on HDR displays to avoid pixel overclipping (washed-out colors)
            var pixelFormat = isHdr
                ? DirectXPixelFormat.R16G16B16A16Float
                : DirectXPixelFormat.B8G8R8A8UIntNormalized;

            if (isHdr)
                _log.Info("HDR display detected — capturing with FP16 format");

            framePool = Direct3D11CaptureFramePool.Create(
                d3dDevice,
                pixelFormat,
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

            // Check if the SoftwareBitmap preserved FP16 format (HDR path)
            if (isHdr && softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                _log.Info("Converting FP16 HDR frame to SDR with ACES tone mapping");
                var size = captureItem.Size;
                var pixels = HdrHelper.ConvertFp16ToSdrPixels(softwareBitmap, size.Width, size.Height);
                return await SavePixelsAsync(pixels, size.Width, size.Height, saveFolder);
            }

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

        var dxgiGuid = IDXGIDeviceGuid;
        hr = Marshal.QueryInterface(d3dDevice, ref dxgiGuid, out var dxgiDevice);
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
        var capGuid = GraphicsCaptureItemGuid;
        var hr = factory.CreateForMonitor(hMonitor, ref capGuid, out nint ptr);
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

    private static readonly Guid IDXGIDeviceGuid = new("7EC71627-C1F5-44A2-B24B-11684F3E92EB");
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

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

    // --- Low-level hook fallback (when RegisterHotKey fails) ---

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_XBUTTONDOWN = 0x20B;
    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

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
