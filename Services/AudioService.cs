using NAudio.Wave;
using SharpDX.DirectSound;
using SharpDX.Multimedia;
using System.Runtime.InteropServices;

namespace VoiceBoard.Services;

public class AudioService : IDisposable
{
    private WaveOutEvent? _speakerOutput;
    private AudioFileReader? _currentSpeakerReader;

    // DirectSound capture için
    private DirectSoundCapture? _capture;
    private CaptureBuffer? _captureBuffer;
    private SecondarySoundBuffer? _secondaryBuffer;
    private DirectSound? _directSound;

    public float MasterVolume { get; set; } = 1.0f;
    public bool PlayToSpeakers { get; set; } = true;

    public List<string> AvailableCaptureDevices { get; private set; } = new();
    public List<string> AvailableRenderDevices { get; private set; } = new();
    public bool HasLoopbackDevice { get; private set; }
    public string? SelectedLoopbackDevice { get; private set; }

    public event Action? OnSoundStarted;
    public event Action? OnSoundFinished;

    public AudioService()
    {
        EnumerateDevices();
    }

    private void EnumerateDevices()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== DirectSound Device Enumeration ===");

            // Enumerate CAPTURE devices (microphones, stereo mix, etc.)
            System.Diagnostics.Debug.WriteLine("\n--- CAPTURE Devices (DirectSoundCapture) ---");
            var captureDevices = DirectSoundCapture.GetDevices();
            foreach (var device in captureDevices)
            {
                System.Diagnostics.Debug.WriteLine($"  [{device.DriverGuid}] {device.Description}");
                AvailableCaptureDevices.Add(device.Description);

                var desc = device.Description.ToLower();
                if (desc.Contains("stereo mix") || desc.Contains("what u hear") ||
                    desc.Contains("wave out") || desc.Contains("loopback"))
                {
                    HasLoopbackDevice = true;
                    SelectedLoopbackDevice = device.Description;
                    System.Diagnostics.Debug.WriteLine($"  ^^^ LOOPBACK device found!");
                }
            }

            // Enumerate RENDER devices (speakers, headphones, virtual cables)
            System.Diagnostics.Debug.WriteLine("\n--- RENDER Devices (DirectSound) ---");
            var renderDevices = DirectSound.GetDevices();
            foreach (var device in renderDevices)
            {
                System.Diagnostics.Debug.WriteLine($"  [{device.DriverGuid}] {device.Description}");
                AvailableRenderDevices.Add(device.Description);

                var desc = device.Description.ToLower();
                if (desc.Contains("cable") || desc.Contains("virtual") ||
                    desc.Contains("voicemeeter") || desc.Contains("vb-audio"))
                {
                    System.Diagnostics.Debug.WriteLine($"  ^^^ Virtual cable output found!");
                }
            }

            // Also check NAudio WaveIn devices
            System.Diagnostics.Debug.WriteLine("\n--- NAudio WaveIn Devices ---");
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                System.Diagnostics.Debug.WriteLine($"  [{i}] {caps.ProductName}");

                var name = caps.ProductName.ToLower();
                if (name.Contains("stereo mix") || name.Contains("what u hear") ||
                    name.Contains("loopback"))
                {
                    HasLoopbackDevice = true;
                    if (SelectedLoopbackDevice == null)
                        SelectedLoopbackDevice = caps.ProductName;
                }
            }

            System.Diagnostics.Debug.WriteLine("\n=== Summary ===");
            System.Diagnostics.Debug.WriteLine($"Loopback available: {HasLoopbackDevice}");
            System.Diagnostics.Debug.WriteLine($"Loopback device: {SelectedLoopbackDevice ?? "None"}");
            System.Diagnostics.Debug.WriteLine("================================\n");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Enumeration error: {ex.Message}");
        }
    }

    public void InitializeDirectSoundCapture(IntPtr windowHandle)
    {
        try
        {
            // Initialize DirectSound for playback
            _directSound = new DirectSound();
            _directSound.SetCooperativeLevel(windowHandle, CooperativeLevel.Priority);

            // Initialize DirectSoundCapture for capture devices
            _capture = new DirectSoundCapture();

            System.Diagnostics.Debug.WriteLine("DirectSound initialized successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DirectSound init error: {ex.Message}");
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

    public void PlaySound(string filePath, float volume = 1.0f)
    {
        Stop();
        OnSoundStarted?.Invoke();

        var effectiveVolume = volume * MasterVolume;

        if (PlayToSpeakers)
        {
            PlayToSpeaker(filePath, effectiveVolume);
        }

        // DirectSound playback için de dene
        PlayWithDirectSound(filePath, effectiveVolume);
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
                DeviceNumber = -1
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
        if (_directSound == null) return;

        try
        {
            // Ses dosyasını oku
            using var reader = new AudioFileReader(filePath);

            // WaveFormat oluştur
            var waveFormat = new WaveFormat(
                reader.WaveFormat.SampleRate,
                reader.WaveFormat.BitsPerSample == 0 ? 16 : reader.WaveFormat.BitsPerSample,
                reader.WaveFormat.Channels);

            // Buffer boyutunu hesapla
            var bufferSize = (int)(reader.Length * 2);

            // Secondary buffer oluştur
            var bufferDesc = new SoundBufferDescription
            {
                Format = waveFormat,
                Flags = BufferFlags.ControlVolume | BufferFlags.GlobalFocus,
                BufferBytes = bufferSize
            };

            _secondaryBuffer?.Dispose();
            _secondaryBuffer = new SecondarySoundBuffer(_directSound, bufferDesc);

            // Ses verisini buffer'a yaz
            var audioData = new byte[reader.Length];
            int bytesRead = 0;
            int offset = 0;

            // Float samples'ı 16-bit PCM'e çevir
            var sampleBuffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            var pcmData = new List<byte>();

            while ((bytesRead = reader.Read(sampleBuffer, 0, sampleBuffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    var sample = (short)(sampleBuffer[i] * volume * 32767);
                    pcmData.Add((byte)(sample & 0xFF));
                    pcmData.Add((byte)((sample >> 8) & 0xFF));
                }
            }

            var finalData = pcmData.ToArray();

            // Buffer'a yaz
            _secondaryBuffer.Write(finalData, 0, LockFlags.None);

            // Oynat
            _secondaryBuffer.Play(0, PlayFlags.None);

            System.Diagnostics.Debug.WriteLine($"DirectSound playing: {filePath}");
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
        _secondaryBuffer?.Stop();

        _currentSpeakerReader?.Dispose();
        _currentSpeakerReader = null;
    }

    public bool IsPlaying =>
        (_speakerOutput?.PlaybackState == PlaybackState.Playing) ||
        ((_secondaryBuffer?.Status & (int)BufferStatus.Playing) != 0);

    public void Dispose()
    {
        Stop();
        _speakerOutput?.Dispose();
        _currentSpeakerReader?.Dispose();
        _secondaryBuffer?.Dispose();
        _captureBuffer?.Dispose();
        _capture?.Dispose();
        _directSound?.Dispose();
    }
}
