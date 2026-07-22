using System.ComponentModel;

namespace EQAPO_Configurator.Models;

public class SpectrumFrame : INotifyPropertyChanged
{
    private double[] _magnitudes = [];
    private double _peakDb;
    private double _rmsDb;

    public DateTime Timestamp { get; set; }
    public int SampleRate { get; set; }
    public int FftSize { get; set; }

    public double[] Frequencies { get; set; } = [];
    public double[] Magnitudes
    {
        get => _magnitudes;
        set { _magnitudes = value; OnPropertyChanged(nameof(Magnitudes)); }
    }
    public double PeakDb
    {
        get => _peakDb;
        set { _peakDb = value; OnPropertyChanged(nameof(PeakDb)); }
    }
    public double RmsDb
    {
        get => _rmsDb;
        set { _rmsDb = value; OnPropertyChanged(nameof(RmsDb)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SpectrumConfig
{
    public int FftSize { get; set; } = 2048;
    public int MinFrequency { get; set; } = 20;
    public int MaxFrequency { get; set; } = 20000;
    public int NumBins { get; set; } = 256;
    public double SmoothingFactor { get; set; } = 0.5;
    public double GainDbFloor { get; set; } = -80;
    public double GainDbCeiling { get; set; } = 0;
}

public class AudioClip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public int SampleRate { get; set; }
    public int ChannelCount { get; set; }
    public float[] AudioData { get; set; } = [];
    public List<SpectrumFrame> SpectrumFrames { get; set; } = [];
    public double DurationSeconds => SampleRate > 0 ? (double)AudioData.Length / (SampleRate * ChannelCount) : 0;
    public string? ActiveProfileName { get; set; }
    public string? ActiveGameName { get; set; }
}
