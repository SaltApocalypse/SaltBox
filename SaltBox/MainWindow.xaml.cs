using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using SaltBox.Services;
using SaltBox.Views;
using System.Runtime.InteropServices;

namespace SaltBox;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly LogService _log;
    private readonly ThemeService _theme;
    private readonly AppWindow _appWindow;
    private readonly DeveloperModeService _devMode;
    private ScreenshotService? _screenshot;
    private TrayService? _tray;
    private string? _currentTag;

    public CultureService Lang { get; }

    public MainWindow(IServiceProvider services, LogService log, ThemeService theme, CultureService lang, DeveloperModeService devMode)
    {
        _services = services;
        _log = log;
        _theme = theme;
        _devMode = devMode;
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
                "DeveloperMode" => Lang.NavDeveloperMode,
                "Settings" => Lang.SettingsTitle,
                _ => ""
            };
        };

        if (AppNotificationManager.IsSupported())
            try { AppNotificationManager.Default.Register(); } catch (Exception ex) { _log.Warn($"Notification registration failed: {ex.Message}"); }

        _screenshot = _services.GetRequiredService<ScreenshotService>();
        _screenshot.RegisterGlobalHotkey();
        Closed += (_, _) => _screenshot.UnregisterGlobalHotkey();

        _tray = _services.GetRequiredService<TrayService>();
        _tray.Initialize();

        _devMode.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DeveloperModeService.IsEnabled))
                DevModeNavItem.Visibility = _devMode.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        };
        DevModeNavItem.Visibility = _devMode.IsEnabled ? Visibility.Visible : Visibility.Collapsed;

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

    public void NavigateToSettings()
    {
        if (_currentTag == "Settings")
            return;

        _currentTag = "Settings";
        NavigateInternal("Settings");
        ShowWindowFromTray();
    }

    private void ShowWindowFromTray()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = ShowWindow(hwnd, 5);
        _ = SetForegroundWindow(hwnd);
    }

    private void NavigateInternal(string tag)
    {
        Type? pageType = tag switch
        {
            "Home" => typeof(HomePage),
            "Screenshot" => typeof(ScreenshotPage),
            "DeveloperMode" => typeof(DeveloperModePage),
            "Settings" => typeof(SettingsPage),
            _ => null
        };

        if (pageType is null)
            return;

        var page = (Page)_services.GetRequiredService(pageType);
        if (page is HomePage homePage)
            homePage.ViewModel.NavigateToTool = NavigateTo;

        ContentFrame.Content = page;

        NavView.Header = tag switch
        {
            "Home" => Lang.NavHome,
            "Screenshot" => Lang.NavScreenshot,
            "DeveloperMode" => Lang.NavDeveloperMode,
            "Settings" => Lang.SettingsTitle,
            _ => ""
        };

        if (_currentTag == "DeveloperMode")
            NavView.SelectedItem = DevModeNavItem;
        else if (_currentTag == "Settings")
            NavView.SelectedItem = (NavigationViewItem)NavView.SettingsItem;


    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
