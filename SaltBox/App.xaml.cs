using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Serilog;
using SaltBox.Services;
using SaltBox.ViewModels;
using SaltBox.Views;

namespace SaltBox;

public partial class App : Application
{
    private IHost _host;

    static App()
    {
        InitLogging();
    }

    private static void InitLogging()
    {
        var baseDir = AppContext.BaseDirectory;
        var logPath = Path.Combine(baseDir, "Logs", "log-.txt");

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
        var log = _host.Services.GetRequiredService<LogService>();
        log.Info("SaltBox launched");

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Activate();
    }
}
