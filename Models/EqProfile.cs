using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace EQAPO_Configurator.Models;

public class EqProfile : INotifyPropertyChanged
{
    private string _name = "Custom EQ";
    private string _description = "";
    private double _preamp;
    private List<EqBand> _bands = new();

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public double Preamp
    {
        get => _preamp;
        set { _preamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(PreampDisplay)); }
    }

    public string PreampDisplay => $"{Preamp:+0.0;-0.0;0.0} dB";

    public List<EqBand> Bands
    {
        get => _bands;
        set { _bands = value; OnPropertyChanged(); OnPropertyChanged(nameof(BandCount)); }
    }

    public int BandCount => Bands.Count(b => b.Enabled);

    public static EqProfile CreateDefault10Band()
    {
        var profile = new EqProfile { Name = "Custom EQ" };
        profile.Bands = new List<EqBand>
        {
            new() { Index = 1, FilterType = EqFilterType.PK, Frequency = 32, Q = 1.0 },
            new() { Index = 2, FilterType = EqFilterType.PK, Frequency = 64, Q = 1.0 },
            new() { Index = 3, FilterType = EqFilterType.PK, Frequency = 125, Q = 1.0 },
            new() { Index = 4, FilterType = EqFilterType.PK, Frequency = 250, Q = 1.0 },
            new() { Index = 5, FilterType = EqFilterType.PK, Frequency = 500, Q = 1.0 },
            new() { Index = 6, FilterType = EqFilterType.PK, Frequency = 1000, Q = 1.0 },
            new() { Index = 7, FilterType = EqFilterType.PK, Frequency = 2000, Q = 1.0 },
            new() { Index = 8, FilterType = EqFilterType.PK, Frequency = 4000, Q = 1.0 },
            new() { Index = 9, FilterType = EqFilterType.PK, Frequency = 8000, Q = 1.0 },
            new() { Index = 10, FilterType = EqFilterType.PK, Frequency = 16000, Q = 1.0 },
        };
        return profile;
    }

    public static EqProfile FromEqaPoConfig(string configText)
    {
        var profile = new EqProfile { Name = "Imported EQ" };
        var bands = new List<EqBand>();

        foreach (var line in configText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Filter ") && trimmed.Contains(" Fc "))
            {
                var band = EqBand.Parse(trimmed);
                if (band.Index > 0) bands.Add(band);
            }
            else if (trimmed.StartsWith("Preamp:"))
            {
                var valStr = trimmed.Replace("Preamp:", "").Replace("dB", "").Trim();
                if (double.TryParse(valStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double val))
                    profile.Preamp = val;
            }
            else if (trimmed.StartsWith("#"))
            {
                var comment = trimmed.TrimStart('#').Trim();
                if (comment.Length > 0 && profile.Description.Length == 0)
                    profile.Description = comment;
            }
        }

        profile.Bands = bands;
        return profile;
    }

    public string ToEqaPoString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Name}");
        if (!string.IsNullOrEmpty(Description))
            sb.AppendLine($"# {Description}");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Preamp: {Preamp:F1} dB");
        sb.AppendLine();

        foreach (var band in Bands)
        {
            string line = band.ToEqaPoString();
            if (!string.IsNullOrEmpty(line))
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    // JSON serialization for import/export
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static EqProfile? FromJson(string json)
        => JsonSerializer.Deserialize<EqProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
