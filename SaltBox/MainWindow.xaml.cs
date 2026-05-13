using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaltBox.Services;
using SaltBox.Views;

namespace SaltBox;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly LogService _log;
    private readonly ThemeService _theme;
    private readonly AppWindow _appWindow;
    private string? _currentTag;

    public CultureService Lang { get; }

    public MainWindow(IServiceProvider services, LogService log, ThemeService theme, CultureService lang)
    {
        _services = services;
        _log = log;
        _theme = theme;
        Lang = lang;

        InitializeComponent();

        _appWindow = AppWindow;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (ExtendsContentIntoTitleBar)
        {
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }

        Title = "SaltBox";

        _theme.ThemeChanged += OnThemeChanged;
        _theme.ApplyTheme(RootGrid);
    }

    private void OnThemeChanged(ElementTheme theme)
    {
        RootGrid.RequestedTheme = theme;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (NavView.SettingsItem is NavigationViewItem settingsItem)
            settingsItem.Content = Lang.SettingsTitle;

        Lang.PropertyChanged += (_, _) =>
        {
            if (NavView.SettingsItem is NavigationViewItem item)
                item.Content = Lang.SettingsTitle;

            NavView.Header = _currentTag switch
            {
                "Home" => Lang.NavHome,
                "Screenshot" => Lang.NavScreenshot,
                "Settings" => Lang.SettingsTitle,
                _ => ""
            };
        };

        NavView.SelectedItem = NavView.MenuItems[0];
        NavigateTo("Home");
        _log.Info("MainWindow loaded");
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = args.InvokedItemContainer?.Tag?.ToString()
                  ?? (args.IsSettingsInvoked ? "Settings" : null);

        NavigateTo(tag);
    }

    private void OnPaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavigateTo(string? tag)
    {
        if (tag is null || tag == _currentTag)
            return;

        _currentTag = tag;
        NavigateInternal(tag);
    }

    private void NavigateInternal(string tag)
    {
        Type? pageType = tag switch
        {
            "Home" => typeof(HomePage),
            "Screenshot" => typeof(ScreenshotPage),
            "Settings" => typeof(SettingsPage),
            _ => null
        };

        if (pageType is null)
            return;

        ContentFrame.Content = _services.GetRequiredService(pageType);

        NavView.Header = tag switch
        {
            "Home" => Lang.NavHome,
            "Screenshot" => Lang.NavScreenshot,
            "Settings" => Lang.SettingsTitle,
            _ => ""
        };

        if (_currentTag == "Settings")
            NavView.SelectedItem = (NavigationViewItem)NavView.SettingsItem;

        _log.Info($"Navigated to {tag}");
    }
}
