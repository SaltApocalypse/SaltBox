using SaltBox.Config;
using SaltBox.Contracts;

namespace SaltBox.Modules.Screenshot;

[ConfigFileName("screenshot")]
public class ScreenshotConfig : ConfigBase
{
    public bool IsEnabled { get; set; } = true;
    public int NotificationMode { get; set; } = 1;
    public int HdrMode { get; set; } = 0;
    public string? SavePath { get; set; }
    public string? SelectedDisplay { get; set; }
    public uint ShortcutModifier { get; set; } = 0x8;
    public uint ShortcutKey { get; set; } = 0x71;
}
