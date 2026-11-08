namespace KeepLid.Core.Settings;

/// <summary>
/// A global shortcut as RegisterHotKey expects it.
/// Constants from the Windows SDK 10.0.26100, um/WinUser.h:
/// MOD_ALT 0x0001, MOD_CONTROL 0x0002, MOD_SHIFT 0x0004, MOD_WIN 0x0008;
/// virtual keys: 'A'–'Z' = 0x41–0x5A, '0'–'9' = 0x30–0x39, VK_F1 0x70 … VK_F24 0x87.
/// </summary>
public readonly record struct Hotkey(uint Modifiers, uint VirtualKey)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private const uint VkF1 = 0x70;
    private const uint VkF24 = 0x87;

    /// <summary>Parses "Ctrl+Alt+Shift+A". At least one of Ctrl/Alt/Win is required to avoid hijacking plain typing.</summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        uint modifiers = 0;
        uint? key = null;
        foreach (string raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": modifiers |= ModControl; break;
                case "ALT": modifiers |= ModAlt; break;
                case "SHIFT" or "MAJ": modifiers |= ModShift; break;
                case "WIN": modifiers |= ModWin; break;
                default:
                    if (key is not null) return false; // only one non-modifier key
                    key = ParseKey(raw);
                    if (key is null) return false;
                    break;
            }
        }

        if (key is null || (modifiers & (ModControl | ModAlt | ModWin)) == 0) return false;
        hotkey = new Hotkey(modifiers, key.Value);
        return true;
    }

    /// <summary>Builds the text form from WPF-independent pieces (used by the settings window).</summary>
    public static string Format(bool ctrl, bool alt, bool shift, bool win, string key)
    {
        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        if (win) parts.Add("Win");
        parts.Add(key.ToUpperInvariant());
        return string.Join("+", parts);
    }

    private static uint? ParseKey(string key)
    {
        string k = key.ToUpperInvariant();
        if (k.Length == 1 && k[0] is >= 'A' and <= 'Z') return k[0];   // 0x41..0x5A
        if (k.Length == 1 && k[0] is >= '0' and <= '9') return k[0];   // 0x30..0x39
        if (k.Length is 2 or 3 && k[0] == 'F' && uint.TryParse(k[1..], out uint n) && n is >= 1 and <= 24)
        {
            uint vk = VkF1 + n - 1;
            return vk <= VkF24 ? vk : null;
        }
        return null;
    }
}
