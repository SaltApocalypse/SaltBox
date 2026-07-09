using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using Windows.Management.Deployment;
using Windows.Storage;
using SaltBox.Config;
using SaltBox.Contracts;
using SaltBox.Modules.DeveloperMode;
using SaltBox.Modules.FileExtractor;
using SaltBox.Modules.CustomExplorerActions;
using SaltBox.Modules.Screenshot;
using SaltBox.Services;
using SaltBox.Services.ExplorerIntegration;
using SaltBox.ViewModels;
using SaltBox.Views;
using System.Runtime.InteropServices;
using System.Threading;
using Velopack;

namespace SaltBox;

public partial class App : Application
{
    private readonly IHost _host;
    private static readonly Mutex? _singleInstanceMutex;
    private static readonly bool _isFirstInstance;
    private static bool _isFirstRun;
    private static InMemoryLogSink? _memorySink;

    static App()
    {
        VelopackApp.Build()
            .OnFirstRun(_ => _isFirstRun = true)
            .Run();
        InitLogging();
        _singleInstanceMutex = new Mutex(true, "SaltBox-SingleInstance", out _isFirstInstance);
    }

    private static void RegisterIdentityPackage()
    {
        try
        {
            var msixName = "SaltBox.Identity.msix";
            var msixPath = Path.Combine(AppContext.BaseDirectory, msixName);
            if (!File.Exists(msixPath))
            {
                Log.Warning("Identity package not found: {Path}", msixPath);
                return;
            }

            var pm = new PackageManager();
            var options = new AddPackageOptions
            {
                ExternalLocationUri = new Uri(AppContext.BaseDirectory)
            };

            var result = pm.AddPackageByUriAsync(new Uri(msixPath), options).GetAwaiter().GetResult();
            if (result.ExtendedErrorCode is { } ex)
            {
                Log.Error("Identity package registration failed: 0x{Code:X8} — {Error}",
                    ex.HResult, result.ErrorText);
            }
            else
            {
                Log.Information("Identity package registered successfully");
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Identity package registration skipped: {Message}", ex.Message);
        }
    }

    private static string GetLogDirectory()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "SaltBox", "Logs");
        }
    }

    private static void InitLogging()
    {
        var logDir = GetLogDirectory();
        var logPath = Path.Combine(logDir, "log-.txt");

        _memorySink = new InMemoryLogSink();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(_memorySink, Serilog.Events.LogEventLevel.Debug)
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
                services.AddSingleton<UpdateService>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<IConfigService, ConfigService>();
                services.AddSingleton<InMemoryLogSink>(_ => _memorySink!);
                services.AddSingleton<DeveloperModeService>();
                services.AddSingleton<ExplorerVariableResolver>();
                services.AddSingleton<ExplorerRegistration>();
                services.AddSingleton<ExplorerActionManager>();
                services.AddSingleton<ExplorerDispatcher>();
                services.AddSingleton<FileExtractorService>();
                services.AddSingleton<IExplorerActionHandler>(sp => sp.GetRequiredService<FileExtractorService>());
                services.AddTransient<DeveloperModeViewModel>();
                services.AddTransient<DeveloperModePage>();
                services.AddTransient<HomeViewModel>();
                services.AddTransient<ScreenshotViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<FileExtractorViewModel>();
                services.AddTransient<FileExtractorPage>();
                services.AddTransient<CustomExplorerActionsViewModel>();
                services.AddTransient<CustomExplorerActionsPage>();
                services.AddTransient<HomePage>();
                services.AddTransient<ScreenshotPage>();
                services.AddTransient<SettingsPage>();
            })
            .Build();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        var dispatcher = _host.Services.GetRequiredService<ExplorerDispatcher>();
        if (dispatcher.Dispatch(cmdArgs))
            return;

        if (!_isFirstInstance)
        {
            ActivateExistingInstance();
            Exit();
            return;
        }

        var log = _host.Services.GetRequiredService<LogService>();
        log.Info("SaltBox launched");

        var configService = _host.Services.GetRequiredService<IConfigService>();
        configService.EnsureDirectories();

        if (_isFirstRun)
        {
            log.Info("First run after install, registering identity package");
            _ = Task.Run(() => RegisterIdentityPackage());
        }

        var config = _host.Services.GetRequiredService<IConfiguration>();
        var updateSection = config.GetSection("Update");
        if (!string.IsNullOrEmpty(updateSection["Type"]))
        {
            var updater = _host.Services.GetRequiredService<UpdateService>();
            updater.ConfigureFromConfig(config);
        }

        // Initialize Explorer integration infrastructure (register context menus)
        var actionManager = _host.Services.GetRequiredService<ExplorerActionManager>();
        actionManager.Initialize();

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
