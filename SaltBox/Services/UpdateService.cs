using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml;
using Velopack;
using Velopack.Sources;

namespace SaltBox.Services;

public enum UpdateStatus
{
    Idle,
    Checking,
    Available,
    UpToDate,
    Downloading,
    ReadyToInstall,
    Error
}

public partial class UpdateService : ObservableObject
{
    private readonly LogService _log;
    private UpdateManager? _mgr;
    private UpdateInfo? _latest;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheck))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(ShowDownload))]
    [NotifyPropertyChangedFor(nameof(ShowInstall))]
    private UpdateStatus _status = UpdateStatus.Idle;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _currentVersion = "";

    [ObservableProperty]
    private string _latestVersion = "";

    public bool CanCheck => Status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Error;
    public bool CanInstall => Status == UpdateStatus.ReadyToInstall;
    public Visibility ShowDownload => Status == UpdateStatus.Available ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowInstall => Status == UpdateStatus.ReadyToInstall ? Visibility.Visible : Visibility.Collapsed;

    public UpdateService(LogService log)
    {
        _log = log;
        InitVersion();
    }

    private void InitVersion()
    {
        try
        {
            CurrentVersion = VelopackRuntimeInfo.VelopackProductVersion?.ToString() ?? "0.2.0";
        }
        catch
        {
            CurrentVersion = "0.2.0";
        }
    }

    public void ConfigureFromConfig(IConfiguration config)
    {
        var section = config.GetSection("Update");
        var type = section["Type"];

        switch (type)
        {
            case "github":
                var repoUrl = section["GithubRepoUrl"];
                var token = section["GithubAccessToken"] ?? "";
                var pre = bool.TryParse(section["GithubPrerelease"], out var prerelease) && prerelease;

                if (!string.IsNullOrEmpty(repoUrl))
                    _mgr = new UpdateManager(new GithubSource(repoUrl, token, pre));
                break;

            default:
                var url = section["Url"];
                if (!string.IsNullOrEmpty(url))
                    _mgr = new UpdateManager(new SimpleWebSource(new Uri(url)));
                break;
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        if (_mgr is null)
        {
            StatusMessage = "Update source not configured";
            Status = UpdateStatus.Error;
            return;
        }

        Status = UpdateStatus.Checking;
        StatusMessage = "Checking for updates...";
        OnPropertyChanged(nameof(CanCheck));

        try
        {
            _latest = await _mgr.CheckForUpdatesAsync();
            if (_latest is null)
            {
                Status = UpdateStatus.UpToDate;
                StatusMessage = "You have the latest version";
            }
            else
            {
                LatestVersion = _latest.TargetFullRelease.Version.ToString();
                Status = UpdateStatus.Available;
                StatusMessage = $"Update {LatestVersion} available";
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Update check failed: {ex.Message}");
            Status = UpdateStatus.Error;
            StatusMessage = $"Check failed: {ex.Message}";
        }

        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanInstall));
    }

    public async Task DownloadUpdateAsync()
    {
        if (_mgr is null || _latest is null)
            return;

        Status = UpdateStatus.Downloading;
        StatusMessage = "Downloading update...";
        OnPropertyChanged(nameof(CanInstall));

        try
        {
            await _mgr.DownloadUpdatesAsync(_latest);
            Status = UpdateStatus.ReadyToInstall;
            StatusMessage = "Update ready — restart to install";
        }
        catch (Exception ex)
        {
            _log.Error($"Update download failed: {ex.Message}");
            Status = UpdateStatus.Error;
            StatusMessage = $"Download failed: {ex.Message}";
        }

        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanInstall));
    }

    public void ApplyAndRestart()
    {
        try
        {
            if (_latest is not null)
                _mgr?.ApplyUpdatesAndRestart(_latest);
        }
        catch (Exception ex)
        {
            _log.Error($"Apply and restart failed: {ex.Message}");
            Status = UpdateStatus.Error;
            StatusMessage = $"Restart failed: {ex.Message}";
            OnPropertyChanged(nameof(CanCheck));
            OnPropertyChanged(nameof(CanInstall));
        }
    }
}
