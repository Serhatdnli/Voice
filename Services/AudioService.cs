using NAudio.Wave;

namespace VoiceBoard.Services;

public class AudioService : IDisposable
{
    private WaveOutEvent? _speakerOutput;
    private WaveOutEvent? _virtualCableOutput;
    private AudioFileReader? _currentSpeakerReader;
    private AudioFileReader? _currentCableReader;

    public int? VirtualCableDeviceNumber { get; set; }
    public bool PlayToSpeakers { get; set; } = true;
    public bool PlayToVirtualCable { get; set; } = true;
    public float MasterVolume { get; set; } = 1.0f;

    public List<(int DeviceNumber, string Name)> GetOutputDevices()
    {
        var devices = new List<(int, string)>();

        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            devices.Add((i, caps.ProductName));
        }

        return devices;
    }

    public List<(int DeviceNumber, string Name)> GetVirtualCableDevices()
    {
        var devices = GetOutputDevices();
        // Virtual Audio Cable ve VB-Cable gibi sanal cihazları filtrele
        return devices.Where(d =>
            d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    public void PlaySound(string filePath, float volume = 1.0f)
    {
        Stop(); // Önceki sesi durdur

        var effectiveVolume = volume * MasterVolume;

        if (PlayToSpeakers)
        {
            PlayToDevice(-1, filePath, effectiveVolume, ref _speakerOutput, ref _currentSpeakerReader);
        }

        if (PlayToVirtualCable && VirtualCableDeviceNumber.HasValue)
        {
            PlayToDevice(VirtualCableDeviceNumber.Value, filePath, effectiveVolume,
                ref _virtualCableOutput, ref _currentCableReader);
        }
    }

    private void PlayToDevice(int deviceNumber, string filePath, float volume,
        ref WaveOutEvent? output, ref AudioFileReader? reader)
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

            output.Init(reader);
            output.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Play error: {ex.Message}");
        }
    }

    public void Stop()
    {
        _speakerOutput?.Stop();
        _virtualCableOutput?.Stop();

        _currentSpeakerReader?.Dispose();
        _currentCableReader?.Dispose();

        _currentSpeakerReader = null;
        _currentCableReader = null;
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
        _currentCableReader?.Dispose();
    }
}
