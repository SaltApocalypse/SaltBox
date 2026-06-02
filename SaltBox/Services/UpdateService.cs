using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml;
using System.Reflection;
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
        {0.2.6
            var v = Assembly.GetEntryAssembly()?.GetName()?.Version;
            CurrentVersion = v is not null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.2.4";
            _log.Debug($"InitVersion: assembly version = {v?.ToString() ?? "null"}, CurrentVersion set to {CurrentVersion}");
        }
        catch (Exception ex)0.2.6
        {0.2.6
            CurrentVersion = "0.2.4";
            _log.Debug($"InitVersion: fallback to 0.2.4 due to {ex.Message}");
        }
    }

    public void ConfigureFromConfig(IConfiguration config)
    {
        var section = config.GetSection("Update");
        var type = section["Type"];
        _log.Debug($"ConfigureFromConfig: type = {type ?? "(null)"}");

        switch (type)
        {
            case "github":
                var repoUrl = section["GithubRepoUrl"];
                var token = section["GithubAccessToken"] ?? "";
                var pre = bool.TryParse(section["GithubPrerelease"], out var prerelease) && prerelease;
                _log.Debug($"ConfigureFromConfig: github repoUrl = {repoUrl}, prerelease = {pre}, token set = {!string.IsNullOrEmpty(token)}");

                if (!string.IsNullOrEmpty(repoUrl))
                {
                    _mgr = new UpdateManager(new GithubSource(repoUrl, token, pre));
                    _log.Debug("ConfigureFromConfig: UpdateManager created with GithubSource");
                }
                else
                {
                    _log.Debug("ConfigureFromConfig: repoUrl is empty, UpdateManager NOT created");
                }
                break;

            default:
                var url = section["Url"];
                _log.Debug($"ConfigureFromConfig: default type, url = {url ?? "(null)"}");
                if (!string.IsNullOrEmpty(url))
                {
                    _mgr = new UpdateManager(new SimpleWebSource(new Uri(url)));
                    _log.Debug("ConfigureFromConfig: UpdateManager created with SimpleWebSource");
                }
                else
                {
                    _log.Debug("ConfigureFromConfig: url is empty, UpdateManager NOT created");
                }
                break;
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        _log.Debug($"CheckForUpdatesAsync: starting, _mgr = {(_mgr is null ? "null" : "not null")}, Status = {Status}, CurrentVersion = {CurrentVersion}");

        if (_mgr is null)
        {
            StatusMessage = "Update source not configured";
            Status = UpdateStatus.Error;
            _log.Debug("CheckForUpdatesAsync: _mgr is null, aborting");
            return;
        }

        Status = UpdateStatus.Checking;
        StatusMessage = "Checking for updates...";
        OnPropertyChanged(nameof(CanCheck));

        try
        {
            _latest = await _mgr.CheckForUpdatesAsync();
            _log.Debug($"CheckForUpdatesAsync: CheckForUpdatesAsync returned, _latest = {(_latest is null ? "null" : $"TargetFullRelease.Version = {_latest.TargetFullRelease.Version}")}");

            if (_latest is null)
            {
                Status = UpdateStatus.UpToDate;
                StatusMessage = "You have the latest version";
                _log.Debug("CheckForUpdatesAsync: _latest is null, assuming up-to-date");
            }
            else
            {
                LatestVersion = _latest.TargetFullRelease.Version.ToString();
                Status = UpdateStatus.Available;
                StatusMessage = $"Update {LatestVersion} available";
                _log.Debug($"CheckForUpdatesAsync: update available: Current {CurrentVersion} -> Latest {LatestVersion}");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Update check failed: {ex.Message}");
            _log.Debug($"CheckForUpdatesAsync: exception details: {ex}");
            Status = UpdateStatus.Error;
            StatusMessage = $"Check failed: {ex.Message}";
        }

        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(CanInstall));
        _log.Debug($"CheckForUpdatesAsync: finished, Status = {Status}, StatusMessage = {StatusMessage}");
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
