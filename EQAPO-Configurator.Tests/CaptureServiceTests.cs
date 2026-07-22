using System.IO;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Tests;

public class CaptureServiceTests : IDisposable
{
    private readonly SpectrumService _spectrum;
    private readonly CaptureService _capture;
    private readonly string _testDir;

    public CaptureServiceTests()
    {
        _spectrum = new SpectrumService();
        _capture = new CaptureService(_spectrum, captureDurationMs: 5000);
        _testDir = Path.Combine(Path.GetTempPath(), $"eqapo_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        _spectrum.Dispose();
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void SaveClip_CreatesDirectoryAndFiles()
    {
        var clip = new AudioClip
        {
            Id = "test1234",
            Name = "Test Clip",
            CapturedAt = DateTime.Now,
            SampleRate = 44100,
            ChannelCount = 1,
            AudioData = [0.1f, 0.2f, 0.3f],
            SpectrumFrames = new List<SpectrumFrame>
            {
                new()
                {
                    Timestamp = DateTime.Now,
                    SampleRate = 44100,
                    FftSize = 4096,
                    Frequencies = [100, 1000, 10000],
                    Magnitudes = [-20, -10, -5],
                    PeakDb = -3,
                    RmsDb = -12
                }
            }
        };

        _capture.SaveClip(clip);

        string clipDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EQAPO-Configurator", "captures", "test1234");

        Assert.True(Directory.Exists(clipDir));
        Assert.True(File.Exists(Path.Combine(clipDir, "clip.json")));
        Assert.True(File.Exists(Path.Combine(clipDir, "audio.wav")));

        string json = File.ReadAllText(Path.Combine(clipDir, "clip.json"));
        Assert.Contains("Test Clip", json);
        Assert.Contains("test1234", json);

        _capture.DeleteClip("test1234");
    }

    [Fact]
    public void LoadClip_ReturnsSavedClip()
    {
        var clip = new AudioClip
        {
            Id = "loadtest1",
            Name = "Load Test",
            SampleRate = 44100,
            ChannelCount = 2,
            AudioData = [0.5f, -0.5f, 0.25f],
            SpectrumFrames = new List<SpectrumFrame>
            {
                new()
                {
                    Timestamp = DateTime.UtcNow,
                    SampleRate = 44100,
                    FftSize = 4096,
                    Frequencies = [1000],
                    Magnitudes = [-6.0],
                    PeakDb = -3.0,
                    RmsDb = -10.0
                }
            },
            ActiveProfileName = "FPS Profile",
            ActiveGameName = "Call of Duty"
        };

        _capture.SaveClip(clip);
        var loaded = _capture.LoadClip("loadtest1");

        Assert.NotNull(loaded);
        Assert.Equal("Load Test", loaded!.Name);
        Assert.Equal(44100, loaded.SampleRate);
        Assert.Equal(2, loaded.ChannelCount);
        Assert.Equal(3, loaded.AudioData.Length);
        Assert.Single(loaded.SpectrumFrames);
        Assert.Equal("FPS Profile", loaded.ActiveProfileName);
        Assert.Equal("Call of Duty", loaded.ActiveGameName);

        _capture.DeleteClip("loadtest1");
    }

    [Fact]
    public void LoadClip_NonExistentId_ReturnsNull()
    {
        var result = _capture.LoadClip("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void DeleteClip_ExistingId_ReturnsTrue()
    {
        var clip = new AudioClip
        {
            Id = "deltst1",
            Name = "Delete Test",
            SampleRate = 44100,
            ChannelCount = 1,
            AudioData = [0f]
        };

        _capture.SaveClip(clip);
        bool result = _capture.DeleteClip("deltst1");

        Assert.True(result);
        Assert.Null(_capture.LoadClip("deltst1"));
    }

    [Fact]
    public void DeleteClip_NonExistentId_ReturnsFalse()
    {
        bool result = _capture.DeleteClip("noexist1");
        Assert.False(result);
    }

    [Fact]
    public void GetSavedClips_ReturnsOrderedByDate()
    {
        var clip1 = new AudioClip
        {
            Id = "order1",
            Name = "First",
            CapturedAt = DateTime.Now.AddMinutes(-10),
            SampleRate = 44100,
            ChannelCount = 1,
            AudioData = [0f]
        };
        var clip2 = new AudioClip
        {
            Id = "order2",
            Name = "Second",
            CapturedAt = DateTime.Now,
            SampleRate = 44100,
            ChannelCount = 1,
            AudioData = [0f]
        };

        _capture.SaveClip(clip1);
        _capture.SaveClip(clip2);

        var clips = _capture.GetSavedClips();
        Assert.True(clips.Count >= 2);
        Assert.Equal("Second", clips[0].Name);

        _capture.DeleteClip("order1");
        _capture.DeleteClip("order2");
    }

    [Fact]
    public void IsCapturingClip_DefaultsFalse()
    {
        Assert.False(_capture.IsCapturingClip);
    }

    [Fact]
    public void StartCapture_SetsIsCapturing()
    {
        _capture.StartCapture();
        Assert.True(_capture.IsCapturingClip);
        _capture.StopCapture();
    }

    [Fact]
    public void StopCapture_ReturnsClip()
    {
        _capture.StartCapture("TestProfile", "TestGame");
        var clip = _capture.StopCapture("TestProfile", "TestGame");

        Assert.NotNull(clip);
        Assert.Equal("TestProfile", clip.ActiveProfileName);
        Assert.Equal("TestGame", clip.ActiveGameName);
        Assert.False(_capture.IsCapturingClip);
    }
}
