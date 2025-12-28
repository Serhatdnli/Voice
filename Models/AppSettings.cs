namespace VoiceBoard.Models;

public class AppSettings
{
    public List<SoundItem> Sounds { get; set; } = new();
    public string SoundsFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VoiceBoard", "Sounds");
    public string? VirtualCableDevice { get; set; }
    public bool PlayToSpeakers { get; set; } = true;
    public bool PlayToVirtualCable { get; set; } = true;
    public float MasterVolume { get; set; } = 1.0f;
}
