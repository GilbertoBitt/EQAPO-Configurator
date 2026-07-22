using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Tests;

public class BiquadFilterTests
{
    [Fact]
    public void MagnitudeDb_PeakingAtCenterFreq_ReturnsGainValue()
    {
        var band = new EqBand
        {
            FilterType = EqFilterType.PK,
            Frequency = 1000,
            Gain = 6.0,
            Q = 1.0
        };

        double db = BiquadFilter.MagnitudeDb(band, 1000);
        Assert.InRange(db, 5.5, 6.5);
    }

    [Fact]
    public void MagnitudeDb_PeakingFarFromCenter_IsZero()
    {
        var band = new EqBand
        {
            FilterType = EqFilterType.PK,
            Frequency = 1000,
            Gain = 6.0,
            Q = 1.0
        };

        double db = BiquadFilter.MagnitudeDb(band, 100);
        Assert.InRange(db, -0.5, 0.5);
    }

    [Fact]
    public void MagnitudeDb_ZeroGain_IsZero()
    {
        var band = new EqBand
        {
            FilterType = EqFilterType.PK,
            Frequency = 1000,
            Gain = 0.0,
            Q = 1.0
        };

        double db = BiquadFilter.MagnitudeDb(band, 1000);
        Assert.InRange(db, -0.1, 0.1);
    }

    [Fact]
    public void MagnitudeDb_NegativeGain_ReducesLevel()
    {
        var band = new EqBand
        {
            FilterType = EqFilterType.PK,
            Frequency = 500,
            Gain = -6.0,
            Q = 1.0
        };

        double db = BiquadFilter.MagnitudeDb(band, 500);
        Assert.InRange(db, -6.5, -5.5);
    }

    [Fact]
    public void MagnitudeDb_HighQ_NarrowPeak()
    {
        var narrow = new EqBand { FilterType = EqFilterType.PK, Frequency = 1000, Gain = 6.0, Q = 5.0 };
        var wide = new EqBand { FilterType = EqFilterType.PK, Frequency = 1000, Gain = 6.0, Q = 0.5 };

        double narrowDb = BiquadFilter.MagnitudeDb(narrow, 800);
        double wideDb = BiquadFilter.MagnitudeDb(wide, 800);

        Assert.True(wideDb > narrowDb, "Wider Q should have more gain at offset frequency");
    }

    [Fact]
    public void CombinedMagnitudeDb_NoFilters_IsZero()
    {
        var bands = new List<EqBand>();
        double db = BiquadFilter.CombinedMagnitudeDb(bands, 1000);
        Assert.InRange(db, -0.1, 0.1);
    }

    [Fact]
    public void CombinedMagnitudeDb_MultipleFilters_CombinesCorrectly()
    {
        var bands = new List<EqBand>
        {
            new() { FilterType = EqFilterType.PK, Frequency = 100, Gain = 3.0, Q = 1.0 },
            new() { FilterType = EqFilterType.PK, Frequency = 100, Gain = 3.0, Q = 1.0 }
        };

        double db = BiquadFilter.CombinedMagnitudeDb(bands, 100);
        Assert.InRange(db, 5.5, 6.5);
    }

    [Fact]
    public void GenerateResponseCurve_ReturnsCorrectNumberOfPoints()
    {
        var bands = new List<EqBand>
        {
            new() { FilterType = EqFilterType.PK, Frequency = 1000, Gain = 0, Q = 1.0 }
        };

        var points = BiquadFilter.GenerateResponseCurve(bands, 100);
        Assert.Equal(100, points.Count);
    }

    [Fact]
    public void GenerateResponseCurve_FrequencyRangeIs20To20k()
    {
        var bands = new List<EqBand>
        {
            new() { FilterType = EqFilterType.PK, Frequency = 1000, Gain = 0, Q = 1.0 }
        };

        var points = BiquadFilter.GenerateResponseCurve(bands, 10);
        Assert.Equal(20, points[0].Freq, 0);
        Assert.Equal(20000, points[^1].Freq, 0);
    }

    [Fact]
    public void LowShelf_BoostsLowFrequencies()
    {
        var band = new EqBand
        {
            FilterType = EqFilterType.LSC,
            Frequency = 200,
            Gain = 6.0,
            Q = 0.7
        };

        double lowDb = BiquadFilter.MagnitudeDb(band, 50);
        double highDb = BiquadFilter.MagnitudeDb(band, 5000);

        Assert.True(lowDb > highDb, "Low shelf should boost low frequencies more than high");
    }

    [Fact]
    public void HighShelf_BoostsHighFrequencies()
    {
        var band = new EqBand
        {
            FilterType = EqFilterType.HSC,
            Frequency = 4000,
            Gain = 6.0,
            Q = 0.7
        };

        double lowDb = BiquadFilter.MagnitudeDb(band, 100);
        double highDb = BiquadFilter.MagnitudeDb(band, 10000);

        Assert.True(highDb > lowDb, "High shelf should boost high frequencies more than low");
    }
}
