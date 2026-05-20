using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SaltBox.Services;
using SaltBox.Helpers;
using static SaltBox.Helpers.ModifierHelper;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Windows.System;

namespace SaltBox.Views;

public sealed partial class KeyRecorderDialog : ContentDialog
{
    private readonly HashSet<VirtualKey> _modifiers = new();
    private VirtualKey? _actionKey;
    private readonly ObservableCollection<string> _keyNames = new();
    private volatile bool _isListening;
    private bool _captureComplete;
    private readonly HashSet<VirtualKey> _pressedKeys = new();
    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcher = null!;
    private ShortcutRegistry? _registry;
    private string? _toolName;
    private string _conflictSystemPrefix = "";
    private string _conflictAppPrefix = "";
    private string _conflictTitle = "";

    public uint SelectedModifier { get; private set; }
    public VirtualKey SelectedKey { get; private set; }
    public bool IsUnconventional { get; private set; }
    public string WarningTitle { get; private set; } = "";
    public string WarningMessage { get; private set; } = "";
    public string TitleText { get; private set; } = "";
    public string SaveText { get; private set; } = "";
    public string CancelText { get; private set; } = "";
    public string ResetText { get; private set; } = "";

    public KeyRecorderDialog()
    {
        InitializeComponent();
    }

    public void SetLanguage(CultureService lang)
    {
        TitleText = lang.KeyRecorderTitle;
        SaveText = lang.Save;
        CancelText = lang.Cancel;
        WarningTitle = lang.ShortcutWarningTitle;
        WarningMessage = lang.ShortcutWarningMessage;
        _conflictTitle = lang.ShortcutConflictTitle;
        _conflictSystemPrefix = lang.ShortcutConflictSystem;
        _conflictAppPrefix = lang.ShortcutConflictApp;
        ResetText = lang.KeyRecorderReset;

        InstructionText.Text = lang.KeyRecorderInstruction;
    }

    public void SetShortcutRegistry(ShortcutRegistry registry)
    {
        _registry = registry;
    }

    public void SetToolName(string toolName)
    {
        _toolName = toolName;
    }

    private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        _dispatcher = DispatcherQueue;
        KeyList.ItemsSource = _keyNames;
        _isListening = true;
        InstallKeyboardHook();
        InstallMouseHook();
        _dispatcher.TryEnqueue(() => KeyCaptureBox.Focus(FocusState.Programmatic));
    }

    private void ContentDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        _isListening = false;
        RemoveKeyboardHook();
        RemoveMouseHook();
    }

    private void OnCaptureKeyDown(object sender, KeyRoutedEventArgs args)
    {
        ProcessKey(args.Key);
        args.Handled = true;
        KeyCaptureBox.Focus(FocusState.Programmatic);
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        ResetRecording();
        _dispatcher.TryEnqueue(() => KeyCaptureBox.Focus(FocusState.Programmatic));
    }

    private void ProcessKey(VirtualKey key)
    {
        if (!_isListening || _captureComplete) return;
        if (key == VirtualKey.Escape || key == VirtualKey.Enter || key == VirtualKey.Tab) return;

        if (key == VirtualKey.Back || key == VirtualKey.Delete)
        {
            ResetRecording();
            return;
        }

        _pressedKeys.Add(key);

        if (IsModifierKey(key))
            _modifiers.Add(CanonicalModifier(key));
        else
            _actionKey = key;

        UpdateDisplay();
        UpdateSaveButton();
    }

    private void ProcessKeyUp(VirtualKey key)
    {
        if (!_isListening || _captureComplete) return;

        _pressedKeys.Remove(key);

        if (_pressedKeys.Count == 0)
        {
            _captureComplete = true;
            UpdateSaveButton();
        }
    }

    private void ProcessMouseKey(VirtualKey key)
    {
        if (!_isListening || _captureComplete) return;
        _actionKey = key;
        _captureComplete = true;
        UpdateDisplay();
        UpdateSaveButton();
    }

    private static bool IsModifierKey(VirtualKey key) => key switch
    {
        VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => true,
        VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => true,
        VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => true,
        VirtualKey.LeftWindows or VirtualKey.RightWindows => true,
        _ => false
    };

    private static VirtualKey CanonicalModifier(VirtualKey key) => key switch
    {
        VirtualKey.LeftControl or VirtualKey.RightControl => VirtualKey.Control,
        VirtualKey.LeftMenu or VirtualKey.RightMenu => VirtualKey.Menu,
        VirtualKey.LeftShift or VirtualKey.RightShift => VirtualKey.Shift,
        VirtualKey.LeftWindows or VirtualKey.RightWindows => VirtualKey.LeftWindows,
        _ => key
    };

    private void ResetRecording()
    {
        _modifiers.Clear();
        _actionKey = null;
        _keyNames.Clear();
        _pressedKeys.Clear();
        _captureComplete = false;
        IsPrimaryButtonEnabled = false;
        WarningBar.IsOpen = false;
        ConflictBar.IsOpen = false;
        ResetButton.IsEnabled = false;
    }

    private void UpdateDisplay()
    {
        _keyNames.Clear();

        if (_modifiers.Contains(VirtualKey.LeftWindows))
            _keyNames.Add("Win");
        if (_modifiers.Contains(VirtualKey.Control))
            _keyNames.Add("Ctrl");
        if (_modifiers.Contains(VirtualKey.Menu))
            _keyNames.Add("Alt");
        if (_modifiers.Contains(VirtualKey.Shift))
            _keyNames.Add("Shift");

        if (_actionKey.HasValue)
            _keyNames.Add(ModifierHelper.GetKeyName(_actionKey.Value));
    }

    private void UpdateSaveButton()
    {
        bool hasModifier = _modifiers.Count > 0;
        bool hasAction = _actionKey.HasValue;
        IsPrimaryButtonEnabled = hasAction;
        ResetButton.IsEnabled = hasModifier || hasAction;

        IsUnconventional = hasAction && !hasModifier;

        bool hasConflict = false;
        if (hasAction && _registry != null)
        {
            uint mod = 0;
            if (_modifiers.Contains(VirtualKey.LeftWindows)) mod |= MOD_WIN;
            if (_modifiers.Contains(VirtualKey.Control)) mod |= MOD_CONTROL;
            if (_modifiers.Contains(VirtualKey.Menu)) mod |= MOD_ALT;
            if (_modifiers.Contains(VirtualKey.Shift)) mod |= MOD_SHIFT;
            var (isConflict, isSystem, name) = _registry.CheckConflict(mod, _actionKey!.Value, _toolName);
            hasConflict = isConflict;
            if (isConflict)
            {
                ConflictBar.Title = _conflictTitle;
                ConflictBar.Message = $"{(isSystem ? _conflictSystemPrefix : _conflictAppPrefix)}{name}";
            }
        }
        ConflictBar.IsOpen = hasConflict;

        WarningBar.IsOpen = IsUnconventional && !hasConflict;
    }


    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
#pragma warning disable CA2249
        if (_modifiers.Contains(VirtualKey.LeftWindows))
            SelectedModifier |= MOD_WIN;
        if (_modifiers.Contains(VirtualKey.Control))
            SelectedModifier |= MOD_CONTROL;
        if (_modifiers.Contains(VirtualKey.Menu))
            SelectedModifier |= MOD_ALT;
        if (_modifiers.Contains(VirtualKey.Shift))
            SelectedModifier |= MOD_SHIFT;
#pragma warning restore CA2249

        SelectedKey = _actionKey ?? VirtualKey.None;
        ResetRecording();
    }

    // --- Low-level keyboard hook ---

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static LowLevelKeyboardProc? _keyboardHookProc;
    private static GCHandle _keyboardHookHandle;
    private static IntPtr _keyboardHookId = IntPtr.Zero;
    private static KeyRecorderDialog? _activeInstance;

    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private void InstallKeyboardHook()
    {
        if (_keyboardHookId != IntPtr.Zero) return;

        _activeInstance = this;
        _keyboardHookProc = KeyboardHookProc;
        _keyboardHookHandle = GCHandle.Alloc(_keyboardHookProc);

        var hMod = GetModuleHandle(null);
        _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookProc, hMod, 0);

        if (_keyboardHookId == IntPtr.Zero)
        {
            _keyboardHookHandle.Free();
            _keyboardHookProc = null;
            _activeInstance = null;
        }
    }

    private void RemoveKeyboardHook()
    {
        if (_keyboardHookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_keyboardHookId);
        _keyboardHookId = IntPtr.Zero;
        _keyboardHookHandle.Free();
        _keyboardHookProc = null;
        _activeInstance = null;
    }

    private static IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var instance = _activeInstance;
            if (instance != null && instance._isListening)
            {
                var msg = (int)wParam;
                var vkCode = Marshal.ReadInt32(lParam);
                var key = (VirtualKey)vkCode;

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (key == VirtualKey.Escape || key == VirtualKey.Enter || key == VirtualKey.Tab)
                        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

                    if (!instance._captureComplete)
                        instance._dispatcher.TryEnqueue(() => instance.ProcessKey(key));

                    return (IntPtr)1;
                }

                if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (!instance._captureComplete)
                        instance._dispatcher.TryEnqueue(() => instance.ProcessKeyUp(key));

                    return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    // --- Low-level mouse hook ---

    private static LowLevelKeyboardProc? _mouseHookProc;
    private static GCHandle _mouseHookHandle;
    private static IntPtr _mouseHookId = IntPtr.Zero;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x201;
    private const int WM_RBUTTONDOWN = 0x204;
    private const int WM_MBUTTONDOWN = 0x207;
    private const int WM_XBUTTONDOWN = 0x20B;

    private void InstallMouseHook()
    {
        if (_mouseHookId != IntPtr.Zero) return;

        _mouseHookProc = MouseHookProc;
        _mouseHookHandle = GCHandle.Alloc(_mouseHookProc);

        var hMod = GetModuleHandle(null);
        _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc, hMod, 0);

        if (_mouseHookId == IntPtr.Zero)
        {
            _mouseHookHandle.Free();
            _mouseHookProc = null;
        }
    }

    private void RemoveMouseHook()
    {
        if (_mouseHookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_mouseHookId);
        _mouseHookId = IntPtr.Zero;
        _mouseHookHandle.Free();
        _mouseHookProc = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint XBUTTON1 = 0x0001;
    private const uint XBUTTON2 = 0x0002;

    private static IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            var instance = _activeInstance;
            if (instance != null && instance._isListening && !instance._captureComplete)
            {
                var msg = (int)wParam;
                if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN)
                {
                    VirtualKey? mouseKey = msg switch
                    {
                        WM_LBUTTONDOWN => VirtualKey.LeftButton,
                        WM_RBUTTONDOWN => VirtualKey.RightButton,
                        WM_MBUTTONDOWN => VirtualKey.MiddleButton,
                        WM_XBUTTONDOWN => null,
                        _ => null
                    };

                    if (msg == WM_XBUTTONDOWN)
                    {
                        var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        var xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
                        mouseKey = xButton switch
                        {
                            XBUTTON1 => VirtualKey.XButton1,
                            XBUTTON2 => VirtualKey.XButton2,
                            _ => null
                        };
                    }

                    if (mouseKey.HasValue)
                    {
                        instance._dispatcher.TryEnqueue(() => instance.ProcessMouseKey(mouseKey.Value));
                        return (IntPtr)1;
                    }
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
