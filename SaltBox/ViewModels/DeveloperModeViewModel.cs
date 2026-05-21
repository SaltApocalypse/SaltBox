using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using SaltBox.Models;
using SaltBox.Services;

namespace SaltBox.ViewModels;

public partial class DeveloperModeViewModel : ObservableObject
{
    private readonly DeveloperModeService _developerService;
    private readonly DispatcherTimer _refreshTimer;

    public CultureService Lang { get; }

    private List<LogEntry> _logEntries = [];

    public List<LogEntry> LogEntries
    {
        get => _logEntries;
        set => SetProperty(ref _logEntries, value);
    }

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

    private void OnTimerTick(object? sender, object e)
    {
        LogEntries = _developerService.LogEntries;
    }

    private void RefreshLog()
    {
        LogEntries = _developerService.LogEntries;
    }
}
