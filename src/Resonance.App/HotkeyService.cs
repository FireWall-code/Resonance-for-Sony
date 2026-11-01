using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

using static Resonance.Core.Localization.Loc;

namespace Resonance.App;

/// <summary>A key combination, e.g. Ctrl + Alt + N.</summary>
public sealed record Hotkey(ModifierKeys Modifiers, Key Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add(T("key.shift"));
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyName(Key));
        return string.Join(" + ", parts);
    }

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => T("key.numpad", key - Key.NumPad0),
        Key.Space => T("key.space"), Key.Return => T("key.enter"), Key.Back => T("key.back"), Key.Tab => "Tab",
        Key.Up => T("key.up"), Key.Down => T("key.down"), Key.Left => T("key.left"), Key.Right => T("key.right"),
        Key.PageUp => T("key.pageup"), Key.PageDown => T("key.pagedown"), Key.Home => T("key.home"), Key.End => T("key.end"),
        Key.Insert => T("key.insert"), Key.Pause => "Pause", Key.Scroll => T("key.scroll"),
        Key.OemPlus => "+", Key.OemMinus => "-", Key.OemComma => ",", Key.OemPeriod => ".",
        Key.Add => T("key.numpad", "+"), Key.Subtract => T("key.numpad", "-"), Key.Multiply => T("key.numpad", "*"), Key.Divide => T("key.numpad", "/"),
        Key.MediaPlayPause => T("key.media.playpause"), Key.MediaNextTrack => T("key.media.next"), Key.MediaPreviousTrack => T("key.media.prev"),
        _ => key.ToString(),
    };

    /// <summary>Function keys and media keys may be used alone; other keys need a modifier.</summary>
    public bool IsValid =>
        Key is >= Key.F1 and <= Key.F24 or Key.MediaPlayPause or Key.MediaNextTrack or Key.MediaPreviousTrack or Key.Pause
        || Modifiers != ModifierKeys.None;
}

/// <summary>
/// System-wide hotkeys through RegisterHotKey, received on a message-only window so they
/// work while Resonance is hidden in the notification area.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _window;
    private readonly Dictionary<int, string> _actions = new();
    private int _nextId = 1;

    /// <summary>Raised on the UI thread with the action name bound to the pressed hotkey.</summary>
    public event Action<string>? Pressed;

    public HotkeyService()
    {
        _window = new HwndSource(new HwndSourceParameters("Resonance.Hotkeys") { ParentWindow = new IntPtr(-3) /* HWND_MESSAGE */ });
        _window.AddHook(WndProc);
    }

    /// <summary>Registers <paramref name="hotkey"/> for <paramref name="action"/>. False when another application owns it.</summary>
    public bool Register(string action, Hotkey hotkey)
    {
        int id = _nextId++;
        // ModifierKeys values (Alt 1, Control 2, Shift 4, Windows 8) are the MOD_* flags.
        if (!RegisterHotKey(_window.Handle, id, (uint)hotkey.Modifiers | ModNoRepeat, (uint)KeyInterop.VirtualKeyFromKey(hotkey.Key)))
            return false;
        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_window.Handle, id);
        _actions.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            Pressed?.Invoke(action);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }
}
