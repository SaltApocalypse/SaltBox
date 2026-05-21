using Microsoft.UI.Xaml.Media;
using Serilog.Events;
using Windows.UI;

namespace SaltBox.Models;

public record LogEntry(DateTimeOffset Timestamp, LogEventLevel Level, string Message)
{
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");

    public string FormattedLevel => Level switch
    {
        LogEventLevel.Verbose => "[VRB]",
        LogEventLevel.Debug => "[DBG]",
        LogEventLevel.Information => "[INF]",
        LogEventLevel.Warning => "[WRN]",
        LogEventLevel.Error => "[ERR]",
        LogEventLevel.Fatal => "[FTL]",
        _ => "[???]"
    };

    public SolidColorBrush LevelColor => Level switch
    {
        LogEventLevel.Verbose => new SolidColorBrush(Color.FromArgb(255, 128, 128, 128)),
        LogEventLevel.Debug => new SolidColorBrush(Color.FromArgb(255, 100, 180, 255)),
        LogEventLevel.Information => new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
        LogEventLevel.Warning => new SolidColorBrush(Color.FromArgb(255, 255, 165, 0)),
        LogEventLevel.Error => new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
        LogEventLevel.Fatal => new SolidColorBrush(Color.FromArgb(255, 139, 0, 0)),
        _ => new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
    };

    public string FullLine => $"{FormattedTimestamp} {FormattedLevel} {Message}";
}
