using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Tests;

public class EqProfileTests
{
    [Fact]
    public void CreateDefault10Band_HasTenBands()
    {
        var profile = EqProfile.CreateDefault10Band();
        Assert.Equal(10, profile.Bands.Count);
    }

    [Fact]
    public void CreateDefault10Band_AllBandsArePeaking()
    {
        var profile = EqProfile.CreateDefault10Band();
        Assert.All(profile.Bands, b => Assert.Equal(EqFilterType.PK, b.FilterType));
    }

    [Fact]
    public void CreateDefault10Band_BandsAreIndexed()
    {
        var profile = EqProfile.CreateDefault10Band();
        for (int i = 0; i < profile.Bands.Count; i++)
            Assert.Equal(i + 1, profile.Bands[i].Index);
    }

    [Fact]
    public void BandCount_OnlyCountsEnabled()
    {
        var profile = EqProfile.CreateDefault10Band();
        profile.Bands[0].Enabled = false;
        profile.Bands[1].Enabled = false;
        Assert.Equal(8, profile.BandCount);
    }

    [Fact]
    public void FromEqaPoConfig_ParsesPreamp()
    {
        string config = "Preamp: -6.0 dB\nFilter 1: ON PK Fc 1000 Hz Gain 0 dB Q 1.0";
        var profile = EqProfile.FromEqaPoConfig(config);
        Assert.Equal(-6.0, profile.Preamp, 1);
    }

    [Fact]
    public void FromEqaPoConfig_ParsesFilters()
    {
        string config = "Preamp: 0 dB\nFilter 1: ON PK Fc 1000 Hz Gain 3.0 dB Q 1.2\nFilter 2: ON LSC Fc 105 Hz Gain -2.0 dB Q 0.7";
        var profile = EqProfile.FromEqaPoConfig(config);
        Assert.Equal(2, profile.Bands.Count);
        Assert.Equal(1000, profile.Bands[0].Frequency, 0);
        Assert.Equal(105, profile.Bands[1].Frequency, 0);
    }

    [Fact]
    public void FromEqaPoConfig_ParsesDescription()
    {
        string config = "# My Custom EQ\nPreamp: 0 dB\nFilter 1: ON PK Fc 1000 Hz Gain 0 dB Q 1.0";
        var profile = EqProfile.FromEqaPoConfig(config);
        Assert.Equal("My Custom EQ", profile.Description);
    }

    [Fact]
    public void ToEqaPoString_ContainsPreampAndFilters()
    {
        var profile = EqProfile.CreateDefault10Band();
        profile.Preamp = -3.0;
        string output = profile.ToEqaPoString();

        Assert.Contains("Preamp: -3.0 dB", output);
        Assert.Contains("Filter 1:", output);
        Assert.Contains("Filter 10:", output);
    }

    [Fact]
    public void JsonRoundTrip_PreservesData()
    {
        var profile = EqProfile.CreateDefault10Band();
        profile.Name = "Test Profile";
        profile.Preamp = -4.5;
        profile.Bands[0].Gain = 3.0;
        profile.Bands[0].Frequency = 500;

        string json = profile.ToJson();
        var restored = EqProfile.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal("Test Profile", restored!.Name);
        Assert.Equal(-4.5, restored.Preamp, 1);
        Assert.Equal(3.0, restored.Bands[0].Gain, 1);
        Assert.Equal(500, restored.Bands[0].Frequency, 0);
    }

    [Fact]
    public void PreampDisplay_FormatsCorrectly()
    {
        var profile = new EqProfile { Preamp = -6.5 };
        Assert.Equal("-6.5 dB", profile.PreampDisplay);
    }
}
