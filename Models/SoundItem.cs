namespace VoiceBoard.Models;

public class SoundItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public Keys Hotkey { get; set; } = Keys.None;
    public Keys ModifierKeys { get; set; } = Keys.None;
    public bool IsDownloaded => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);
    public float Volume { get; set; } = 1.0f;

    public string HotkeyDisplayText
    {
        get
        {
            if (Hotkey == Keys.None) return "Atanmadı";

            var parts = new List<string>();
            if (ModifierKeys.HasFlag(Keys.Control)) parts.Add("Ctrl");
            if (ModifierKeys.HasFlag(Keys.Alt)) parts.Add("Alt");
            if (ModifierKeys.HasFlag(Keys.Shift)) parts.Add("Shift");
            parts.Add(Hotkey.ToString());

            return string.Join(" + ", parts);
        }
    }
}
