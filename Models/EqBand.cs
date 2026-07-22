using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EQAPO_Configurator.Models;

public enum EqFilterType
{
    PK,     // Peaking
    LSC,    // Low Shelf
    HSC,    // High Shelf
    LP,     // Low Pass
    HP,     // High Pass
    BP,     // Band Pass
    NOTCH,  // Notch
}

public class EqBand : INotifyPropertyChanged
{
    private int _index;
    private EqFilterType _filterType = EqFilterType.PK;
    private double _frequency = 1000;
    private double _gain;
    private double _q = 1.0;
    private bool _enabled = true;
    private double _bandLevelDb = -80;

    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    public EqFilterType FilterType
    {
        get => _filterType;
        set { _filterType = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilterTypeString)); }
    }

    public string FilterTypeString => FilterType.ToString();

    public double Frequency
    {
        get => _frequency;
        set { _frequency = value; OnPropertyChanged(); OnPropertyChanged(nameof(FrequencyDisplay)); }
    }

    public double Gain
    {
        get => _gain;
        set { _gain = value; OnPropertyChanged(); OnPropertyChanged(nameof(GainDisplay)); }
    }

    public double Q
    {
        get => _q;
        set { _q = value; OnPropertyChanged(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Real-time audio level at this band's center frequency (dB).
    /// Updated by the spectrum analyzer each frame.
    /// </summary>
    public double BandLevelDb
    {
        get => _bandLevelDb;
        set { _bandLevelDb = value; OnPropertyChanged(); }
    }

    public string FrequencyDisplay => Frequency >= 1000 ? $"{Frequency / 1000:F1}k" : $"{Frequency:F0}";
    public string GainDisplay => $"{Gain:+0.0;-0.0;0.0} dB";
    public string BandLevelDisplay => BandLevelDb > -70 ? $"{BandLevelDb:F0} dB" : "";

    public string ToEqaPoString()
    {
        if (!Enabled) return "";
        string type = FilterType switch
        {
            EqFilterType.PK => "PK",
            EqFilterType.LSC => "LSC",
            EqFilterType.HSC => "HSC",
            EqFilterType.LP => "LP",
            EqFilterType.HP => "HP",
            EqFilterType.BP => "BP",
            EqFilterType.NOTCH => "NO",
            _ => "PK"
        };
        return $"Filter {Index}: ON {type} Fc {Frequency:F1} Hz Gain {Gain:+0.0;-0.0;0.0} dB Q {Q:F2}";
    }

    public EqBand Clone()
    {
        return new EqBand
        {
            Index = Index,
            FilterType = FilterType,
            Frequency = Frequency,
            Gain = Gain,
            Q = Q,
            Enabled = Enabled
        };
    }

    public static EqBand Parse(string line)
    {
        // Parse: "Filter 1: ON PK Fc 1000.0 Hz Gain 0.0 dB Q 1.00"
        var band = new EqBand();
        try
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 12) return band;

            band.Index = int.Parse(parts[1].TrimEnd(':'));
            band.Enabled = parts[2] == "ON";

            band.FilterType = parts[3] switch
            {
                "PK" => EqFilterType.PK,
                "LSC" => EqFilterType.LSC,
                "HSC" => EqFilterType.HSC,
                "LP" => EqFilterType.LP,
                "HP" => EqFilterType.HP,
                "BP" => EqFilterType.BP,
                "NO" => EqFilterType.NOTCH,
                _ => EqFilterType.PK
            };

            for (int i = 4; i < parts.Length; i++)
            {
                if (parts[i] == "Fc" && i + 1 < parts.Length)
                    band.Frequency = double.Parse(parts[i + 1].TrimEnd("Hz".ToCharArray()), System.Globalization.CultureInfo.InvariantCulture);
                else if (parts[i] == "Gain" && i + 1 < parts.Length)
                    band.Gain = double.Parse(parts[i + 1].TrimEnd("dB".ToCharArray()), System.Globalization.CultureInfo.InvariantCulture);
                else if (parts[i] == "Q" && i + 1 < parts.Length)
                    band.Q = double.Parse(parts[i + 1], System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch { }
        return band;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
