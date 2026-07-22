using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Tests;

public class SpectrumServiceTests
{
    [Fact]
    public void Constructor_WithDefaultConfig_CreatesSuccessfully()
    {
        var service = new SpectrumService();
        Assert.NotNull(service);
        Assert.False(service.IsCapturing);
        service.Dispose();
    }

    [Fact]
    public void Constructor_WithCustomConfig_CreatesSuccessfully()
    {
        var config = new SpectrumConfig
        {
            FftSize = 8192,
            NumBins = 128,
            SmoothingFactor = 0.5
        };
        var service = new SpectrumService(config);
        Assert.NotNull(service);
        service.Dispose();
    }

    [Fact]
    public void IsCapturing_BeforeStart_IsFalse()
    {
        var service = new SpectrumService();
        Assert.False(service.IsCapturing);
        service.Dispose();
    }

    [Fact]
    public void Stop_WhenNotCapturing_DoesNotThrow()
    {
        var service = new SpectrumService();
        var ex = Record.Exception(() => service.Stop());
        Assert.Null(ex);
        service.Dispose();
    }

    [Fact]
    public void GetCapturedSamples_ReturnsEmptyList()
    {
        var service = new SpectrumService();
        var samples = service.GetCapturedSamples();
        Assert.NotNull(samples);
        Assert.Empty(samples);
        service.Dispose();
    }

    [Fact]
    public void GetCapturedSamples_WithMaxDuration_ReturnsEmptyList()
    {
        var service = new SpectrumService();
        var samples = service.GetCapturedSamples(1000);
        Assert.NotNull(samples);
        Assert.Empty(samples);
        service.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var service = new SpectrumService();
        service.Dispose();
        var ex = Record.Exception(() => service.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void SpectrumUpdated_EventCanBeSubscribed()
    {
        var service = new SpectrumService();
        service.SpectrumUpdated += _ => { };
        service.SpectrumUpdated -= _ => { };
        service.Dispose();
    }
}
