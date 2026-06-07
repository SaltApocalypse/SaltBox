using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SaltBox.Services;
using Windows.Storage;

namespace SaltBox.Modules.FileExtractor;

public class FileExtractorService
{
    private const string SettingKey = "EnableFileExtractor";
    private static readonly string SettingsFallbackPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaltBox", "Settings", "FileExtractor.txt");
    private const string NotificationModeSettingKey = "FileExtractorNotificationMode";
    private static readonly string NotificationModeFallbackPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaltBox", "Settings", "FileExtractorNotificationMode.txt");

    private readonly LogService _log;
    private readonly ContextMenuManager _menuManager;
    private readonly CultureService _lang;

    public FileExtractorService(LogService log, ContextMenuManager menuManager, CultureService lang)
    {
        _log = log;
        _menuManager = menuManager;
        _lang = lang;
    }

    public bool IsEnabled
    {
        get => LoadIsEnabled();
        set
        {
            SaveIsEnabled(value);
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                _log.Error("Cannot update context menu: unable to determine executable path");
                return;
            }
            if (value)
            {
                var command = $"\"{exePath}\" --extract-files \"%1\"";
                _menuManager.RegisterSubItem("FileExtractor", "文件提取", command);
            }
            else
            {
                _menuManager.UnregisterSubItem("FileExtractor");
            }
        }
    }

    public void ApplyStartupSetting()
    {
        _menuManager.Cleanup();

        if (!IsEnabled)
        {
            _menuManager.UnregisterSubItem("FileExtractor");
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            _log.Error("Cannot apply startup setting: unable to determine executable path");
            return;
        }
        var command = $"\"{exePath}\" --extract-files \"%1\"";
        _menuManager.RegisterSubItem("FileExtractor", "文件提取", command);
    }

    public void ExtractFiles(string rootFolderPath)
    {
        _log.Info($"Starting file extraction for: {rootFolderPath}");

        var subDirs = Directory.GetDirectories(rootFolderPath, "*", SearchOption.AllDirectories);
        var successCount = 0;
        var failedCount = 0;

        foreach (var dir in subDirs)
        {
            var files = Directory.GetFiles(dir);
            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                var destPath = Path.Combine(rootFolderPath, fileName);

                if (File.Exists(destPath))
                    destPath = GetUniqueFilePath(rootFolderPath, fileName);

                try
                {
                    File.Move(filePath, destPath);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _log.Error($"Failed to move file '{filePath}': {ex.Message}");
                    failedCount++;
                }
            }
        }

        _log.Info($"File extraction completed for: {rootFolderPath}, {successCount} moved, {failedCount} failed");
        TrySendNotification(rootFolderPath, successCount, failedCount);
    }

    private void TrySendNotification(string rootFolderPath, int success, int failed)
    {
        if (LoadNotificationModeValue() == 0)
            return;

        if (!AppNotificationManager.IsSupported())
            return;

        try
        {
            var folderName = Path.GetFileName(rootFolderPath);
            var resultLine = string.Format(_lang.FileExtractorNotificationResultFormat, success, failed);
            var body = $"{folderName} ({rootFolderPath})\n{resultLine}";

            var builder = new AppNotificationBuilder()
                .AddText(_lang.FileExtractorNotificationTitle)
                .AddText(body)
                .AddArgument("action", "openFileExtractorFolder")
                .AddArgument("folderPath", rootFolderPath)
                .SetScenario(AppNotificationScenario.Urgent);

            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to send file extraction notification: {ex.Message}");
        }
    }

    private static int LoadNotificationModeValue()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(NotificationModeSettingKey, out var v) && v is int i)
                return i;
        }
        catch
        {
        }

        return LoadNotificationModeFallback();
    }

    private static int LoadNotificationModeFallback()
    {
        try
        {
            if (File.Exists(NotificationModeFallbackPath))
            {
                var text = File.ReadAllText(NotificationModeFallbackPath).Trim();
                if (int.TryParse(text, out var val))
                    return val;
            }
        }
        catch
        {
        }
        return 1;
    }

    private static string GetUniqueFilePath(string folder, string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);

        for (int i = 1; ; i++)
        {
            var newName = $"{nameWithoutExt} ({i}){ext}";
            var newPath = Path.Combine(folder, newName);
            if (!File.Exists(newPath))
                return newPath;
        }
    }

    private static bool LoadIsEnabled()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(SettingKey, out var v) && v is bool b)
                return b;
        }
        catch
        {
        }

        return LoadIsEnabledFallback();
    }

    private static void SaveIsEnabled(bool value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = value;
            return;
        }
        catch
        {
        }

        SaveIsEnabledFallback(value);
    }

    private static bool LoadIsEnabledFallback()
    {
        try
        {
            if (File.Exists(SettingsFallbackPath))
            {
                var text = File.ReadAllText(SettingsFallbackPath).Trim();
                return text == "1";
            }
        }
        catch
        {
        }
        return false;
    }

    private static void SaveIsEnabledFallback(bool value)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFallbackPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsFallbackPath, value ? "1" : "0");
        }
        catch
        {
        }
    }
}
