using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoiceBoard.Services;

public class AudioService : IDisposable
{
    private WaveOutEvent? _speakerOutput;
    private DirectSoundOut? _directSoundOutput;
    private AudioFileReader? _currentSpeakerReader;
    private AudioFileReader? _currentDirectSoundReader;

    public float MasterVolume { get; set; } = 1.0f;
    public bool PlayToSpeakers { get; set; } = true;

    // DirectSound device GUID for microphone injection
    public Guid? DirectSoundMicDevice { get; private set; }
    public bool IsDirectSoundAvailable => DirectSoundMicDevice.HasValue;

    public event Action? OnSoundStarted;
    public event Action? OnSoundFinished;

    public AudioService()
    {
        InitializeDirectSound();
    }

    private void InitializeDirectSound()
    {
        try
        {
            // List OUTPUT devices (speakers, headphones, virtual cables)
            System.Diagnostics.Debug.WriteLine("=== OUTPUT Devices (WaveOut) ===");
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                System.Diagnostics.Debug.WriteLine($"  [{i}] {caps.ProductName}");
            }

            // List INPUT devices (microphones, line-in, stereo mix)
            System.Diagnostics.Debug.WriteLine("=== INPUT Devices (WaveIn) ===");
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                System.Diagnostics.Debug.WriteLine($"  [{i}] {caps.ProductName}");

                // Check for Stereo Mix or virtual cable input
                var name = caps.ProductName.ToLower();
                if (name.Contains("stereo mix") || name.Contains("cable") ||
                    name.Contains("virtual") || name.Contains("voicemeeter"))
                {
                    System.Diagnostics.Debug.WriteLine($"  ^^^ Potential loopback device!");
                }
            }

            // List DirectSound OUTPUT devices
            System.Diagnostics.Debug.WriteLine("=== DirectSound OUTPUT Devices ===");
            var dsDevices = DirectSoundOut.Devices.ToList();
            foreach (var device in dsDevices)
            {
                System.Diagnostics.Debug.WriteLine($"  {device.Description} - {device.Guid}");

                // Look for virtual audio cable output
                var desc = device.Description.ToLower();
                if (desc.Contains("cable") || desc.Contains("virtual") ||
                    desc.Contains("voicemeeter") || desc.Contains("vb-audio"))
                {
                    DirectSoundMicDevice = device.Guid;
                    System.Diagnostics.Debug.WriteLine($"  ^^^ Virtual cable found - can use for mic injection!");
                }
            }

            System.Diagnostics.Debug.WriteLine("================================");
            System.Diagnostics.Debug.WriteLine("NOTE: Microphones are INPUT devices.");
            System.Diagnostics.Debug.WriteLine("Cannot 'play' audio to input devices.");
            System.Diagnostics.Debug.WriteLine("Need virtual audio cable or Stereo Mix.");
            System.Diagnostics.Debug.WriteLine("================================");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Audio init error: {ex.Message}");
        }
    }

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

    public List<(Guid Guid, string Name)> GetDirectSoundDevices()
    {
        return DirectSoundOut.Devices
            .Select(d => (d.Guid, d.Description))
            .ToList();
    }

    public void PlaySound(string filePath, float volume = 1.0f)
    {
        Stop();

        OnSoundStarted?.Invoke();

        var effectiveVolume = volume * MasterVolume;

        // Play to speakers
        if (PlayToSpeakers)
        {
            PlayToSpeaker(filePath, effectiveVolume);
        }

        // Try DirectSound injection to microphone
        if (DirectSoundMicDevice.HasValue)
        {
            PlayWithDirectSound(filePath, effectiveVolume);
        }
    }

    private void PlayToSpeaker(string filePath, float volume)
    {
        try
        {
            _speakerOutput?.Dispose();
            _currentSpeakerReader?.Dispose();

            _currentSpeakerReader = new AudioFileReader(filePath)
            {
                Volume = volume
            };

            _speakerOutput = new WaveOutEvent
            {
                DeviceNumber = -1 // Default device
            };

            _speakerOutput.PlaybackStopped += OnPlaybackFinished;
            _speakerOutput.Init(_currentSpeakerReader);
            _speakerOutput.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Speaker play error: {ex.Message}");
        }
    }

    private void PlayWithDirectSound(string filePath, float volume)
    {
        if (!DirectSoundMicDevice.HasValue) return;

        try
        {
            _directSoundOutput?.Dispose();
            _currentDirectSoundReader?.Dispose();

            // Boost volume for microphone injection
            _currentDirectSoundReader = new AudioFileReader(filePath)
            {
                Volume = volume * 3.0f
            };

            // Try to play to the microphone device via DirectSound
            _directSoundOutput = new DirectSoundOut(DirectSoundMicDevice.Value);
            _directSoundOutput.Init(_currentDirectSoundReader);
            _directSoundOutput.Play();

            System.Diagnostics.Debug.WriteLine($"DirectSound playing to: {DirectSoundMicDevice}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DirectSound play error: {ex.Message}");
        }
    }

    private void OnPlaybackFinished(object? sender, StoppedEventArgs e)
    {
        OnSoundFinished?.Invoke();
    }

    public void Stop()
    {
        _speakerOutput?.Stop();
        _directSoundOutput?.Stop();

        _currentSpeakerReader?.Dispose();
        _currentDirectSoundReader?.Dispose();

        _currentSpeakerReader = null;
        _currentDirectSoundReader = null;
    }

    public bool IsPlaying =>
        (_speakerOutput?.PlaybackState == PlaybackState.Playing) ||
        (_directSoundOutput?.PlaybackState == PlaybackState.Playing);

    public void Dispose()
    {
        Stop();
        _speakerOutput?.Dispose();
        _directSoundOutput?.Dispose();
        _currentSpeakerReader?.Dispose();
        _currentDirectSoundReader?.Dispose();
    }
}
