using CommunityToolkit.Mvvm.ComponentModel;
using SaltBox.Contracts;
using SaltBox.Services;
using SaltBox.Services.ExplorerIntegration;

namespace SaltBox.Modules.FileExtractor;

public enum FileExtractorNotificationMode
{
    None,
    Text
}

public partial class FileExtractorViewModel : ObservableObject
{
    private readonly FileExtractorService _fileExtractorService;
    private readonly ExplorerActionManager _actionManager;
    private readonly IConfigService _configService;
    private FileExtractorConfig _config;

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
        _config.NotificationMode = (int)value;
        SaveConfig();
    }

    public FileExtractorViewModel(CultureService lang, FileExtractorService fileExtractorService, ExplorerActionManager actionManager, IConfigService configService)
    {
        Lang = lang;
        _fileExtractorService = fileExtractorService;
        _actionManager = actionManager;
        _configService = configService;
        _config = configService.Load<FileExtractorConfig>();

        _isEnabled = _fileExtractorService.IsEnabled;
        OnPropertyChanged(nameof(IsEnabled));

        _notificationMode = (FileExtractorNotificationMode)_config.NotificationMode;
        OnPropertyChanged(nameof(NotificationModeIndex));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _fileExtractorService.IsEnabled = value;
        _actionManager.RefreshHandler(_fileExtractorService);
    }

    private void SaveConfig()
    {
        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("Failed to save FileExtractor config: {Message}", ex.Message);
        }
    }
}
