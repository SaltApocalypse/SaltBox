using Microsoft.UI.Xaml;
using Serilog;
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
        catch (Exception ex)
        {
            Log.Warning("Failed to load theme: {Message}", ex.Message);
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
        catch (Exception ex)
        {
            Log.Warning("Failed to save theme: {Message}", ex.Message);
        }
    }
}
