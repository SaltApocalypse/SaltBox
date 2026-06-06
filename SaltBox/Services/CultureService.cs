using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Windows.Globalization;
using Windows.Storage;

namespace SaltBox.Services;

public partial class CultureService : ObservableObject
{
    private static readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["en-US"] = new()
        {
            ["NavHome"] = "Home",
            ["NavTools"] = "Tools",
            ["NavScreenshot"] = "Screenshot",
            ["NavDeveloperMode"] = "Developer Mode",
            ["HomeTitle"] = "Welcome to SaltBox",
            ["HomeSubtitle"] = "Your personal Windows toolbox",
            ["HomeToolsButton"] = "Explore Tools",
            ["SettingsTitle"] = "Settings",
            ["SettingsTheme"] = "App theme",
            ["SettingsThemeDescription"] = "Choose app theme",
            ["SettingsThemeSystem"] = "Follow system",
            ["SettingsThemeDark"] = "Dark",
            ["SettingsThemeLight"] = "Light",
            ["SettingsLanguage"] = "Language",
            ["SettingsLanguageDescription"] = "Choose display language",
            ["SettingsAppVersion"] = "Software version",
            ["SettingsUpdates"] = "Updates",
            ["SettingsUpdateCheck"] = "Check for updates",
            ["SettingsUpdateCheckButton"] = "Check",
            ["SettingsUpdateCheckManual"] = "Check for updates",
            ["SettingsUpdateChecking"] = "Checking for updates...",
            ["SettingsUpdateDownload"] = "Download update",
            ["SettingsUpdateDownloadButton"] = "Download",
            ["SettingsUpdateDownloading"] = "Downloading update...",
            ["SettingsUpdateInstall"] = "Restart to install",
            ["SettingsUpdateInstallButton"] = "Restart now",
            ["SettingsUpdateUpToDate"] = "You have the latest version",
            ["SettingsUpdateAvailable"] = "New version {0} available",
            ["SettingsUpdateNowButton"] = "Update now",
            ["SettingsUpdateDownloadLink"] = "Download from GitHub",
            ["SettingsCurrentVersion"] = "Current version",
            ["ScreenshotDescription"] = "Global screenshot tool, triggered by shortcut key.",
            ["ScreenshotEnable"] = "Enable screenshot",
            ["ScreenshotEnableDescription"] = "Enable or disable the screenshot feature",
            ["ScreenshotEnableOn"] = "On",
            ["ScreenshotEnableOff"] = "Off",
            ["ScreenshotConfig"] = "Configuration",
            ["ScreenshotDisplay"] = "Capture display",
            ["ScreenshotDisplayDescription"] = "Select which display to capture",
            ["ScreenshotSavePath"] = "Save path",
            ["ScreenshotSavePathDescription"] = "Folder where screenshots are saved",
            ["ScreenshotShortcut"] = "Shortcut key",
            ["ScreenshotStatusNoDisplay"] = "No display selected",
            ["ScreenshotStatusCapturing"] = "Capturing...",
            ["ScreenshotStatusSaved"] = "Saved:",
            ["ScreenshotStatusFailed"] = "Capture failed",
            ["ScreenshotStatusPathReset"] = "Save path reset to default",
            ["ScreenshotOpenPath"] = "Open save path",
            ["ScreenshotOpenPathButton"] = "Open folder",
            ["ScreenshotResetPath"] = "Reset",
            ["ScreenshotResetPathHeader"] = "Reset to default path",
            ["ScreenshotCurrentPath"] = "Current path",
            ["ScreenshotDisplaySelect"] = "Select a display",
            ["ScreenshotDisplayIdentify"] = "Identify",
            ["Browse"] = "Browse\u2026",
            ["ScreenshotNotificationMode"] = "Notification",
            ["ScreenshotNotificationNone"] = "No notification",
            ["ScreenshotNotificationText"] = "Text",
            ["ScreenshotNotificationPreview"] = "Screenshot preview",
            ["ScreenshotNotificationTitle"] = "SaltBox",
            ["ScreenshotNotificationSystemHint"] = "If notifications do not appear, enable SaltBox notifications in System Settings → System → Notifications & actions.",
            ["ScreenshotTest"] = "Function test",
            ["ScreenshotTestButton"] = "Screenshot",
            ["ScreenshotTestLabel"] = "Test",
            ["ScreenshotTestResult"] = "Result",
            ["ScreenshotShortcutCurrent"] = "Current shortcut",
            ["ScreenshotShortcutModify"] = "Modify",
            ["ScreenshotShortcutReset"] = "Restore default shortcut",
            ["ScreenshotShortcutResetButton"] = "Reset",
            ["KeyRecorderTitle"] = "Activate Shortcut",
            ["KeyRecorderInstruction"] = "Press the key combination you want to use, then click Save.",
            ["KeyRecorderReset"] = "Clear",
            ["ShortcutConflictTitle"] = "Shortcut conflict",
            ["ShortcutConflictSystem"] = "Conflict with system shortcut: ",
            ["ShortcutConflictApp"] = "Already in use by: ",
            ["ShortcutWarningTitle"] = "Unconventional shortcut",
            ["ShortcutWarningMessage"] = "This shortcut has no modifier key (Win, Ctrl, Alt, or Shift) and may interfere with normal typing.",
            ["Save"] = "Save",
            ["Cancel"] = "Cancel",
            ["SettingsDevModeTitle"] = "Developer",
            ["SettingsDevMode"] = "Developer Mode",
            ["SettingsDevModeDescription"] = "Show developer diagnostics page in the navigation panel.",
            ["SettingsDevModeOn"] = "On",
            ["SettingsDevModeOff"] = "Off",
            ["DevLogsTitle"] = "Logs",
            ["DevLogLevel"] = "Minimum log level",
            ["DevCopyLogs"] = "Copy & Export",
            ["DevCopyLogsDesc"] = "Copy logs to clipboard or export to file",
            ["DevCopyButton"] = "Copy",
            ["DevExportButton"] = "Export",
            ["DevExportSuccess"] = "Exported successfully",
            ["DevExportFailed"] = "Export failed",
            ["DevOpenLogFolder"] = "Open folder",
            ["HomeUpdateBanner"] = "New version v{0} -> v{1} available",
        },
        ["zh-CN"] = new()
        {
            ["NavHome"] = "主页",
            ["NavTools"] = "工具",
            ["NavScreenshot"] = "屏幕截图",
            ["NavDeveloperMode"] = "开发者模式",
            ["HomeTitle"] = "欢迎使用 SaltBox",
            ["HomeSubtitle"] = "您的个人 Windows 工具箱",
            ["HomeToolsButton"] = "浏览工具",
            ["SettingsTitle"] = "设置",
            ["SettingsTheme"] = "应用主题",
            ["SettingsThemeDescription"] = "选择应用主题",
            ["SettingsThemeSystem"] = "跟随系统",
            ["SettingsThemeDark"] = "夜间",
            ["SettingsThemeLight"] = "日间",
            ["SettingsLanguage"] = "语言",
            ["SettingsLanguageDescription"] = "选择显示语言",
            ["SettingsAppVersion"] = "软件版本",
            ["SettingsUpdates"] = "软件更新",
            ["SettingsUpdateCheck"] = "检查更新",
            ["SettingsUpdateCheckButton"] = "检查",
            ["SettingsUpdateCheckManual"] = "手动检查更新",
            ["SettingsUpdateChecking"] = "正在检查更新...",
            ["SettingsUpdateDownload"] = "下载更新",
            ["SettingsUpdateDownloadButton"] = "下载",
            ["SettingsUpdateDownloading"] = "正在下载更新...",
            ["SettingsUpdateInstall"] = "重启以安装更新",
            ["SettingsUpdateInstallButton"] = "立即重启",
            ["SettingsUpdateUpToDate"] = "当前是最新版本",
            ["SettingsUpdateAvailable"] = "发现新版本 {0}",
            ["SettingsUpdateNowButton"] = "立即更新",
            ["SettingsUpdateDownloadLink"] = "从 GitHub 下载",
            ["SettingsCurrentVersion"] = "当前版本",
            ["ScreenshotDescription"] = "全局的截图工具，使用快捷键触发。",
            ["ScreenshotEnable"] = "启用屏幕截图",
            ["ScreenshotEnableDescription"] = "启用或禁用屏幕截图功能",
            ["ScreenshotEnableOn"] = "开",
            ["ScreenshotEnableOff"] = "关",
            ["ScreenshotConfig"] = "配置",
            ["ScreenshotDisplay"] = "截屏的显示器",
            ["ScreenshotDisplayDescription"] = "选择要截取的显示器",
            ["ScreenshotSavePath"] = "保存路径",
            ["ScreenshotSavePathDescription"] = "截图文件保存的文件夹",
            ["ScreenshotShortcut"] = "快捷键",
            ["ScreenshotStatusNoDisplay"] = "未选择显示器",
            ["ScreenshotStatusCapturing"] = "截图中...",
            ["ScreenshotStatusSaved"] = "已保存:",
            ["ScreenshotStatusFailed"] = "截图失败",
            ["ScreenshotStatusPathReset"] = "保存路径已重置为默认",
            ["ScreenshotOpenPath"] = "打开截图保存路径",
            ["ScreenshotOpenPathButton"] = "打开文件夹",
            ["ScreenshotResetPath"] = "重置",
            ["ScreenshotResetPathHeader"] = "恢复默认路径",
            ["ScreenshotCurrentPath"] = "当前路径",
            ["ScreenshotDisplaySelect"] = "选择显示器",
            ["ScreenshotDisplayIdentify"] = "标识",
            ["Browse"] = "浏览\u2026",
            ["ScreenshotNotificationMode"] = "通知方式",
            ["ScreenshotNotificationNone"] = "无通知",
            ["ScreenshotNotificationText"] = "文本通知",
            ["ScreenshotNotificationPreview"] = "截图预览",
            ["ScreenshotNotificationTitle"] = "SaltBox",
            ["ScreenshotNotificationSystemHint"] = "若通知未显示，请在系统设置 → 系统 → 通知中为 SaltBox 开启通知权限。",
            ["ScreenshotTest"] = "功能测试",
            ["ScreenshotTestButton"] = "截图",
            ["ScreenshotTestLabel"] = "测试",
            ["ScreenshotTestResult"] = "结果",
            ["ScreenshotShortcutCurrent"] = "当前快捷键",
            ["ScreenshotShortcutModify"] = "修改",
            ["ScreenshotShortcutReset"] = "恢复默认快捷键",
            ["ScreenshotShortcutResetButton"] = "重置",
            ["KeyRecorderTitle"] = "激活快捷键",
            ["KeyRecorderInstruction"] = "按下您想要使用的快捷键组合，然后点击保存。",
            ["KeyRecorderReset"] = "清除",
            ["ShortcutConflictTitle"] = "快捷键冲突",
            ["ShortcutConflictSystem"] = "与系统快捷键冲突：",
            ["ShortcutConflictApp"] = "已被其他功能占用：",
            ["ShortcutWarningTitle"] = "非常规快捷键",
            ["ShortcutWarningMessage"] = "此快捷键没有修饰键（Win、Ctrl、Alt 或 Shift），可能会干扰正常输入。",
            ["Save"] = "保存",
            ["Cancel"] = "取消",
            ["SettingsDevModeTitle"] = "开发者",
            ["SettingsDevMode"] = "开发者模式",
            ["SettingsDevModeDescription"] = "在导航面板中显示开发者诊断页面。",
            ["SettingsDevModeOn"] = "开",
            ["SettingsDevModeOff"] = "关",
            ["DevLogsTitle"] = "日志",
            ["DevLogLevel"] = "最低日志级别",
            ["DevCopyLogs"] = "复制与导出",
            ["DevCopyLogsDesc"] = "复制日志到剪贴板或导出到文件",
            ["DevCopyButton"] = "复制",
            ["DevExportButton"] = "导出",
            ["DevExportSuccess"] = "导出成功",
            ["DevExportFailed"] = "导出失败",
            ["DevOpenLogFolder"] = "打开文件夹",
            ["HomeUpdateBanner"] = "发现新版本 v{0} -> v{1}",
        }
    };

    private Dictionary<string, string> _strings;

    public string CurrentCulture { get; private set; }

    public CultureService()
    {
        var saved = LoadSavedCulture();
        CurrentCulture = string.IsNullOrEmpty(saved) ? DetectSystemCulture() : saved;
        _strings = _resources.GetValueOrDefault(CurrentCulture, _resources["en-US"]);
        SaveCulture(CurrentCulture);
    }

    private static string DetectSystemCulture()
    {
        try
        {
            var lang = ApplicationLanguages.Languages.FirstOrDefault() ?? "en-US";
            return lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to detect system culture: {Message}", ex.Message);
            return "en-US";
        }
    }

    public string NavHome => _strings.GetValueOrDefault(nameof(NavHome), "Home");
    public string NavTools => _strings.GetValueOrDefault(nameof(NavTools), "Tools");
    public string NavScreenshot => _strings.GetValueOrDefault(nameof(NavScreenshot), "Screenshot");
    public string NavDeveloperMode => _strings.GetValueOrDefault(nameof(NavDeveloperMode), "Developer Mode");
    public string HomeTitle => _strings.GetValueOrDefault(nameof(HomeTitle), "Welcome to SaltBox");
    public string HomeSubtitle => _strings.GetValueOrDefault(nameof(HomeSubtitle), "Your personal Windows toolbox");
    public string HomeToolsButton => _strings.GetValueOrDefault(nameof(HomeToolsButton), "Explore Tools");
    public string SettingsTitle => _strings.GetValueOrDefault(nameof(SettingsTitle), "Settings");
    public string SettingsTheme => _strings.GetValueOrDefault(nameof(SettingsTheme), "App theme");
    public string SettingsThemeSystem => _strings.GetValueOrDefault(nameof(SettingsThemeSystem), "Follow system");
    public string SettingsThemeDark => _strings.GetValueOrDefault(nameof(SettingsThemeDark), "Dark");
    public string SettingsThemeLight => _strings.GetValueOrDefault(nameof(SettingsThemeLight), "Light");
    public string SettingsLanguage => _strings.GetValueOrDefault(nameof(SettingsLanguage), "Language");
    public string SettingsThemeDescription => _strings.GetValueOrDefault(nameof(SettingsThemeDescription), "Choose app theme");
    public string SettingsLanguageDescription => _strings.GetValueOrDefault(nameof(SettingsLanguageDescription), "Choose display language");
    public string SettingsAppVersion => _strings.GetValueOrDefault(nameof(SettingsAppVersion), "Software version");
    public string SettingsUpdates => _strings.GetValueOrDefault(nameof(SettingsUpdates), "Updates");
    public string SettingsUpdateCheck => _strings.GetValueOrDefault(nameof(SettingsUpdateCheck), "Check for updates");
    public string SettingsUpdateCheckButton => _strings.GetValueOrDefault(nameof(SettingsUpdateCheckButton), "Check");
    public string SettingsUpdateCheckManual => _strings.GetValueOrDefault(nameof(SettingsUpdateCheckManual), "Check for updates");
    public string SettingsUpdateChecking => _strings.GetValueOrDefault(nameof(SettingsUpdateChecking), "Checking for updates...");
    public string SettingsUpdateDownload => _strings.GetValueOrDefault(nameof(SettingsUpdateDownload), "Download update");
    public string SettingsUpdateDownloadButton => _strings.GetValueOrDefault(nameof(SettingsUpdateDownloadButton), "Download");
    public string SettingsUpdateDownloading => _strings.GetValueOrDefault(nameof(SettingsUpdateDownloading), "Downloading update...");
    public string SettingsUpdateInstall => _strings.GetValueOrDefault(nameof(SettingsUpdateInstall), "Restart to install");
    public string SettingsUpdateInstallButton => _strings.GetValueOrDefault(nameof(SettingsUpdateInstallButton), "Restart now");
    public string SettingsUpdateUpToDate => _strings.GetValueOrDefault(nameof(SettingsUpdateUpToDate), "You have the latest version");
    public string SettingsUpdateAvailable => _strings.GetValueOrDefault(nameof(SettingsUpdateAvailable), "New version {0} available");
    public string SettingsUpdateNowButton => _strings.GetValueOrDefault(nameof(SettingsUpdateNowButton), "Update now");
    public string SettingsUpdateDownloadLink => _strings.GetValueOrDefault(nameof(SettingsUpdateDownloadLink), "Download from GitHub");
    public string SettingsCurrentVersion => _strings.GetValueOrDefault(nameof(SettingsCurrentVersion), "Current version");
    public string ScreenshotDescription => _strings.GetValueOrDefault(nameof(ScreenshotDescription), "Global screenshot tool, triggered by shortcut key.");
    public string ScreenshotEnable => _strings.GetValueOrDefault(nameof(ScreenshotEnable), "Enable screenshot");
    public string ScreenshotEnableDescription => _strings.GetValueOrDefault(nameof(ScreenshotEnableDescription), "Enable or disable the screenshot feature");
    public string ScreenshotEnableOn => _strings.GetValueOrDefault(nameof(ScreenshotEnableOn), "On");
    public string ScreenshotEnableOff => _strings.GetValueOrDefault(nameof(ScreenshotEnableOff), "Off");
    public string ScreenshotConfig => _strings.GetValueOrDefault(nameof(ScreenshotConfig), "Configuration");
    public string ScreenshotDisplay => _strings.GetValueOrDefault(nameof(ScreenshotDisplay), "Capture display");
    public string ScreenshotDisplayDescription => _strings.GetValueOrDefault(nameof(ScreenshotDisplayDescription), "Select which display to capture");
    public string ScreenshotSavePath => _strings.GetValueOrDefault(nameof(ScreenshotSavePath), "Save path");
    public string ScreenshotSavePathDescription => _strings.GetValueOrDefault(nameof(ScreenshotSavePathDescription), "Folder where screenshots are saved");
    public string ScreenshotShortcut => _strings.GetValueOrDefault(nameof(ScreenshotShortcut), "Shortcut key");
    public string Browse => _strings.GetValueOrDefault(nameof(Browse), "Browse\u2026");
    public string ScreenshotStatusNoDisplay => _strings.GetValueOrDefault(nameof(ScreenshotStatusNoDisplay), "No display selected");
    public string ScreenshotStatusCapturing => _strings.GetValueOrDefault(nameof(ScreenshotStatusCapturing), "Capturing...");
    public string ScreenshotStatusSaved => _strings.GetValueOrDefault(nameof(ScreenshotStatusSaved), "Saved:");
    public string ScreenshotStatusFailed => _strings.GetValueOrDefault(nameof(ScreenshotStatusFailed), "Capture failed");
    public string ScreenshotStatusPathReset => _strings.GetValueOrDefault(nameof(ScreenshotStatusPathReset), "Save path reset to default");
    public string ScreenshotResetPath => _strings.GetValueOrDefault(nameof(ScreenshotResetPath), "Reset");
    public string ScreenshotResetPathHeader => _strings.GetValueOrDefault(nameof(ScreenshotResetPathHeader), "Reset to default path");
    public string ScreenshotOpenPath => _strings.GetValueOrDefault(nameof(ScreenshotOpenPath), "Open save path");
    public string ScreenshotOpenPathButton => _strings.GetValueOrDefault(nameof(ScreenshotOpenPathButton), "Open folder");
    public string ScreenshotCurrentPath => _strings.GetValueOrDefault(nameof(ScreenshotCurrentPath), "Current path");
    public string ScreenshotDisplaySelect => _strings.GetValueOrDefault(nameof(ScreenshotDisplaySelect), "Select a display");
    public string ScreenshotDisplayIdentify => _strings.GetValueOrDefault(nameof(ScreenshotDisplayIdentify), "Identify");
    public string ScreenshotNotificationMode => _strings.GetValueOrDefault(nameof(ScreenshotNotificationMode), "Notification");
    public string ScreenshotNotificationNone => _strings.GetValueOrDefault(nameof(ScreenshotNotificationNone), "No notification");
    public string ScreenshotNotificationText => _strings.GetValueOrDefault(nameof(ScreenshotNotificationText), "Text");
    public string ScreenshotNotificationPreview => _strings.GetValueOrDefault(nameof(ScreenshotNotificationPreview), "Screenshot preview");
    public string ScreenshotNotificationTitle => _strings.GetValueOrDefault(nameof(ScreenshotNotificationTitle), "SaltBox");
    public string ScreenshotNotificationSystemHint => _strings.GetValueOrDefault(nameof(ScreenshotNotificationSystemHint), "If notifications do not appear, enable SaltBox notifications in System Settings.");
    public string ScreenshotTest => _strings.GetValueOrDefault(nameof(ScreenshotTest), "Test capture");
    public string ScreenshotTestButton => _strings.GetValueOrDefault(nameof(ScreenshotTestButton), "Screenshot");
    public string ScreenshotTestLabel => _strings.GetValueOrDefault(nameof(ScreenshotTestLabel), "Test");
    public string ScreenshotTestResult => _strings.GetValueOrDefault(nameof(ScreenshotTestResult), "Result");
    public string ScreenshotShortcutCurrent => _strings.GetValueOrDefault(nameof(ScreenshotShortcutCurrent), "Current shortcut");
    public string ScreenshotShortcutModify => _strings.GetValueOrDefault(nameof(ScreenshotShortcutModify), "Modify");
    public string ScreenshotShortcutReset => _strings.GetValueOrDefault(nameof(ScreenshotShortcutReset), "Restore default shortcut");
    public string ScreenshotShortcutResetButton => _strings.GetValueOrDefault(nameof(ScreenshotShortcutResetButton), "Reset");
    public string KeyRecorderTitle => _strings.GetValueOrDefault(nameof(KeyRecorderTitle), "Activate Shortcut");
    public string KeyRecorderInstruction => _strings.GetValueOrDefault(nameof(KeyRecorderInstruction), "Press the key combination you want to use, then click Save.");
    public string KeyRecorderReset => _strings.GetValueOrDefault(nameof(KeyRecorderReset), "Clear");
    public string ShortcutConflictTitle => _strings.GetValueOrDefault(nameof(ShortcutConflictTitle), "Shortcut conflict");
    public string ShortcutConflictSystem => _strings.GetValueOrDefault(nameof(ShortcutConflictSystem), "Conflict with system shortcut: ");
    public string ShortcutConflictApp => _strings.GetValueOrDefault(nameof(ShortcutConflictApp), "Already in use by: ");
    public string ShortcutWarningTitle => _strings.GetValueOrDefault(nameof(ShortcutWarningTitle), "Unconventional shortcut");
    public string ShortcutWarningMessage => _strings.GetValueOrDefault(nameof(ShortcutWarningMessage), "This shortcut has no modifier key (Win, Ctrl, Alt, or Shift) and may interfere with normal typing.");
    public string Save => _strings.GetValueOrDefault(nameof(Save), "Save");
    public string Cancel => _strings.GetValueOrDefault(nameof(Cancel), "Cancel");
    public string SettingsDevMode => _strings.GetValueOrDefault(nameof(SettingsDevMode), "Developer Mode");
    public string SettingsDevModeTitle => _strings.GetValueOrDefault(nameof(SettingsDevModeTitle), "Developer");
    public string SettingsDevModeDescription => _strings.GetValueOrDefault(nameof(SettingsDevModeDescription), "Show developer diagnostics page in the navigation panel.");
    public string SettingsDevModeOn => _strings.GetValueOrDefault(nameof(SettingsDevModeOn), "On");
    public string SettingsDevModeOff => _strings.GetValueOrDefault(nameof(SettingsDevModeOff), "Off");
    public string DevLogsTitle => _strings.GetValueOrDefault(nameof(DevLogsTitle), "Logs");
    public string DevLogLevel => _strings.GetValueOrDefault(nameof(DevLogLevel), "Minimum log level");
    public string DevCopyLogs => _strings.GetValueOrDefault(nameof(DevCopyLogs), "Copy & Export");
    public string DevCopyLogsDesc => _strings.GetValueOrDefault(nameof(DevCopyLogsDesc), "Copy logs to clipboard or export to file");
    public string DevCopyButton => _strings.GetValueOrDefault(nameof(DevCopyButton), "Copy");
    public string DevExportButton => _strings.GetValueOrDefault(nameof(DevExportButton), "Export");
    public string DevExportSuccess => _strings.GetValueOrDefault(nameof(DevExportSuccess), "Exported successfully");
    public string DevExportFailed => _strings.GetValueOrDefault(nameof(DevExportFailed), "Export failed");
    public string DevOpenLogFolder => _strings.GetValueOrDefault(nameof(DevOpenLogFolder), "Open folder");
    public string HomeUpdateBanner => _strings.GetValueOrDefault(nameof(HomeUpdateBanner), "New version v{0} -> v{1} available");

    public void SetCulture(string code)
    {
        if (code == CurrentCulture)
            return;

        if (!_resources.ContainsKey(code))
            return;

        CurrentCulture = code;
        _strings = _resources[code];
        SaveCulture(code);
        OnPropertyChanged((string?)null);
    }

    private static string? LoadSavedCulture()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue("AppLanguage", out var val) && val is string s)
                return s;
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to load AppLanguage: {Message}", ex.Message);
        }
        return null;
    }

    private static void SaveCulture(string code)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values["AppLanguage"] = code;
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to save AppLanguage: {Message}", ex.Message);
        }
    }
}
