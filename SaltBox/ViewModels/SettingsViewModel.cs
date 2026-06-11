using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SaltBox.Modules.DeveloperMode;
using SaltBox.Services;
using System.Reflection;

namespace SaltBox.ViewModels;

public record LanguageOption(string Code, string DisplayName);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ThemeService _themeService;
    private readonly CultureService _culture;
    private readonly UpdateService _updateService;
    private readonly DeveloperModeService _developerModeService;

    public SettingsViewModel(ThemeService themeService, CultureService culture, UpdateService updateService, DeveloperModeService developerModeService)
    {
        _themeService = themeService;
        _culture = culture;
        _updateService = updateService;
        _developerModeService = developerModeService;
        Lang = culture;

        _updateService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UpdateService.Status) or nameof(UpdateService.LatestVersion))
                OnUpdateStatusChanged();
        };

        SelectedThemeIndex = _themeService.CurrentTheme switch
        {
            ElementTheme.Default => 0,
            ElementTheme.Dark => 1,
            ElementTheme.Light => 2,
            _ => 0
        };

        SelectedLanguage = Languages.FirstOrDefault(
            l => l.Code == _culture.CurrentCulture);
    }

    public CultureService Lang { get; }

    public string AppVersion
    {
        get
        {
            try
            {
                var v = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"v{v.Major}.{v.Minor}.{v.Build}";
            }
            catch
            {
                var v = Assembly.GetEntryAssembly()?.GetName()?.Version;
                return v is not null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v0.3.3";
            }
        }
    }

    public List<LanguageOption> Languages { get; } = new()
    {
        new LanguageOption("en-US", "English"),
        new LanguageOption("zh-CN", "中文 (简体)")
    };

    public UpdateService Updater => _updateService;

    public string UpdateStatusDescription
    {
        get
        {
            if (_updateService.Status == UpdateStatus.Idle)
                return "";
            return _updateService.Status switch
            {
                UpdateStatus.UpToDate => Lang.SettingsUpdateUpToDate,
                UpdateStatus.Available => string.Format(Lang.HomeUpdateBanner, _updateService.CurrentVersion, _updateService.LatestVersion),
                UpdateStatus.ReadyToInstall => string.Format(Lang.HomeUpdateBanner, _updateService.CurrentVersion, _updateService.LatestVersion),
                _ => ""
            };
        }
    }

    public string UpdateInfoBarMessage
    {
        get
        {
            if (_updateService.Status == UpdateStatus.Idle)
                return "";
            return _updateService.Status switch
            {
                UpdateStatus.Checking => Lang.SettingsUpdateChecking,
                UpdateStatus.UpToDate => Lang.SettingsUpdateUpToDate,
                UpdateStatus.Downloading => Lang.SettingsUpdateDownloading,
                UpdateStatus.ReadyToInstall => Lang.SettingsUpdateInstall,
                UpdateStatus.Error => _updateService.StatusMessage,
                _ => ""
            };
        }
    }

    public InfoBarSeverity UpdateInfoBarSeverity
    {
        get
        {
            if (_updateService.Status == UpdateStatus.Idle)
                return InfoBarSeverity.Informational;
            return _updateService.Status switch
            {
                UpdateStatus.UpToDate => InfoBarSeverity.Success,
                UpdateStatus.Error => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Informational
            };
        }
    }

    public bool ShowUpdateInfoBar => _updateService.Status switch
    {
        UpdateStatus.Checking => true,
        UpdateStatus.Downloading => true,
        UpdateStatus.UpToDate => true,
        UpdateStatus.Error => true,
        _ => false
    };

    public Visibility ShowResultRow => _updateService.Status is UpdateStatus.Available or UpdateStatus.ReadyToInstall ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowUpdateActionButton => _updateService.Status is UpdateStatus.Available or UpdateStatus.ReadyToInstall ? Visibility.Visible : Visibility.Collapsed;
    public bool CanCheckUpdate => _updateService.CanCheck;
    public bool IsChecking => _updateService.Status == UpdateStatus.Checking;

    public bool IsDeveloperModeEnabled
    {
        get => _developerModeService.IsEnabled;
        set
        {
            if (_developerModeService.IsEnabled != value)
            {
                _developerModeService.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    private int _selectedThemeIndex;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            0 => ElementTheme.Default,
            1 => ElementTheme.Dark,
            2 => ElementTheme.Light,
            _ => ElementTheme.Default
        };
        _themeService.SetTheme(theme);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null)
        {
            _culture.SetCulture(value.Code);
        }
    }

    private void OnUpdateStatusChanged()
    {
        OnPropertyChanged(nameof(UpdateStatusDescription));
        OnPropertyChanged(nameof(UpdateInfoBarMessage));
        OnPropertyChanged(nameof(UpdateInfoBarSeverity));
        OnPropertyChanged(nameof(ShowUpdateInfoBar));
        OnPropertyChanged(nameof(ShowResultRow));
        OnPropertyChanged(nameof(ShowUpdateActionButton));
        OnPropertyChanged(nameof(CanCheckUpdate));
        OnPropertyChanged(nameof(IsChecking));
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (!_updateService.CanCheck)
            return;
        await _updateService.CheckForUpdatesAsync();
    }

    [RelayCommand]
    private async Task DownloadAndInstall()
    {
        if (_updateService.Status == UpdateStatus.ReadyToInstall)
        {
            _updateService.ApplyAndRestart();
            return;
        }

        if (_updateService.Status != UpdateStatus.Available)
            return;

        await _updateService.DownloadUpdateAsync();
    }
}
