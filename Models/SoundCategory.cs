using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EQAPO_Configurator.Models;

public class SoundCategory : INotifyPropertyChanged
{
    private double _sliderValue;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public double SliderValue
    {
        get => _sliderValue;
        set
        {
            if (Math.Abs(_sliderValue - value) < 0.001) return;
            _sliderValue = value;
            foreach (var filter in Filters)
                filter.UserOffset = value - filter.BaseGain;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SliderValueText));
        }
    }
    public string SliderValueText => $"{SliderValue:+0.0;-0.0;0.0} dB";
    public string FrequencyRange { get; set; } = "";
    public List<FilterMapping> Filters { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
