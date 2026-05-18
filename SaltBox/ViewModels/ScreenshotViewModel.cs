using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SaltBox.Services;
using Windows.Storage;
using Windows.System;

namespace SaltBox.ViewModels;

public enum NotificationMode
{
    None,
    Text,
    Preview
}

public partial class ScreenshotViewModel : ObservableObject
{
    private readonly ScreenshotService _screenshotService;
    private readonly ShortcutRegistry _shortcutRegistry;
    private readonly LogService _log;
    public CultureService Lang { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CaptureCommand))]
    private bool _isCapturing;

    [ObservableProperty]
    private NotificationMode _notificationMode;

    public int NotificationModeIndex
    {
        get => (int)NotificationMode;
        set => NotificationMode = (NotificationMode)value;
    }

    public bool HasNotificationHint => NotificationMode != NotificationMode.None;

    partial void OnNotificationModeChanged(NotificationMode value)
    {
        OnPropertyChanged(nameof(NotificationModeIndex));
        OnPropertyChanged(nameof(HasNotificationHint));
        SaveNotificationMode(value);
    }

    [ObservableProperty]
    private DisplayInfo? _selectedDisplay;

    [ObservableProperty]
    private string _savePath = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public InfoBarSeverity StatusSeverity => InfoBarSeverity.Informational;

    private bool _shortcutIsUnconventional;
    public bool ShortcutIsUnconventional
    {
        get => _shortcutIsUnconventional;
        set
        {
            if (SetProperty(ref _shortcutIsUnconventional, value))
                OnPropertyChanged(nameof(ShortcutUnconventionalVisible));
        }
    }

    private string _shortcutUnconventionalMessage = "";
    public string ShortcutUnconventionalMessage
    {
        get => _shortcutUnconventionalMessage;
        set => SetProperty(ref _shortcutUnconventionalMessage, value);
    }

    public bool ShortcutUnconventionalVisible => ShortcutIsUnconventional && !ShortcutHasConflict;

    private bool _shortcutHasConflict;
    public bool ShortcutHasConflict
    {
        get => _shortcutHasConflict;
        set
        {
            if (SetProperty(ref _shortcutHasConflict, value))
                OnPropertyChanged(nameof(ShortcutUnconventionalVisible));
        }
    }

    private string _shortcutConflictMessage = "";
    public string ShortcutConflictMessage
    {
        get => _shortcutConflictMessage;
        set => SetProperty(ref _shortcutConflictMessage, value);
    }

    public List<DisplayInfo> Displays { get; }

    private uint _shortcutModifier = 0x8;
    private VirtualKey _shortcutKey = VirtualKey.F2;

    public uint ShortcutModifier => _shortcutModifier;
    public VirtualKey ShortcutKey => _shortcutKey;

    public List<string> ShortcutKeyNames => GetKeyNames();
    public string ShortcutDisplayText => string.Join(" ", ShortcutKeyNames);

    public ScreenshotViewModel(CultureService lang, ScreenshotService screenshotService, ShortcutRegistry shortcutRegistry, LogService log)
    {
        Lang = lang;
        _screenshotService = screenshotService;
        _shortcutRegistry = shortcutRegistry;
        _log = log;

        IsEnabled = LoadIsEnabled();
        var savedMode = LoadNotificationMode();
        _notificationMode = savedMode;
        OnPropertyChanged(nameof(NotificationModeIndex));

        var displays = _screenshotService.GetDisplays();
        Displays = displays;

        var savedDisplay = LoadSelectedDisplay();
        SelectedDisplay = displays.FirstOrDefault(d => d.DeviceName == savedDisplay)
                       ?? displays.FirstOrDefault(d => d.IsPrimary)
                       ?? displays.FirstOrDefault();

        SavePath = LoadSavePath() ?? GetDefaultScreenshotPath();

        _shortcutModifier = LoadShortcutModifier();
        _shortcutKey = LoadShortcutKey();
        _screenshotService.PrepareShortcut(_shortcutModifier, _shortcutKey);
        _shortcutRegistry.Register("Screenshot", _shortcutModifier, _shortcutKey);
    }

    private List<string> GetKeyNames()
    {
        var names = new List<string>();
        if ((_shortcutModifier & 0x8) != 0) names.Add("Win");
        if ((_shortcutModifier & 0x2) != 0) names.Add("Ctrl");
        if ((_shortcutModifier & 0x1) != 0) names.Add("Alt");
        if ((_shortcutModifier & 0x4) != 0) names.Add("Shift");
        names.Add(GetVirtualKeyName(_shortcutKey));
        return names;
    }

    private static string GetVirtualKeyName(VirtualKey key)
    {
        if (key >= VirtualKey.F1 && key <= VirtualKey.F12)
            return $"F{key - VirtualKey.F1 + 1}";
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
            return $"{(char)('0' + key - VirtualKey.Number0)}";
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
            return $"{(char)('A' + key - VirtualKey.A)}";
        return key.ToString();
    }

    public void UpdateShortcut(uint modifier, VirtualKey key)
    {
        var (hasConflict, isSystem, conflictName) = _shortcutRegistry.CheckConflict(modifier, key);
        ShortcutHasConflict = hasConflict;
        var displayName = isSystem ? conflictName : GetToolDisplayName(conflictName);
        ShortcutConflictMessage = hasConflict
            ? $"{(isSystem ? Lang.ShortcutConflictSystem : Lang.ShortcutConflictApp)}{displayName}"
            : "";

        _shortcutModifier = modifier;
        _shortcutKey = key;
        OnPropertyChanged(nameof(ShortcutModifier));
        OnPropertyChanged(nameof(ShortcutKey));
        OnPropertyChanged(nameof(ShortcutKeyNames));
        OnPropertyChanged(nameof(ShortcutDisplayText));
        SaveShortcut(modifier, key);
        _screenshotService.UpdateHotkey(modifier, key);
        _shortcutRegistry.Register("Screenshot", modifier, key);
        _log.Info($"Shortcut updated to modifier={modifier}, key={key}");

        ShortcutIsUnconventional = modifier == 0 && key != VirtualKey.None;
        ShortcutUnconventionalMessage = Lang.ShortcutWarningMessage;
        OnPropertyChanged(nameof(ShortcutUnconventionalVisible));
    }

    private string GetToolDisplayName(string toolName)
    {
        return toolName switch
        {
            "Screenshot" => Lang.NavScreenshot,
            _ => toolName
        };
    }

    public void ResetShortcut()
    {
        UpdateShortcut(0x8, VirtualKey.F2); // Default: Win + F2
    }

    public void ClearShortcutWarning()
    {
        ShortcutIsUnconventional = false;
        ShortcutUnconventionalMessage = "";
    }

    partial void OnSavePathChanged(string value)
    {
        SavePathSetting(value);
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
        OnPropertyChanged(nameof(StatusSeverity));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        SaveIsEnabled(value);
        if (!value)
            StatusMessage = "";
    }

    partial void OnSelectedDisplayChanged(DisplayInfo? value)
    {
        if (value != null)
            SaveSelectedDisplay(value.DeviceName);
    }

    private bool CanCapture() => IsEnabled && !IsCapturing;

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task PickFolder()
    {
        var path = await _screenshotService.PickFolderAsync();
        if (path != null)
            SavePath = path;
    }

    [RelayCommand]
    private void ResetPath()
    {
        SavePath = GetDefaultScreenshotPath();
        StatusMessage = Lang.ScreenshotStatusPathReset;
        _log.Info("Save path reset to default");
    }

    [RelayCommand]
    private void OpenSaveFolder()
    {
        try
        {
            Process.Start("explorer.exe", SavePath);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open save folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task IdentifyDisplay()
    {
        IsCapturing = true;
        try
        {
            await _screenshotService.IdentifyDisplaysAsync();
        }
        finally
        {
            IsCapturing = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task Capture()
    {
        if (SelectedDisplay is null)
        {
            StatusMessage = Lang.ScreenshotStatusNoDisplay;
            _log.Warn("No display selected for capture");
            return;
        }

        IsCapturing = true;
        StatusMessage = Lang.ScreenshotStatusCapturing;

        try
        {
            var result = await _screenshotService.CaptureScreenshotAsync(SelectedDisplay, SavePath);
            if (result != null)
            {
                StatusMessage = $"{Lang.ScreenshotStatusSaved} {result}";
                SendNotification(result);
                _log.Info($"Capture complete: {result}");
            }
            else
            {
                StatusMessage = Lang.ScreenshotStatusFailed;
                SendNotification(null);
                _log.Error("Capture failed");
            }
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException is { } inner ? $"{ex.Message} — {inner.Message}" : ex.Message;
            StatusMessage = $"{Lang.ScreenshotStatusFailed} {detail}";
            SendNotification(null);
            _log.Error($"Capture threw: {detail}", ex);
        }
        finally
        {
            IsCapturing = false;
        }
    }

    private static string GetDefaultScreenshotPath()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pictures, "Screenshots");
    }

    private static NotificationMode LoadNotificationMode()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ScreenshotNotificationMode", out var v) && v is int i)
                return (NotificationMode)i;
        }
        catch { }
        return NotificationMode.Text;
    }

    private static void SaveNotificationMode(NotificationMode mode)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values["ScreenshotNotificationMode"] = (int)mode;
        }
        catch { }
    }

    private void SendNotification(string? imagePath)
    {
        if (NotificationMode == NotificationMode.None)
            return;

        if (!AppNotificationManager.IsSupported())
        {
            _log.Warn("Notifications not supported: singleton runtime package missing");
            return;
        }

        try
        {
            var builder = new AppNotificationBuilder()
                .AddText(Lang.ScreenshotNotificationTitle)
                .AddText(StatusMessage)
                .SetScenario(AppNotificationScenario.Urgent);

            if (NotificationMode == NotificationMode.Preview && imagePath != null)
                builder.SetAppLogoOverride(new Uri(imagePath));

            AppNotificationManager.Default.Show(builder.BuildNotification());
            _log.Info("Notification sent");
        }
        catch (Exception ex)
        {
            _log.Error($"Notification failed: {ex.Message}");
        }
    }

    private static bool LoadIsEnabled()
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

    private static void SaveIsEnabled(bool value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values["ScreenshotEnabled"] = value;
        }
        catch { }
    }

    private static string? LoadSavePath()
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

    private static void SavePathSetting(string path)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values["ScreenshotSavePath"] = path;
        }
        catch { }
    }

    private static string? LoadSelectedDisplay()
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

    private static void SaveSelectedDisplay(string deviceName)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values["ScreenshotDisplay"] = deviceName;
        }
        catch { }
    }

    private static uint LoadShortcutModifier()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ShortcutModifier", out var v) && v is uint u)
                return u;
        }
        catch { }
        return 0x8;
    }

    private static VirtualKey LoadShortcutKey()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("ShortcutKey", out var v) && v is uint u)
                return (VirtualKey)u;
        }
        catch { }
        return VirtualKey.F2;
    }

    private static void SaveShortcut(uint modifier, VirtualKey key)
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values["ShortcutModifier"] = modifier;
            settings.Values["ShortcutKey"] = (uint)key;
        }
        catch { }
    }
}
