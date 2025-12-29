using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoiceBoard.Services;

/// <summary>
/// Shared memory structure matching the C++ APO
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct VoiceBoardSharedData
{
    public int IsPlaying;        // atomic bool (4 bytes for alignment)
    public uint WritePos;        // atomic uint
    public uint ReadPos;         // atomic uint
    public uint SampleRate;
    public uint Channels;
    public uint BufferSize;
    // Audio buffer follows (48000 * 2 * 2 floats = ~750KB)
}

public class APOService : IDisposable
{
    private const string SharedMemName = "VoiceBoardSharedMem";
    private const int BufferSizeFloats = 48000 * 2 * 2; // 2 seconds stereo
    private const int HeaderSize = 24; // 6 * 4 bytes

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private bool _isInitialized;

    public bool IsAPOInstalled { get; private set; }
    public bool IsConnected => _isInitialized && _accessor != null;

    public APOService()
    {
        CheckAPOInstallation();
        Initialize();
    }

    private void CheckAPOInstallation()
    {
        // Check if APO is registered in Windows Registry
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Classes\CLSID\{8A5BC3E7-9D4F-4B2A-8C1E-6F3D5A7B9C0E}");
            IsAPOInstalled = key != null;
        }
        catch
        {
            IsAPOInstalled = false;
        }
    }

    private void Initialize()
    {
        try
        {
            // Create shared memory
            int totalSize = HeaderSize + (BufferSizeFloats * sizeof(float));

            _mmf = MemoryMappedFile.CreateOrOpen(
                SharedMemName,
                totalSize,
                MemoryMappedFileAccess.ReadWrite);

            _accessor = _mmf.CreateViewAccessor(0, totalSize, MemoryMappedFileAccess.ReadWrite);

            // Initialize header
            _accessor.Write(0, 0);                    // IsPlaying = false
            _accessor.Write(4, (uint)0);              // WritePos = 0
            _accessor.Write(8, (uint)0);              // ReadPos = 0
            _accessor.Write(12, (uint)48000);         // SampleRate
            _accessor.Write(16, (uint)2);             // Channels (stereo)
            _accessor.Write(20, (uint)BufferSizeFloats); // BufferSize

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"APO Service init error: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Play audio through APO (injected into microphone stream)
    /// </summary>
    public async Task PlayThroughAPOAsync(string filePath, float volume = 1.0f)
    {
        if (!_isInitialized || _accessor == null)
        {
            throw new InvalidOperationException("APO Service not initialized");
        }

        // Read audio file and convert to float samples
        var samples = await Task.Run(() => LoadAudioSamples(filePath, volume));

        if (samples == null || samples.Length == 0)
            return;

        // Write to shared memory
        WriteAudioToSharedMemory(samples);
    }

    private float[]? LoadAudioSamples(string filePath, float volume)
    {
        try
        {
            using var reader = new AudioFileReader(filePath);

            // Convert to 48kHz stereo if needed
            ISampleProvider provider = reader;

            if (reader.WaveFormat.SampleRate != 48000)
            {
                var resampler = new WdlResamplingSampleProvider(reader, 48000);
                provider = resampler;
            }

            // Convert to stereo if mono
            if (provider.WaveFormat.Channels == 1)
            {
                provider = new MonoToStereoSampleProvider(provider);
            }

            // Read all samples
            var samples = new List<float>();
            var buffer = new float[4096];
            int read;

            // Apply volume boost for APO/microphone output
            // Web audio is often quiet - boost by 30x for microphone
            float boostMultiplier = 30.0f * volume;

            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    // Apply boost and clamp to prevent distortion
                    float sample = buffer[i] * boostMultiplier;
                    sample = Math.Clamp(sample, -1.0f, 1.0f);
                    samples.Add(sample);
                }
            }

            // Limit to buffer size
            if (samples.Count > BufferSizeFloats)
            {
                samples = samples.Take(BufferSizeFloats).ToList();
            }

            return samples.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Load audio error: {ex.Message}");
            return null;
        }
    }

    private void WriteAudioToSharedMemory(float[] samples)
    {
        if (_accessor == null) return;

        // Stop any current playback
        _accessor.Write(0, 0); // IsPlaying = false
        Thread.Sleep(10); // Give APO time to notice

        // Reset positions
        _accessor.Write(4, (uint)0);              // WritePos = 0
        _accessor.Write(8, (uint)0);              // ReadPos = 0

        // Write audio data
        int offset = HeaderSize;
        for (int i = 0; i < samples.Length; i++)
        {
            _accessor.Write(offset + (i * sizeof(float)), samples[i]);
        }

        // Update write position
        _accessor.Write(4, (uint)samples.Length); // WritePos = samples.Length

        // Start playback
        _accessor.Write(0, 1); // IsPlaying = true
    }

    /// <summary>
    /// Stop current APO playback
    /// </summary>
    public void Stop()
    {
        if (_accessor != null)
        {
            _accessor.Write(0, 0); // IsPlaying = false
        }
    }

    /// <summary>
    /// Check if APO is currently playing
    /// </summary>
    public bool IsPlaying
    {
        get
        {
            if (_accessor == null) return false;
            return _accessor.ReadInt32(0) != 0;
        }
    }

    public void Dispose()
    {
        Stop();
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}
