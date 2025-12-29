using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoiceBoard.Services;

public class AudioService : IDisposable
{
    private WaveOutEvent? _speakerOutput;
    private WaveOutEvent? _virtualCableOutput;
    private AudioFileReader? _currentSpeakerReader;
    private AudioFileReader? _currentVirtualReader;

    public float MasterVolume { get; set; } = 1.0f;
    public bool PlayToSpeakers { get; set; } = true;
    public bool PlayToVirtualCable { get; set; } = true;

    // Seçili cihaz numaraları (-1 = default)
    public int SpeakerDeviceNumber { get; set; } = -1;
    public int VirtualCableDeviceNumber { get; set; } = -1;

    // Bulunan virtual cable cihazı
    public string? VirtualCableDeviceName { get; private set; }
    public bool HasVirtualCable => VirtualCableDeviceNumber >= 0;

    public event Action? OnSoundStarted;
    public event Action? OnSoundFinished;

    public AudioService()
    {
        DetectDevices();
    }

    private void DetectDevices()
    {
        System.Diagnostics.Debug.WriteLine("=== Audio Output Devices ===");

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            System.Diagnostics.Debug.WriteLine($"  [{i}] {caps.ProductName}");

            var name = caps.ProductName.ToLower();
            // VB-Cable, VoiceMeeter, Virtual Cable vb. ara
            if (name.Contains("cable") || name.Contains("virtual") ||
                name.Contains("voicemeeter") || name.Contains("vb-audio") ||
                name.Contains("voice meter"))
            {
                VirtualCableDeviceNumber = i;
                VirtualCableDeviceName = caps.ProductName;
                System.Diagnostics.Debug.WriteLine($"  ^^^ Virtual Cable BULUNDU!");
            }
        }

        System.Diagnostics.Debug.WriteLine($"\nVirtual Cable: {(HasVirtualCable ? VirtualCableDeviceName : "Bulunamadi")}");
        System.Diagnostics.Debug.WriteLine("================================\n");
    }

    public List<(int DeviceNumber, string Name)> GetOutputDevices()
    {
        var devices = new List<(int, string)>();
        devices.Add((-1, "Varsayilan Cihaz"));

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add((i, caps.ProductName));
        }
        return devices;
    }

    public List<(int DeviceNumber, string Name)> GetInputDevices()
    {
        var devices = new List<(int, string)>();
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add((i, caps.ProductName));
        }
        return devices;
    }

    public void PlaySound(string filePath, float volume = 1.0f)
    {
        Stop();
        OnSoundStarted?.Invoke();

        var effectiveVolume = volume * MasterVolume;

        // Hoparlöre çal
        if (PlayToSpeakers)
        {
            PlayToDevice(filePath, effectiveVolume, SpeakerDeviceNumber, ref _speakerOutput, ref _currentSpeakerReader, true);
        }

        // Virtual Cable'a çal (mikrofon olarak kullanılacak)
        if (PlayToVirtualCable && HasVirtualCable)
        {
            PlayToDevice(filePath, effectiveVolume, VirtualCableDeviceNumber, ref _virtualCableOutput, ref _currentVirtualReader, false);
        }
    }

    private void PlayToDevice(string filePath, float volume, int deviceNumber,
        ref WaveOutEvent? output, ref AudioFileReader? reader, bool trackFinish)
    {
        try
        {
            output?.Dispose();
            reader?.Dispose();

            reader = new AudioFileReader(filePath)
            {
                Volume = volume
            };

            output = new WaveOutEvent
            {
                DeviceNumber = deviceNumber
            };

            if (trackFinish)
            {
                output.PlaybackStopped += OnPlaybackFinished;
            }

            output.Init(reader);
            output.Play();

            System.Diagnostics.Debug.WriteLine($"Playing to device {deviceNumber}: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Play error (device {deviceNumber}): {ex.Message}");
        }
    }

    private void OnPlaybackFinished(object? sender, StoppedEventArgs e)
    {
        OnSoundFinished?.Invoke();
    }

    public void Stop()
    {
        _speakerOutput?.Stop();
        _virtualCableOutput?.Stop();

        _currentSpeakerReader?.Dispose();
        _currentVirtualReader?.Dispose();

        _currentSpeakerReader = null;
        _currentVirtualReader = null;
    }

    public bool IsPlaying =>
        (_speakerOutput?.PlaybackState == PlaybackState.Playing) ||
        (_virtualCableOutput?.PlaybackState == PlaybackState.Playing);

    public void Dispose()
    {
        Stop();
        _speakerOutput?.Dispose();
        _virtualCableOutput?.Dispose();
        _currentSpeakerReader?.Dispose();
        _currentVirtualReader?.Dispose();
    }
}
