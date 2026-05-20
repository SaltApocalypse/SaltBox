using Windows.System;

namespace SaltBox.Helpers;

public static class ModifierHelper
{
    public const uint MOD_ALT = 0x1;
    public const uint MOD_CONTROL = 0x2;
    public const uint MOD_SHIFT = 0x4;
    public const uint MOD_WIN = 0x8;

    public static List<string> GetKeyNames(uint modifier, VirtualKey key)
    {
        var parts = new List<string>();
        if ((modifier & MOD_WIN) != 0) parts.Add("Win");
        if ((modifier & MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifier & MOD_ALT) != 0) parts.Add("Alt");
        if ((modifier & MOD_SHIFT) != 0) parts.Add("Shift");
        var keyName = GetKeyName(key);
        parts.Add(keyName);
        return parts;
    }

    public static string GetKeyName(VirtualKey key)
    {
        if (key >= VirtualKey.F1 && key <= VirtualKey.F12)
            return $"F{key - VirtualKey.F1 + 1}";
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
            return $"{(char)('0' + key - VirtualKey.Number0)}";
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
            return $"{(char)('A' + key - VirtualKey.A)}";
        return key switch
        {
            VirtualKey.LeftButton => "Mouse Left",
            VirtualKey.RightButton => "Mouse Right",
            VirtualKey.MiddleButton => "Mouse Middle",
            VirtualKey.XButton1 => "Mouse X1",
            VirtualKey.XButton2 => "Mouse X2",
            _ => key.ToString(),
        };
    }
}
