using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Tests;

public class EqBandTests
{
    [Fact]
    public void Parse_StandardFilterLine_ParsesCorrectly()
    {
        string line = "Filter 3: ON PK Fc 1000.0 Hz Gain -2.5 dB Q 1.20";
        var band = EqBand.Parse(line);

        Assert.Equal(3, band.Index);
        Assert.True(band.Enabled);
        Assert.Equal(EqFilterType.PK, band.FilterType);
        Assert.Equal(1000.0, band.Frequency, 1);
        Assert.Equal(-2.5, band.Gain, 1);
        Assert.Equal(1.20, band.Q, 2);
    }

    [Fact]
    public void Parse_OffFilter_IsDisabled()
    {
        string line = "Filter 5: OFF LSC Fc 105.0 Hz Gain -3.8 dB Q 0.70";
        var band = EqBand.Parse(line);

        Assert.Equal(5, band.Index);
        Assert.False(band.Enabled);
        Assert.Equal(EqFilterType.LSC, band.FilterType);
    }

    [Fact]
    public void Parse_HighShelf_ParsesCorrectly()
    {
        string line = "Filter 10: ON HSC Fc 10000.0 Hz Gain -1.7 dB Q 0.70";
        var band = EqBand.Parse(line);

        Assert.Equal(EqFilterType.HSC, band.FilterType);
        Assert.Equal(10000.0, band.Frequency, 0);
    }

    [Fact]
    public void ToEqaPoString_RoundTrips()
    {
        string original = "Filter 1: ON PK Fc 250.0 Hz Gain 3.5 dB Q 1.40";
        var band = EqBand.Parse(original);
        string output = band.ToEqaPoString();

        Assert.Contains("PK", output);
        Assert.Contains("250.0", output);
        Assert.Contains("3.5", output);
        Assert.Contains("1.40", output);
    }

    [Fact]
    public void ToEqaPoString_DisabledBand_ReturnsEmpty()
    {
        var band = new EqBand { Enabled = false, Index = 1 };
        Assert.Equal("", band.ToEqaPoString());
    }

    [Fact]
    public void FrequencyDisplay_ShowsKForThousands()
    {
        var band = new EqBand { Frequency = 4000 };
        Assert.Equal("4.0k", band.FrequencyDisplay);
    }

    [Fact]
    public void FrequencyDisplay_ShowsHzBelow1000()
    {
        var band = new EqBand { Frequency = 250 };
        Assert.Equal("250", band.FrequencyDisplay);
    }

    [Fact]
    public void GainDisplay_ShowsSign()
    {
        var band = new EqBand { Gain = 3.5 };
        Assert.Contains("+3.5", band.GainDisplay);
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        var original = new EqBand
        {
            Index = 5,
            FilterType = EqFilterType.LSC,
            Frequency = 200,
            Gain = -3.0,
            Q = 2.5,
            Enabled = false
        };

        var clone = original.Clone();

        Assert.Equal(original.Index, clone.Index);
        Assert.Equal(original.FilterType, clone.FilterType);
        Assert.Equal(original.Frequency, clone.Frequency);
        Assert.Equal(original.Gain, clone.Gain);
        Assert.Equal(original.Q, clone.Q);
        Assert.Equal(original.Enabled, clone.Enabled);
    }
}
