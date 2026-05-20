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

    public Action<string>? NavigateToTool { get; set; }

    public HomeViewModel(CultureService lang, ShortcutRegistry shortcutRegistry)
    {
        Lang = lang;

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
