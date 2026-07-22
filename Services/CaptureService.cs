using System.IO;
using System.Text.Json;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public class CaptureService
{
    private readonly SpectrumService _spectrum;
    private readonly List<SpectrumFrame> _frameBuffer = new();
    private readonly object _frameLock = new();
    private readonly int _maxFrames;
    private readonly int _captureDurationMs;
    private readonly string _captureDir;
    private List<float> _audioBuffer = new();
    private DateTime? _captureStartTime;
    private bool _isCapturingClip;

    public bool IsCapturingClip => _isCapturingClip;
    public event Action<AudioClip>? ClipCaptured;

    public CaptureService(SpectrumService spectrum, int captureDurationMs = 5000, int maxFps = 15)
    {
        _spectrum = spectrum;
        _captureDurationMs = captureDurationMs;
        _maxFrames = captureDurationMs * maxFps / 1000;
        _captureDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EQAPO-Configurator", "captures");
        Directory.CreateDirectory(_captureDir);

        _spectrum.SpectrumUpdated += OnSpectrumFrame;
    }

    public void StartCapture(string? profileName = null, string? gameName = null)
    {
        if (_isCapturingClip) return;

        _isCapturingClip = true;
        _captureStartTime = DateTime.Now;

        lock (_frameLock)
        {
            _frameBuffer.Clear();
        }

        _audioBuffer = _spectrum.GetCapturedSamples(_captureDurationMs);
    }

    public AudioClip StopCapture(string? profileName = null, string? gameName = null)
    {
        if (!_isCapturingClip) return new AudioClip();
        _isCapturingClip = false;

        List<SpectrumFrame> frames;
        lock (_frameLock)
        {
            frames = new List<SpectrumFrame>(_frameBuffer);
        }

        var clip = new AudioClip
        {
            Name = $"Clip {DateTime.Now:yyyy-MM-dd HH-mm-ss}",
            CapturedAt = _captureStartTime ?? DateTime.Now,
            SampleRate = 44100,
            ChannelCount = 1,
            AudioData = _audioBuffer.ToArray(),
            SpectrumFrames = frames,
            ActiveProfileName = profileName,
            ActiveGameName = gameName
        };

        SaveClip(clip);
        ClipCaptured?.Invoke(clip);
        return clip;
    }

    private void OnSpectrumFrame(SpectrumFrame frame)
    {
        if (!_isCapturingClip) return;

        lock (_frameLock)
        {
            if (_frameBuffer.Count < _maxFrames)
            {
                _frameBuffer.Add(new SpectrumFrame
                {
                    Timestamp = frame.Timestamp,
                    SampleRate = frame.SampleRate,
                    FftSize = frame.FftSize,
                    Frequencies = (double[])frame.Frequencies.Clone(),
                    Magnitudes = (double[])frame.Magnitudes.Clone(),
                    PeakDb = frame.PeakDb,
                    RmsDb = frame.RmsDb
                });
            }
        }

        _audioBuffer = _spectrum.GetCapturedSamples(_captureDurationMs);
    }

    public void SaveClip(AudioClip clip)
    {
        var clipDir = Path.Combine(_captureDir, clip.Id);
        Directory.CreateDirectory(clipDir);

        string jsonPath = Path.Combine(clipDir, "clip.json");
        var json = JsonSerializer.Serialize(clip, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);

        if (clip.AudioData.Length > 0)
        {
            string wavPath = Path.Combine(clipDir, "audio.wav");
            WriteWav(wavPath, clip.AudioData, clip.SampleRate, clip.ChannelCount);
        }
    }

    public AudioClip? LoadClip(string clipId)
    {
        string clipDir = Path.Combine(_captureDir, clipId);
        string jsonPath = Path.Combine(clipDir, "clip.json");

        if (!File.Exists(jsonPath)) return null;

        string json = File.ReadAllText(jsonPath);
        return JsonSerializer.Deserialize<AudioClip>(json);
    }

    public List<AudioClip> GetSavedClips()
    {
        var clips = new List<AudioClip>();
        if (!Directory.Exists(_captureDir)) return clips;

        foreach (var dir in Directory.GetDirectories(_captureDir))
        {
            string jsonPath = Path.Combine(dir, "clip.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    var clip = JsonSerializer.Deserialize<AudioClip>(json);
                    if (clip != null) clips.Add(clip);
                }
                catch { }
            }
        }

        return clips.OrderByDescending(c => c.CapturedAt).ToList();
    }

    public bool DeleteClip(string clipId)
    {
        string clipDir = Path.Combine(_captureDir, clipId);
        if (!Directory.Exists(clipDir)) return false;

        try
        {
            Directory.Delete(clipDir, true);
            return true;
        }
        catch { return false; }
    }

    private static void WriteWav(string path, float[] samples, int sampleRate, int channels)
    {
        using var writer = new BinaryWriter(File.Create(path));
        int bitsPerSample = 16;
        int bytesPerSample = bitsPerSample / 8;
        int dataSize = samples.Length * bytesPerSample;
        int fileSize = 44 + dataSize;

        writer.Write("RIFF"u8);
        writer.Write(fileSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample);
        writer.Write((short)(channels * bytesPerSample));
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);

        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Math.Clamp(samples[i], -1f, 1f);
            short s = (short)(clamped * short.MaxValue);
            writer.Write(s);
        }
    }
}
