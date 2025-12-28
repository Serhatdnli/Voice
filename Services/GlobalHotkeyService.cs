using System.Runtime.InteropServices;

namespace VoiceBoard.Services;

public class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    // Modifier flags
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private readonly IntPtr _hWnd;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _nextId = 1;

    public GlobalHotkeyService(IntPtr windowHandle)
    {
        _hWnd = windowHandle;
    }

    public int RegisterHotkey(Keys key, Keys modifiers, Action action)
    {
        var id = _nextId++;
        uint mod = MOD_NOREPEAT;

        if (modifiers.HasFlag(Keys.Alt)) mod |= MOD_ALT;
        if (modifiers.HasFlag(Keys.Control)) mod |= MOD_CONTROL;
        if (modifiers.HasFlag(Keys.Shift)) mod |= MOD_SHIFT;

        if (RegisterHotKey(_hWnd, id, mod, (uint)key))
        {
            _hotkeyActions[id] = action;
            return id;
        }

        return -1;
    }

    public bool UnregisterHotkey(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            UnregisterHotKey(_hWnd, id);
            _hotkeyActions.Remove(id);
            return true;
        }
        return false;
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys.ToList())
        {
            UnregisterHotKey(_hWnd, id);
        }
        _hotkeyActions.Clear();
    }

    public void ProcessHotkey(int id)
    {
        if (_hotkeyActions.TryGetValue(id, out var action))
        {
            action?.Invoke();
        }
    }

    public void Dispose()
    {
        UnregisterAll();
    }
}
