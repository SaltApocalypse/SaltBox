using Serilog;

namespace SaltBox.Services;

public class LogService
{
    public void Info(string message) => Log.Information(message);
    public void Warn(string message) => Log.Warning(message);
    public void Error(string message, Exception? ex = null)
    {
        if (ex is null) Log.Error(message);
        else Log.Error(ex, message);
    }
    public void Debug(string message) => Log.Debug(message);
}
