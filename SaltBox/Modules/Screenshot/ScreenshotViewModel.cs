using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SaltBox.Contracts;
using SaltBox.Helpers;
using SaltBox.Services;
using Serilog;
using Windows.System;

namespace SaltBox.Modules.Screenshot;

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
    private readonly IConfigService _configService;
    private ScreenshotConfig _config = new();
    public CultureService Lang { get; }

    private bool _isEnabled;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;
            _isEnabled = value;
            OnPropertyChanged();
            _config.IsEnabled = value;
            Save();
            _screenshotService.IsEnabled = value;
            if (!value)
                StatusMessage = "";
            CaptureCommand.NotifyCanExecuteChanged();
        }
    }

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
        _config.NotificationMode = (int)value;
        Save();
        _screenshotService.NotificationMode = value;
    }

    [ObservableProperty]
    private HdrMode _hdrMode;

    public int HdrModeIndex
    {
        get => (int)HdrMode;
        set => HdrMode = (HdrMode)value;
    }

    partial void OnHdrModeChanged(HdrMode value)
    {
        OnPropertyChanged(nameof(HdrModeIndex));
        _config.HdrMode = (int)value;
        Save();
        _screenshotService.HdrModeOverride = value;
        _log.Info($"HDR mode changed to {value}");
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

    private uint _shortcutModifier = ModifierHelper.MOD_WIN;
    private VirtualKey _shortcutKey = VirtualKey.F2;

    public uint ShortcutModifier => _shortcutModifier;
    public VirtualKey ShortcutKey => _shortcutKey;

    public List<string> ShortcutKeyNames => ModifierHelper.GetKeyNames(_shortcutModifier, _shortcutKey);
    public string ShortcutDisplayText => string.Join(" ", ShortcutKeyNames);

    public ScreenshotViewModel(CultureService lang, ScreenshotService screenshotService, ShortcutRegistry shortcutRegistry, LogService log, IConfigService configService)
    {
        Lang = lang;
        _screenshotService = screenshotService;
        _shortcutRegistry = shortcutRegistry;
        _log = log;
        _configService = configService;
        _config = configService.Load<ScreenshotConfig>();

        IsEnabled = _config.IsEnabled;
        _screenshotService.IsEnabled = _config.IsEnabled;

        _notificationMode = (NotificationMode)_config.NotificationMode;
        _screenshotService.NotificationMode = _notificationMode;
        OnPropertyChanged(nameof(NotificationModeIndex));

        _hdrMode = (HdrMode)_config.HdrMode;
        _screenshotService.HdrModeOverride = _hdrMode;
        OnPropertyChanged(nameof(HdrModeIndex));

        var displays = _screenshotService.GetDisplays();
        Displays = displays;

        SelectedDisplay = displays.FirstOrDefault(d => d.DeviceName == _config.SelectedDisplay)
                       ?? displays.FirstOrDefault(d => d.IsPrimary)
                       ?? displays.FirstOrDefault();
        _screenshotService.SelectedDisplay = SelectedDisplay?.DeviceName ?? "";

        SavePath = _config.SavePath ?? GetDefaultScreenshotPath();
        _screenshotService.SavePath = SavePath;

        _shortcutModifier = _config.ShortcutModifier;
        _shortcutKey = (VirtualKey)_config.ShortcutKey;
        _screenshotService.PrepareShortcut(_shortcutModifier, _shortcutKey);
        _shortcutRegistry.Register("Screenshot", _shortcutModifier, _shortcutKey);
    }

    public void UpdateShortcut(uint modifier, VirtualKey key)
    {
        var (hasConflict, isSystem, conflictName) = _shortcutRegistry.CheckConflict(modifier, key, "Screenshot");
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
        _config.ShortcutModifier = modifier;
        _config.ShortcutKey = (uint)key;
        Save();
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
        _config.SavePath = value;
        Save();
        _screenshotService.SavePath = value;
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
        OnPropertyChanged(nameof(StatusSeverity));
    }

    partial void OnSelectedDisplayChanged(DisplayInfo? value)
    {
        if (value != null)
        {
            _config.SelectedDisplay = value.DeviceName;
            Save();
            _screenshotService.SelectedDisplay = value.DeviceName;
        }
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
                .AddArgument("action", "openScreenshotFolder")
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

    private void Save()
    {
        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save config: {ex.Message}");
        }
    }
}
