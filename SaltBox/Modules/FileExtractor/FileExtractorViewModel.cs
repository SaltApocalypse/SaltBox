using CommunityToolkit.Mvvm.ComponentModel;
using SaltBox.Services;
using Serilog;
using Windows.Storage;

namespace SaltBox.Modules.FileExtractor;

public enum FileExtractorNotificationMode
{
    None,
    Text
}

public partial class FileExtractorViewModel : ObservableObject
{
    private const string NotificationModeKey = "FileExtractorNotificationMode";
    private static readonly string NotificationModeFallbackPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaltBox", "Settings", "FileExtractorNotificationMode.txt");

    private readonly FileExtractorService _fileExtractorService;

    public CultureService Lang { get; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private FileExtractorNotificationMode _notificationMode;

    public int NotificationModeIndex
    {
        get => (int)NotificationMode;
        set => NotificationMode = (FileExtractorNotificationMode)value;
    }

    public bool HasNotificationHint => NotificationMode != FileExtractorNotificationMode.None;

    partial void OnNotificationModeChanged(FileExtractorNotificationMode value)
    {
        OnPropertyChanged(nameof(NotificationModeIndex));
        OnPropertyChanged(nameof(HasNotificationHint));
        SaveNotificationMode(value);
    }

    public FileExtractorViewModel(CultureService lang, FileExtractorService fileExtractorService)
    {
        Lang = lang;
        _fileExtractorService = fileExtractorService;

        _isEnabled = _fileExtractorService.IsEnabled;
        OnPropertyChanged(nameof(IsEnabled));

        _notificationMode = LoadNotificationMode();
        OnPropertyChanged(nameof(NotificationModeIndex));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _fileExtractorService.IsEnabled = value;
    }

    private static FileExtractorNotificationMode LoadNotificationMode()
    {
        try
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(NotificationModeKey, out var v) && v is int i)
                return (FileExtractorNotificationMode)i;
        }
        catch
        {
        }

        return LoadNotificationModeFallback();
    }

    private static FileExtractorNotificationMode LoadNotificationModeFallback()
    {
        try
        {
            if (File.Exists(NotificationModeFallbackPath))
            {
                var text = File.ReadAllText(NotificationModeFallbackPath).Trim();
                if (int.TryParse(text, out var val))
                    return (FileExtractorNotificationMode)val;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to load notification mode from fallback: {Message}", ex.Message);
        }
        return FileExtractorNotificationMode.Text;
    }

    private static void SaveNotificationMode(FileExtractorNotificationMode mode)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[NotificationModeKey] = (int)mode;
        }
        catch
        {
        }

        SaveNotificationModeFallback(mode);
    }

    private static void SaveNotificationModeFallback(FileExtractorNotificationMode mode)
    {
        try
        {
            var dir = Path.GetDirectoryName(NotificationModeFallbackPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(NotificationModeFallbackPath, ((int)mode).ToString());
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to save notification mode to fallback: {Message}", ex.Message);
        }
    }
}
