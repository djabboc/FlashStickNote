using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FlashStickNote.Services;

public sealed class HotkeyManager : IDisposable
{
    public const int WmHotkey = 0x0312;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private static readonly Dictionary<string, uint> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = ModControl,
        ["Control"] = ModControl,
        ["Alt"] = ModAlt,
        ["Shift"] = ModShift,
        ["Win"] = ModWin,
        ["Windows"] = ModWin,
        ["Super"] = ModWin,
    };

    private static readonly Dictionary<string, Keys> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = Keys.Space,
        ["Enter"] = Keys.Return,
        ["Return"] = Keys.Return,
        ["Esc"] = Keys.Escape,
        ["Escape"] = Keys.Escape,
        ["Tab"] = Keys.Tab,
        ["Backspace"] = Keys.Back,
        ["Insert"] = Keys.Insert,
        ["Delete"] = Keys.Delete,
        ["Del"] = Keys.Delete,
        ["Home"] = Keys.Home,
        ["End"] = Keys.End,
        ["PageUp"] = Keys.PageUp,
        ["PageDown"] = Keys.PageDown,
        ["Up"] = Keys.Up,
        ["Down"] = Keys.Down,
        ["Left"] = Keys.Left,
        ["Right"] = Keys.Right,
        ["PrintScreen"] = Keys.PrintScreen,
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hWnd;
    private readonly int _id;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager(IntPtr hWnd, int id)
    {
        _hWnd = hWnd;
        _id = id;
    }

    public bool Register(string combo)
    {
        Unregister();
        if (!TryParse(combo, out var modifiers, out var virtualKey))
        {
            return false;
        }

        _registered = RegisterHotKey(_hWnd, _id, modifiers, virtualKey);
        return _registered;
    }

    public static bool TryParse(string combo, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(combo))
        {
            return false;
        }

        var parts = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var key = Keys.None;
        var hasKey = false;

        foreach (var part in parts)
        {
            if (Modifiers.TryGetValue(part, out var mod))
            {
                modifiers |= mod;
                continue;
            }

            if (TryGetKey(part, out var k))
            {
                key = k;
                hasKey = true;
            }
            else
            {
                return false;
            }
        }

        if (!hasKey)
        {
            return false;
        }

        virtualKey = (uint)(key & Keys.KeyCode);
        return virtualKey != 0;
    }

    private static bool TryGetKey(string text, out Keys key)
    {
        if (KeyAliases.TryGetValue(text, out key))
        {
            return true;
        }

        if (text.Length == 1 && char.IsDigit(text[0]))
        {
            key = Keys.D0 + (text[0] - '0');
            return true;
        }

        if (Enum.TryParse(text, true, out key))
        {
            key &= Keys.KeyCode;
            return key != Keys.None;
        }

        return false;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_hWnd, _id);
            _registered = false;
        }
    }

    public void OnHotkeyMessage() => Pressed?.Invoke();

    public void Dispose() => Unregister();
}
