using System.Windows.Forms;
using CopyToMd.Interop;

namespace CopyToMd.Configuration;

public sealed record HotKeyGesture(uint Modifiers, Keys Key)
{
    public static HotKeyGesture Parse(string? value)
    {
        var parts = (value ?? string.Empty).Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new FormatException("快捷键必须包含修饰键和主键，例如 Ctrl+Alt+Shift+V。");
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            modifiers |= parts[index].ToLowerInvariant() switch
            {
                "ctrl" or "control" => NativeMethods.MOD_CONTROL,
                "alt" => NativeMethods.MOD_ALT,
                "shift" => NativeMethods.MOD_SHIFT,
                "win" or "windows" => NativeMethods.MOD_WIN,
                _ => throw new FormatException($"不支持的修饰键：{parts[index]}")
            };
        }

        if (!Enum.TryParse<Keys>(parts[^1], true, out var key) || key == Keys.None)
        {
            throw new FormatException($"不支持的主键：{parts[^1]}");
        }

        return new HotKeyGesture(modifiers, key);
    }
}
