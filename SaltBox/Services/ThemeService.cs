using Microsoft.UI.Xaml;
using SaltBox.Config;
using SaltBox.Contracts;
using Serilog;

namespace SaltBox.Services;

public class ThemeService
{
    private readonly IConfigService _config;

    public ElementTheme CurrentTheme { get; private set; }

    public event Action<ElementTheme>? ThemeChanged;

    public ThemeService(IConfigService config)
    {
        _config = config;
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

    private ElementTheme LoadTheme()
    {
        try
        {
            var appConfig = _config.Load<AppConfig>();
            if (Enum.TryParse<ElementTheme>(appConfig.Theme, out var theme))
                return theme;
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to load theme: {Message}", ex.Message);
        }

        return ElementTheme.Default;
    }

    private void SaveTheme(ElementTheme theme)
    {
        try
        {
            var appConfig = _config.Load<AppConfig>();
            appConfig.Theme = theme.ToString();
            _config.Save(appConfig);
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to save theme: {Message}", ex.Message);
        }
    }
}
