using SaltBox.Models;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace SaltBox.Services;

public class InMemoryLogSink : ILogEventSink
{
    private readonly int _maxEntries;
    private readonly ConcurrentQueue<(DateTimeOffset Timestamp, LogEventLevel Level, string Message)> _entries = new();

    public InMemoryLogSink(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = (logEvent.Timestamp, logEvent.Level, logEvent.RenderMessage());
        _entries.Enqueue(entry);
        while (_entries.Count > _maxEntries)
            _entries.TryDequeue(out _);
    }

    public string GetFormattedLogs(LogEventLevel minLevel = LogEventLevel.Verbose)
    {
        return string.Join(Environment.NewLine,
            _entries
                .Where(e => e.Level >= minLevel)
                .OrderByDescending(e => e.Timestamp)
                .Select(e => $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Level}] {e.Message}"));
    }

    public List<LogEntry> GetEntries(LogEventLevel minLevel = LogEventLevel.Verbose)
    {
        return _entries
            .Where(e => e.Level >= minLevel)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new LogEntry(e.Timestamp, e.Level, e.Message))
            .ToList();
    }
}
