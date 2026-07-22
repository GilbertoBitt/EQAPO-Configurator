using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Tests;

public class SpectrumDataTests
{
    [Fact]
    public void SpectrumFrame_DefaultValues_AreCorrect()
    {
        var frame = new SpectrumFrame();

        Assert.Equal(0, frame.PeakDb);
        Assert.Equal(0, frame.RmsDb);
        Assert.Empty(frame.Magnitudes);
        Assert.Empty(frame.Frequencies);
    }

    [Fact]
    public void SpectrumFrame_SetMagnitudes_ReturnsSetValue()
    {
        var frame = new SpectrumFrame();
        double[] mags = [-40.0, -20.0, -10.0, -3.0];
        frame.Magnitudes = mags;

        Assert.Equal(mags, frame.Magnitudes);
    }

    [Fact]
    public void SpectrumFrame_RaisesPropertyChanged()
    {
        var frame = new SpectrumFrame();
        bool raised = false;
        frame.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpectrumFrame.Magnitudes)) raised = true;
        };

        frame.Magnitudes = [1.0, 2.0];
        Assert.True(raised);
    }

    [Fact]
    public void SpectrumFrame_PeakDb_RaisesPropertyChanged()
    {
        var frame = new SpectrumFrame();
        bool raised = false;
        frame.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpectrumFrame.PeakDb)) raised = true;
        };

        frame.PeakDb = -6.0;
        Assert.True(raised);
    }

    [Fact]
    public void SpectrumFrame_RmsDb_RaisesPropertyChanged()
    {
        var frame = new SpectrumFrame();
        bool raised = false;
        frame.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpectrumFrame.RmsDb)) raised = true;
        };

        frame.RmsDb = -12.0;
        Assert.True(raised);
    }

    [Fact]
    public void SpectrumConfig_DefaultValues_AreReasonable()
    {
        var config = new SpectrumConfig();

        Assert.Equal(4096, config.FftSize);
        Assert.Equal(20, config.MinFrequency);
        Assert.Equal(20000, config.MaxFrequency);
        Assert.Equal(256, config.NumBins);
        Assert.True(config.SmoothingFactor > 0 && config.SmoothingFactor < 1);
    }

    [Fact]
    public void SpectrumConfig_CustomValues_AreStored()
    {
        var config = new SpectrumConfig
        {
            FftSize = 8192,
            NumBins = 512,
            SmoothingFactor = 0.5,
            GainDbFloor = -100
        };

        Assert.Equal(8192, config.FftSize);
        Assert.Equal(512, config.NumBins);
        Assert.Equal(0.5, config.SmoothingFactor);
        Assert.Equal(-100, config.GainDbFloor);
    }

    [Fact]
    public void AudioClip_DefaultValues_AreCorrect()
    {
        var clip = new AudioClip();

        Assert.NotEmpty(clip.Id);
        Assert.Equal(8, clip.Id.Length);
        Assert.Empty(clip.AudioData);
        Assert.Empty(clip.SpectrumFrames);
        Assert.Equal(0, clip.DurationSeconds);
    }

    [Fact]
    public void AudioClip_DurationSeconds_CalculatesCorrectly()
    {
        var clip = new AudioClip
        {
            SampleRate = 44100,
            ChannelCount = 2,
            AudioData = new float[44100 * 2 * 5]
        };

        Assert.Equal(5.0, clip.DurationSeconds, 1);
    }

    [Fact]
    public void AudioClip_DurationSeconds_ZeroSampleRate_ReturnsZero()
    {
        var clip = new AudioClip { SampleRate = 0 };
        Assert.Equal(0, clip.DurationSeconds);
    }

    [Fact]
    public void AudioClip_Id_IsUnique()
    {
        var clip1 = new AudioClip();
        var clip2 = new AudioClip();

        Assert.NotEqual(clip1.Id, clip2.Id);
    }

    [Fact]
    public void AudioClip_ActiveProfileName_DefaultsToNull()
    {
        var clip = new AudioClip();
        Assert.Null(clip.ActiveProfileName);
        Assert.Null(clip.ActiveGameName);
    }
}
