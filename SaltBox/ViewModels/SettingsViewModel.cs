using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using SaltBox.Services;

namespace SaltBox.ViewModels;

public record LanguageOption(string Code, string DisplayName);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _themeService;
    private readonly CultureService _culture;

    public SettingsViewModel(ThemeService themeService, CultureService culture)
    {
        _themeService = themeService;
        _culture = culture;
        Lang = culture;

        SelectedThemeIndex = _themeService.CurrentTheme switch
        {
            ElementTheme.Default => 0,
            ElementTheme.Dark => 1,
            ElementTheme.Light => 2,
            _ => 0
        };

        SelectedLanguage = Languages.FirstOrDefault(
            l => l.Code == _culture.CurrentCulture);
    }

    public CultureService Lang { get; }

    public List<LanguageOption> Languages { get; } = new()
    {
        new LanguageOption("en-US", "English"),
        new LanguageOption("zh-CN", "中文 (简体)")
    };

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            0 => ElementTheme.Default,
            1 => ElementTheme.Dark,
            2 => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        _themeService.SetTheme(theme);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null)
        {
            _culture.SetCulture(value.Code);
        }
    }
}
