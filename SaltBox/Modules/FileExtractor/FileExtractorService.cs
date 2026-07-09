using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using SaltBox.Contracts;
using SaltBox.Services;
using SaltBox.Services.ExplorerIntegration;

namespace SaltBox.Modules.FileExtractor;

public class FileExtractorService : IExplorerActionHandler
{
    private readonly LogService _log;
    private readonly CultureService _lang;
    private readonly IConfigService _configService;
    private FileExtractorConfig _config;

    string IExplorerActionHandler.ActionId => "SaltBox.FileExtractor";
    ExplorerTarget IExplorerActionHandler.Target => ExplorerTarget.Directory;
    string IExplorerActionHandler.DisplayName => "文件提取";

    public FileExtractorService(LogService log, CultureService lang, IConfigService configService)
    {
        _log = log;
        _lang = lang;
        _configService = configService;
        _config = configService.Load<FileExtractorConfig>();
    }

    public bool IsEnabled
    {
        get => _config.IsEnabled;
        set
        {
            _config.IsEnabled = value;
            SaveConfig();
        }
    }

    public void Execute(ExplorerContext context)
    {
        ExtractFiles(context.PrimaryPath);
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
        if (_config.NotificationMode == 0)
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

    private void SaveConfig()
    {
        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save config: {ex.Message}");
        }
    }
}
