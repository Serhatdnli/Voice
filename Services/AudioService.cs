using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoiceBoard.Services;

public enum AudioOutputMode
{
    APO,           // Direct injection via APO (no VB-Cable needed)
    VirtualCable,  // VB-Cable passthrough mode
    SpeakersOnly   // Just play to speakers
}

public class AudioService : IDisposable
{
    private WaveOutEvent? _speakerOutput;
    private WaveOutEvent? _virtualCableOutput;
    private AudioFileReader? _currentSpeakerReader;
    private AudioFileReader? _currentCableReader;

    // Mikrofon pass-through icin (VB-Cable mode)
    private WaveInEvent? _microphoneInput;
    private WaveOutEvent? _microphonePassthrough;
    private BufferedWaveProvider? _microphoneBuffer;
    private bool _isPassthroughActive;

    // APO Service
    private APOService? _apoService;

    public AudioOutputMode OutputMode { get; set; } = AudioOutputMode.APO;
    public int? VirtualCableDeviceNumber { get; set; }
    public int? MicrophoneDeviceNumber { get; set; }
    public bool PlayToSpeakers { get; set; } = true;
    public bool PlayToVirtualCable { get; set; } = true;
    public bool MicrophonePassthroughEnabled { get; set; } = true;
    public float MasterVolume { get; set; } = 1.0f;
    public float MicrophoneVolume { get; set; } = 1.0f;

    public bool IsAPOInstalled => _apoService?.IsAPOInstalled ?? false;
    public bool IsAPOConnected => _apoService?.IsConnected ?? false;

    public event Action? OnSoundStarted;
    public event Action? OnSoundFinished;

    public AudioService()
    {
        InitializeAPO();
    }

    private void InitializeAPO()
    {
        try
        {
            _apoService = new APOService();
            if (_apoService.IsAPOInstalled)
            {
                OutputMode = AudioOutputMode.APO;
            }
            else
            {
                OutputMode = AudioOutputMode.VirtualCable;
            }
        }
        catch
        {
            OutputMode = AudioOutputMode.VirtualCable;
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

    public List<(int DeviceNumber, string Name)> GetVirtualCableDevices()
    {
        var devices = GetOutputDevices();
        return devices.Where(d =>
            d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
            d.Name.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    /// <summary>
    /// Play sound - automatically uses APO if installed, otherwise VB-Cable
    /// </summary>
    public async void PlaySound(string filePath, float volume = 1.0f)
    {
        Stop();

        OnSoundStarted?.Invoke();

        // Apply volume with boost (web audio is often quiet)
        var effectiveVolume = volume * MasterVolume * 2.5f;
        effectiveVolume = Math.Min(effectiveVolume, 3.0f); // Cap at 3x to prevent distortion

        // Always play to speakers if enabled
        if (PlayToSpeakers)
        {
            PlayToDevice(-1, filePath, effectiveVolume, ref _speakerOutput, ref _currentSpeakerReader, OnPlaybackFinished);
        }

        // Send to microphone stream
        if (OutputMode == AudioOutputMode.APO && _apoService?.IsConnected == true)
        {
            // Use APO - direct injection, no VB-Cable needed!
            try
            {
                await _apoService.PlayThroughAPOAsync(filePath, effectiveVolume);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"APO playback error: {ex.Message}");
            }
        }
        else if (OutputMode == AudioOutputMode.VirtualCable && PlayToVirtualCable && VirtualCableDeviceNumber.HasValue)
        {
            // Fallback to VB-Cable
            PlayToDevice(VirtualCableDeviceNumber.Value, filePath, effectiveVolume,
                ref _virtualCableOutput, ref _currentCableReader);
        }
    }

    private void OnPlaybackFinished(object? sender, StoppedEventArgs e)
    {
        OnSoundFinished?.Invoke();
    }

    private void PlayToDevice(int deviceNumber, string filePath, float volume,
        ref WaveOutEvent? output, ref AudioFileReader? reader, EventHandler<StoppedEventArgs>? onStopped = null)
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

            if (onStopped != null)
            {
                output.PlaybackStopped += onStopped;
            }

            output.Init(reader);
            output.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Play error: {ex.Message}");
        }
    }

    #region VB-Cable Passthrough (Fallback Mode)

    public void StartMicrophonePassthrough()
    {
        if (OutputMode == AudioOutputMode.APO)
            return; // APO mode doesn't need passthrough

        if (!MicrophonePassthroughEnabled || !VirtualCableDeviceNumber.HasValue || !MicrophoneDeviceNumber.HasValue)
            return;

        if (_isPassthroughActive)
            return;

        try
        {
            StopMicrophonePassthrough();

            _microphoneInput = new WaveInEvent
            {
                DeviceNumber = MicrophoneDeviceNumber.Value,
                WaveFormat = new WaveFormat(44100, 16, 1)
            };

            _microphoneBuffer = new BufferedWaveProvider(_microphoneInput.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };

            _microphoneInput.DataAvailable += (s, e) =>
            {
                if (!IsPlaying)
                {
                    var buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);
                    AdjustVolume(buffer, MicrophoneVolume);
                    _microphoneBuffer?.AddSamples(buffer, 0, e.BytesRecorded);
                }
            };

            _microphonePassthrough = new WaveOutEvent
            {
                DeviceNumber = VirtualCableDeviceNumber.Value
            };

            _microphonePassthrough.Init(_microphoneBuffer);
            _microphoneInput.StartRecording();
            _microphonePassthrough.Play();

            _isPassthroughActive = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Passthrough error: {ex.Message}");
        }
    }

    public void StopMicrophonePassthrough()
    {
        _isPassthroughActive = false;

        _microphoneInput?.StopRecording();
        _microphoneInput?.Dispose();
        _microphoneInput = null;

        _microphonePassthrough?.Stop();
        _microphonePassthrough?.Dispose();
        _microphonePassthrough = null;

        _microphoneBuffer = null;
    }

    public void RestartMicrophonePassthrough()
    {
        if (OutputMode == AudioOutputMode.VirtualCable &&
            MicrophonePassthroughEnabled &&
            VirtualCableDeviceNumber.HasValue &&
            MicrophoneDeviceNumber.HasValue)
        {
            StopMicrophonePassthrough();
            StartMicrophonePassthrough();
        }
    }

    private void AdjustVolume(byte[] buffer, float volume)
    {
        if (Math.Abs(volume - 1.0f) < 0.01f) return;

        for (int i = 0; i < buffer.Length; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            sample = (short)(sample * volume);
            buffer[i] = (byte)(sample & 0xFF);
            buffer[i + 1] = (byte)((sample >> 8) & 0xFF);
        }
    }

    #endregion

    public void Stop()
    {
        _speakerOutput?.Stop();
        _virtualCableOutput?.Stop();
        _apoService?.Stop();

        _currentSpeakerReader?.Dispose();
        _currentCableReader?.Dispose();

        _currentSpeakerReader = null;
        _currentCableReader = null;
    }

    public bool IsPlaying =>
        (_speakerOutput?.PlaybackState == PlaybackState.Playing) ||
        (_virtualCableOutput?.PlaybackState == PlaybackState.Playing) ||
        (_apoService?.IsPlaying ?? false);

    public bool IsPassthroughActive => _isPassthroughActive;

    public void Dispose()
    {
        Stop();
        StopMicrophonePassthrough();
        _speakerOutput?.Dispose();
        _virtualCableOutput?.Dispose();
        _currentSpeakerReader?.Dispose();
        _currentCableReader?.Dispose();
        _apoService?.Dispose();
    }
}
