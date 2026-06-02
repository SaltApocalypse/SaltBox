using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SaltBox.Services;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SaltBox.ViewModels;

public partial class DeveloperModeViewModel : ObservableObject
{
    private readonly DeveloperModeService _developerService;
    private readonly DispatcherTimer _refreshTimer;

    public CultureService Lang { get; }

    [ObservableProperty]
    private string _fullLogText = "";

    [ObservableProperty]
    private string _exportStatusText = "";

    [ObservableProperty]
    private Visibility _showExportResult = Visibility.Collapsed;

    public LogLevelFilter[] LogLevels => Enum.GetValues<LogLevelFilter>();

    public LogLevelFilter SelectedMinLevel
    {
        get => _developerService.SelectedMinLevel;
        set
        {
            if (_developerService.SelectedMinLevel == value)
                return;
            _developerService.SelectedMinLevel = value;
            RefreshLog();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScrollToBottomVisible))]
    private bool _isAtBottom = true;

    public Visibility ScrollToBottomVisible => IsAtBottom ? Visibility.Collapsed : Visibility.Visible;

    public Action? ScrollToBottomRequested { get; set; }

    public DeveloperModeViewModel(DeveloperModeService developerService, CultureService lang)
    {
        _developerService = developerService;
        Lang = lang;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += OnTimerTick;
        _refreshTimer.Start();

        RefreshLog();
    }

    public void StopRefresh()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnTimerTick;
    }

    [RelayCommand]
    private void CopyLogs()
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(FullLogText);
            Clipboard.SetContent(dataPackage);
        }
        catch { }
    }

    [RelayCommand]
    private async Task ExportLogs()
    {
        try
        {
            var logDir = GetLogDirectory();
            Directory.CreateDirectory(logDir);
            var fileName = $"saltbox-logs-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            var filePath = Path.Combine(logDir, fileName);
            await File.WriteAllTextAsync(filePath, FullLogText);
            ExportStatusText = $"{Lang.DevExportSuccess}: {filePath}";
            ShowExportResult = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ExportStatusText = $"{Lang.DevExportFailed}: {ex.Message}";
            ShowExportResult = Visibility.Visible;
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var logDir = GetLogDirectory();
        if (Directory.Exists(logDir))
            Process.Start("explorer.exe", logDir);
    }

    [RelayCommand]
    private void ScrollToBottom()
    {
        IsAtBottom = true;
        ScrollToBottomRequested?.Invoke();
    }

    private void OnTimerTick(object? sender, object e)
    {
        FullLogText = string.Join(Environment.NewLine,
            _developerService.LogEntries.Select(e => e.FullLine));
        if (IsAtBottom)
            ScrollToBottomRequested?.Invoke();
    }

    private void RefreshLog()
    {
        FullLogText = string.Join(Environment.NewLine,
            _developerService.LogEntries.Select(e => e.FullLine));
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
}
