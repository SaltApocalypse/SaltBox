using Microsoft.UI.Xaml;
using Windows.Storage;

namespace SaltBox.Services;

public class ThemeService
{
    private const string SettingsKey = "AppTheme";

    public ElementTheme CurrentTheme { get; private set; }

    public event Action<ElementTheme>? ThemeChanged;

    public ThemeService()
    {
        CurrentTheme = LoadTheme();
    }

    public void ApplyTheme(FrameworkElement root)
    {
        root.RequestedTheme = CurrentTheme;
    }

    public void SetTheme(ElementTheme theme)
    {
        CurrentTheme = theme;
        SaveTheme(theme);
        ThemeChanged?.Invoke(theme);
    }

    private static ElementTheme LoadTheme()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(SettingsKey, out var value) && value is string str)
                return Enum.TryParse<ElementTheme>(str, out var theme) ? theme : ElementTheme.Default;
        }
        catch
        {
            // No package identity (dev without sparse package) — use default
        }

        return ElementTheme.Default;
    }

    private static void SaveTheme(ElementTheme theme)
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[SettingsKey] = theme.ToString();
        }
        catch
        {
            // No package identity — silently ignore
        }
    }
}
