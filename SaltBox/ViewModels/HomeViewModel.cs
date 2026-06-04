using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaltBox.Helpers;
using SaltBox.Services;
using Windows.System;

namespace SaltBox.ViewModels;

public record ToolCard(string Name, List<string> ShortcutKeys, string NavigateTag);

public partial class HomeViewModel : ObservableObject
{
    public CultureService Lang { get; }
    public List<ToolCard> Tools { get; }
    private readonly UpdateService _updateService;

    public Action<string>? NavigateToTool { get; set; }

    public bool ShowUpdateBanner => _updateService.Status == UpdateStatus.Available;

    public string UpdateBannerText
    {
        get
        {
            if (_updateService.Status != UpdateStatus.Available || string.IsNullOrEmpty(_updateService.LatestVersion))
                return "";
            return string.Format(Lang.HomeUpdateBanner, _updateService.CurrentVersion, _updateService.LatestVersion);
        }
    }

    public HomeViewModel(CultureService lang, ShortcutRegistry shortcutRegistry, UpdateService updateService)
    {
        Lang = lang;
        _updateService = updateService;

        _updateService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UpdateService.Status) or nameof(UpdateService.LatestVersion))
            {
                OnPropertyChanged(nameof(ShowUpdateBanner));
                OnPropertyChanged(nameof(UpdateBannerText));
            }
        };

        var shortcut = shortcutRegistry.GetToolShortcut("Screenshot");
        var shortcutKeys = shortcut is not null ? ModifierHelper.GetKeyNames(shortcut.Value.Modifier, shortcut.Value.Key) : new();
        Tools = new()
        {
            new(Lang.NavScreenshot, shortcutKeys, "Screenshot"),
        };
    }

    [RelayCommand]
    private void Navigate(string tag)
    {
        NavigateToTool?.Invoke(tag);
    }
}
