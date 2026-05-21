using CommunityToolkit.Mvvm.ComponentModel;
using SaltBox.Models;
using Serilog.Events;

namespace SaltBox.Services;

public enum LogLevelFilter
{
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

public partial class DeveloperModeService : ObservableObject
{
    private readonly InMemoryLogSink _logSink;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private LogLevelFilter _selectedMinLevel = LogLevelFilter.Information;

    public DeveloperModeService(InMemoryLogSink logSink)
    {
        _logSink = logSink;
    }

    private LogEventLevel ToLogEventLevel() => _selectedMinLevel switch
    {
        LogLevelFilter.Debug => LogEventLevel.Debug,
        LogLevelFilter.Information => LogEventLevel.Information,
        LogLevelFilter.Warning => LogEventLevel.Warning,
        LogLevelFilter.Error => LogEventLevel.Error,
        LogLevelFilter.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };

    public List<LogEntry> GetEntries() => _logSink.GetEntries(ToLogEventLevel());

    partial void OnSelectedMinLevelChanged(LogLevelFilter value)
    {
        OnPropertyChanged(nameof(LogEntries));
    }

    public List<LogEntry> LogEntries => GetEntries();
}
