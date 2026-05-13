using CommunityToolkit.Mvvm.ComponentModel;
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
            ["HomeTitle"] = "Welcome to SaltBox",
            ["HomeSubtitle"] = "Your personal Windows toolbox",
            ["HomeToolsButton"] = "Explore Tools",
            ["SettingsTitle"] = "Settings",
            ["SettingsTheme"] = "App theme",
            ["SettingsThemeSystem"] = "Follow system",
            ["SettingsThemeDark"] = "Dark",
            ["SettingsThemeLight"] = "Light",
            ["SettingsLanguage"] = "Language",
            ["SettingsThemeDescription"] = "Choose app theme",
            ["SettingsLanguageDescription"] = "Choose display language",
        },
        ["zh-CN"] = new()
        {
            ["NavHome"] = "主页",
            ["NavTools"] = "工具",
            ["NavScreenshot"] = "屏幕截图",
            ["HomeTitle"] = "欢迎使用 SaltBox",
            ["HomeSubtitle"] = "您的个人 Windows 工具箱",
            ["HomeToolsButton"] = "浏览工具",
            ["SettingsTitle"] = "设置",
            ["SettingsTheme"] = "应用主题",
            ["SettingsThemeSystem"] = "跟随系统",
            ["SettingsThemeDark"] = "夜间",
            ["SettingsThemeLight"] = "日间",
            ["SettingsLanguage"] = "语言",
            ["SettingsThemeDescription"] = "选择应用主题",
            ["SettingsLanguageDescription"] = "选择显示语言",
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
        catch
        {
            return "en-US";
        }
    }

    public string NavHome => _strings.GetValueOrDefault(nameof(NavHome), "Home");
    public string NavTools => _strings.GetValueOrDefault(nameof(NavTools), "Tools");
    public string NavScreenshot => _strings.GetValueOrDefault(nameof(NavScreenshot), "Screenshot");
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
        catch { }
        return null;
    }

    private static void SaveCulture(string code)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values["AppLanguage"] = code;
        }
        catch { }
    }
}
