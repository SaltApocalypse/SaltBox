using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Windows.Storage;
using SaltBox.Services;
using SaltBox.ViewModels;
using SaltBox.Views;
using System.Runtime.InteropServices;
using System.Threading;

namespace SaltBox;

public partial class App : Application
{
    private readonly IHost _host;
    private static readonly Mutex? _singleInstanceMutex;
    private static readonly bool _isFirstInstance;

    static App()
    {
        InitLogging();
        _singleInstanceMutex = new Mutex(true, "SaltBox-SingleInstance", out _isFirstInstance);
    }

    private static void InitLogging()
    {
        var logDir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs");
        var logPath = Path.Combine(logDir, "log-.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public App()
    {
        InitializeComponent();
        _host = CreateHost();
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<CultureService>();
                services.AddSingleton<LogService>();
                services.AddSingleton<ToolRegistry>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<ShortcutRegistry>();
                services.AddSingleton<ScreenshotService>();
                services.AddSingleton<TrayService>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<ScreenshotViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<HomePage>();
                services.AddTransient<ScreenshotPage>();
                services.AddTransient<SettingsPage>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_isFirstInstance)
        {
            ActivateExistingInstance();
            Exit();
            return;
        }

        var log = _host.Services.GetRequiredService<LogService>();
        log.Info("SaltBox launched");

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Activate();
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static void ActivateExistingInstance()
    {
        var hwnd = FindWindowW(null, "SaltBox");
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }
}
