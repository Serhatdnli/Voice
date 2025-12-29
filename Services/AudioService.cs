using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;

namespace VoiceBoard.Services;

public enum AudioOutputMode
{
    DirectSound,   // DirectSound API - direct microphone injection
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

    // DirectSound
    private DirectSoundOut? _directSoundOutput;

    public AudioOutputMode OutputMode { get; set; } = AudioOutputMode.DirectSound; // Try DirectSound first
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

    // DirectSound device GUID for microphone injection
    public Guid? DirectSoundMicDevice { get; private set; }

    public AudioService()
    {
        InitializeDirectSound();
        InitializeAPO();
    }

    private void InitializeDirectSound()
    {
        try
        {
            // Get all DirectSound devices
            var devices = DirectSoundOut.Devices.ToList();

            System.Diagnostics.Debug.WriteLine("=== DirectSound Devices ===");
            foreach (var device in devices)
            {
                System.Diagnostics.Debug.WriteLine($"  {device.Description} - {device.Guid}");

                // Look for any device that might be a microphone or capture device
                var desc = device.Description.ToLower();
                if (desc.Contains("mic") || desc.Contains("capture") || desc.Contains("input") ||
                    desc.Contains("record") || desc.Contains("line in"))
                {
                    DirectSoundMicDevice = device.Guid;
                    System.Diagnostics.Debug.WriteLine($"  ^^^ Found potential mic device!");
                }
            }

            if (DirectSoundMicDevice.HasValue)
            {
                OutputMode = AudioOutputMode.DirectSound;
                System.Diagnostics.Debug.WriteLine($"DirectSound mic device found: {DirectSoundMicDevice}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DirectSound init error: {ex.Message}");
        }
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

        var effectiveVolume = volume * MasterVolume;

        // Always play to speakers if enabled (no boost - normal volume)
        if (PlayToSpeakers)
        {
            PlayToDevice(-1, filePath, effectiveVolume, ref _speakerOutput, ref _currentSpeakerReader, OnPlaybackFinished);
        }

        // Send to microphone stream
        if (OutputMode == AudioOutputMode.DirectSound && DirectSoundMicDevice.HasValue)
        {
            // DirectSound - try to inject to microphone device
            try
            {
                PlayWithDirectSound(filePath, effectiveVolume);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DirectSound playback error: {ex.Message}");
            }
        }
        else if (OutputMode == AudioOutputMode.APO && _apoService?.IsConnected == true)
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
            // VB-Cable with volume boost (virtual cables need higher volume)
            var cableVolume = effectiveVolume * 3.0f; // 3x boost for virtual cable
            cableVolume = Math.Min(cableVolume, 5.0f);
            PlayToDevice(VirtualCableDeviceNumber.Value, filePath, cableVolume,
                ref _virtualCableOutput, ref _currentCableReader);
        }
    }

    private void OnPlaybackFinished(object? sender, StoppedEventArgs e)
    {
        OnSoundFinished?.Invoke();
    }

    private void PlayWithDirectSound(string filePath, float volume)
    {
        if (!DirectSoundMicDevice.HasValue) return;

        try
        {
            _directSoundOutput?.Dispose();

            var reader = new AudioFileReader(filePath) { Volume = volume * 3.0f }; // Boost for mic

            // Try to play to the microphone device via DirectSound
            _directSoundOutput = new DirectSoundOut(DirectSoundMicDevice.Value);
            _directSoundOutput.Init(reader);
            _directSoundOutput.Play();

            System.Diagnostics.Debug.WriteLine($"DirectSound playing to: {DirectSoundMicDevice}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DirectSound play error: {ex.Message}");
        }
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
        _directSoundOutput?.Stop();
        _apoService?.Stop();

        _currentSpeakerReader?.Dispose();
        _currentCableReader?.Dispose();

        _currentSpeakerReader = null;
        _currentCableReader = null;
    }

    public bool IsPlaying =>
        (_speakerOutput?.PlaybackState == PlaybackState.Playing) ||
        (_virtualCableOutput?.PlaybackState == PlaybackState.Playing) ||
        (_directSoundOutput?.PlaybackState == PlaybackState.Playing) ||
        (_apoService?.IsPlaying ?? false);

    public bool IsDirectSoundAvailable => DirectSoundMicDevice.HasValue;

    public bool IsPassthroughActive => _isPassthroughActive;

    public void Dispose()
    {
        Stop();
        StopMicrophonePassthrough();
        _speakerOutput?.Dispose();
        _virtualCableOutput?.Dispose();
        _directSoundOutput?.Dispose();
        _currentSpeakerReader?.Dispose();
        _currentCableReader?.Dispose();
        _apoService?.Dispose();
    }
}
