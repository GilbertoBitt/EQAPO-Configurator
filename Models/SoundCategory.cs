namespace EQAPO_Configurator.Models;

public class SoundCategory
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public double SliderValue { get; set; } // -12 to +12 dB
    public string FrequencyRange { get; set; } = "";
    public List<FilterMapping> Filters { get; set; } = new();
}

public class FilterMapping
{
    public int FilterIndex { get; set; }
    public string OriginalFilter { get; set; } = ""; // The raw filter line
    public double BaseGain { get; set; }  // From headphone correction
    public double UserOffset { get; set; } // User's slider adjustment
    public double CenterFrequency { get; set; }
    public double Q { get; set; }
    public string FilterType { get; set; } = "PK"; // PK, LSC, HSC
}
