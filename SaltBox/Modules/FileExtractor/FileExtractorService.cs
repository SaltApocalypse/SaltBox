using SaltBox.Services;
using Windows.Storage;

namespace SaltBox.Modules.FileExtractor;

public class FileExtractorService
{
    private const string SettingKey = "EnableFileExtractor";
    private static readonly string SettingsFallbackPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaltBox", "Settings", "FileExtractor.txt");

    private readonly LogService _log;
    private readonly ContextMenuManager _menuManager;

    public FileExtractorService(LogService log, ContextMenuManager menuManager)
    {
        _log = log;
        _menuManager = menuManager;
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
        var totalMoved = 0;

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
                    totalMoved++;
                }
                catch (Exception ex)
                {
                    _log.Error($"Failed to move file '{filePath}': {ex.Message}");
                }
            }
        }

        _log.Info($"File extraction completed for: {rootFolderPath}, {totalMoved} files moved");
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
