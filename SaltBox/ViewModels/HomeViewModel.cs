using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        var shortcutKeys = shortcut is not null ? GetKeyNames(shortcut.Value.Modifier, shortcut.Value.Key) : new();
        Tools = new()
        {
            new(Lang.NavScreenshot, shortcutKeys, "Screenshot"),
        };
    }

    private static List<string> GetKeyNames(uint modifier, VirtualKey key)
    {
        var parts = new List<string>();
        if ((modifier & 0x8) != 0) parts.Add("Win");
        if ((modifier & 0x2) != 0) parts.Add("Ctrl");
        if ((modifier & 0x1) != 0) parts.Add("Alt");
        if ((modifier & 0x4) != 0) parts.Add("Shift");
        parts.Add(GetKeyName(key));
        return parts;
    }

    private static string GetKeyName(VirtualKey key)
    {
        if (key >= VirtualKey.F1 && key <= VirtualKey.F12)
            return $"F{key - VirtualKey.F1 + 1}";
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
            return $"{(char)('0' + key - VirtualKey.Number0)}";
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
            return $"{(char)('A' + key - VirtualKey.A)}";
        return key.ToString();
    }

    [RelayCommand]
    private void Navigate(string tag)
    {
        NavigateToTool?.Invoke(tag);
    }
}
