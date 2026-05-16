using Windows.System;

namespace SaltBox.Services;

public class ShortcutRegistry
{
    private readonly List<ShortcutEntry> _appShortcuts = new();

    public (bool HasConflict, bool IsSystem, string Name) CheckConflict(uint modifier, VirtualKey key)
    {
        if (key == VirtualKey.None)
            return (false, false, "");

        var sysConflict = SystemShortcuts.FirstOrDefault(s => s.Modifier == modifier && s.Key == key);
        if (sysConflict != null)
            return (true, true, sysConflict.Name);

        var appConflict = _appShortcuts.FirstOrDefault(s => s.Modifier == modifier && s.Key == key);
        if (appConflict != null)
            return (true, false, appConflict.Name);

        return (false, false, "");
    }

    public void Register(string toolName, uint modifier, VirtualKey key)
    {
        _appShortcuts.RemoveAll(s => s.Name == toolName);
        _appShortcuts.Add(new ShortcutEntry(toolName, modifier, key));
    }

    public void Unregister(string toolName)
    {
        _appShortcuts.RemoveAll(s => s.Name == toolName);
    }

    public (uint Modifier, VirtualKey Key)? GetToolShortcut(string toolName)
    {
        var entry = _appShortcuts.FirstOrDefault(s => s.Name == toolName);
        if (entry is not null)
            return (entry.Modifier, entry.Key);

        return toolName switch
        {
            "Screenshot" => (MOD_WIN, VirtualKey.F2),
            _ => null
        };
    }

    public string? GetConflictDescription(uint modifier, VirtualKey key)
    {
        var (hasConflict, _, name) = CheckConflict(modifier, key);
        return hasConflict ? name : null;
    }

    private static readonly List<ShortcutEntry> SystemShortcuts = new()
    {
        new("Win + D", MOD_WIN, VirtualKey.D),
        new("Win + E", MOD_WIN, VirtualKey.E),
        new("Win + F", MOD_WIN, VirtualKey.F),
        new("Win + G", MOD_WIN, VirtualKey.G),
        new("Win + H", MOD_WIN, VirtualKey.H),
        new("Win + I", MOD_WIN, VirtualKey.I),
        new("Win + K", MOD_WIN, VirtualKey.K),
        new("Win + L", MOD_WIN, VirtualKey.L),
        new("Win + M", MOD_WIN, VirtualKey.M),
        new("Win + P", MOD_WIN, VirtualKey.P),
        new("Win + R", MOD_WIN, VirtualKey.R),
        new("Win + S", MOD_WIN, VirtualKey.S),
        new("Win + T", MOD_WIN, VirtualKey.T),
        new("Win + U", MOD_WIN, VirtualKey.U),
        new("Win + V", MOD_WIN, VirtualKey.V),
        new("Win + W", MOD_WIN, VirtualKey.W),
        new("Win + X", MOD_WIN, VirtualKey.X),
        new("Win + Z", MOD_WIN, VirtualKey.Z),
        new("Win + Tab", MOD_WIN, VirtualKey.Tab),
        new("Win + Space", MOD_WIN, VirtualKey.Space),
        new("Win + 1", MOD_WIN, VirtualKey.Number1),
        new("Win + 2", MOD_WIN, VirtualKey.Number2),
        new("Win + 3", MOD_WIN, VirtualKey.Number3),
        new("Win + 4", MOD_WIN, VirtualKey.Number4),
        new("Win + 5", MOD_WIN, VirtualKey.Number5),
        new("Win + 6", MOD_WIN, VirtualKey.Number6),
        new("Win + 7", MOD_WIN, VirtualKey.Number7),
        new("Win + 8", MOD_WIN, VirtualKey.Number8),
        new("Win + 9", MOD_WIN, VirtualKey.Number9),
        new("Win + 0", MOD_WIN, VirtualKey.Number0),
        new("Win + ↑", MOD_WIN, VirtualKey.Up),
        new("Win + ↓", MOD_WIN, VirtualKey.Down),
        new("Win + ←", MOD_WIN, VirtualKey.Left),
        new("Win + →", MOD_WIN, VirtualKey.Right),
        new("Win + Ctrl + D", MOD_WIN | MOD_CONTROL, VirtualKey.D),
        new("Win + Ctrl + F4", MOD_WIN | MOD_CONTROL, VirtualKey.F4),
        new("Win + Ctrl + ←", MOD_WIN | MOD_CONTROL, VirtualKey.Left),
        new("Win + Ctrl + →", MOD_WIN | MOD_CONTROL, VirtualKey.Right),
        new("Win + Shift + S", MOD_WIN | MOD_SHIFT, VirtualKey.S),
        new("Win + Shift + ←", MOD_WIN | MOD_SHIFT, VirtualKey.Left),
        new("Win + Shift + →", MOD_WIN | MOD_SHIFT, VirtualKey.Right),
        new("Win + Shift + ↑", MOD_WIN | MOD_SHIFT, VirtualKey.Up),
        new("Win + Shift + ↓", MOD_WIN | MOD_SHIFT, VirtualKey.Down),
        new("Ctrl + A", MOD_CONTROL, VirtualKey.A),
        new("Ctrl + C", MOD_CONTROL, VirtualKey.C),
        new("Ctrl + V", MOD_CONTROL, VirtualKey.V),
        new("Ctrl + X", MOD_CONTROL, VirtualKey.X),
        new("Ctrl + Z", MOD_CONTROL, VirtualKey.Z),
        new("Ctrl + Y", MOD_CONTROL, VirtualKey.Y),
        new("Ctrl + S", MOD_CONTROL, VirtualKey.S),
        new("Ctrl + F", MOD_CONTROL, VirtualKey.F),
        new("Ctrl + Shift + Esc", MOD_CONTROL | MOD_SHIFT, VirtualKey.Escape),
        new("Ctrl + Alt + Del", MOD_CONTROL | MOD_ALT, VirtualKey.Delete),
        new("Alt + F4", MOD_ALT, VirtualKey.F4),
        new("Alt + Tab", MOD_ALT, VirtualKey.Tab),
        new("Alt + Space", MOD_ALT, VirtualKey.Space),
        new("Alt + Enter", MOD_ALT, VirtualKey.Enter),
        new("F1", 0, VirtualKey.F1),
        new("F5", 0, VirtualKey.F5),
    };

    private record ShortcutEntry(string Name, uint Modifier, VirtualKey Key);

    private const uint MOD_ALT = 0x1;
    private const uint MOD_CONTROL = 0x2;
    private const uint MOD_SHIFT = 0x4;
    private const uint MOD_WIN = 0x8;
}
