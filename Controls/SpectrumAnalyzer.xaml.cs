using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Controls;

public partial class SpectrumAnalyzer : UserControl
{
    private readonly List<EqBand> _eqBands = new();
    private readonly DispatcherTimer _renderTimer;
    private SpectrumFrame? _latestFrame;

    public bool ShowEqOverlay { get; set; } = true;

    private static readonly SolidColorBrush BarFill = new(Color.FromRgb(0, 180, 216));
    private static readonly SolidColorBrush BarPeak = new(Color.FromRgb(0, 224, 255));
    private static readonly SolidColorBrush EqLineBrush = new(Color.FromRgb(255, 107, 107));
    private static readonly SolidColorBrush GridBrush = new(Color.FromArgb(30, 255, 255, 255));
    private static readonly SolidColorBrush LabelBrush;

    static SpectrumAnalyzer()
    {
        BarFill.Freeze();
        BarPeak.Freeze();
        EqLineBrush.Freeze();
        GridBrush.Freeze();
        LabelBrush = new SolidColorBrush(Color.FromRgb(120, 120, 120));
        LabelBrush.Freeze();
    }

    public SpectrumAnalyzer()
    {
        InitializeComponent();
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 30)
        };
        _renderTimer.Tick += RenderFrame;
        _renderTimer.Start();
    }

    public void UpdateFrame(SpectrumFrame frame)
    {
        _latestFrame = frame;
    }

    public void SetEqBands(IEnumerable<EqBand> bands)
    {
        _eqBands.Clear();
        _eqBands.AddRange(bands);
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        var frame = _latestFrame;
        if (frame == null || SpectrumCanvas.ActualWidth < 1 || SpectrumCanvas.ActualHeight < 1)
            return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        int binCount = frame.Magnitudes.Length;

        if (binCount == 0) return;

        SpectrumCanvas.Children.Clear();
        LabelsCanvas.Children.Clear();
        EqOverlayCanvas.Children.Clear();

        DrawGrid(width, height, frame);

        double barWidth = Math.Max(1, (width - binCount) / binCount);
        double gap = Math.Min(1, (width - binCount * barWidth) / Math.Max(1, binCount - 1));

        double dbMin = -80;
        double dbMax = 0;
        double range = dbMax - dbMin;

        for (int i = 0; i < binCount; i++)
        {
            double db = frame.Magnitudes[i];
            double normalized = (db - dbMin) / range;
            normalized = Math.Clamp(normalized, 0, 1);

            double barHeight = normalized * height;
            double x = i * (barWidth + gap);
            double y = height - barHeight;

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = barWidth,
                Height = Math.Max(1, barHeight),
                Fill = normalized > 0.85 ? BarPeak : BarFill,
                RadiusX = barWidth > 2 ? 1 : 0,
                RadiusY = barWidth > 2 ? 1 : 0
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            SpectrumCanvas.Children.Add(rect);
        }

        if (ShowEqOverlay && _eqBands.Count > 0)
            DrawEqOverlay(width, height);

        // Update per-band real-time volume levels
        UpdateBandLevels(frame);

        PeakText.Text = $"Peak: {frame.PeakDb:F1} dB";
        RmsText.Text = $"RMS: {frame.RmsDb:F1} dB";
    }

    /// <summary>
    /// For each EQ band, compute post-EQ level (raw spectrum + gain)
    /// so the UI volume meter shows what you actually hear.
    /// </summary>
    private void UpdateBandLevels(SpectrumFrame frame)
    {
        if (frame.Frequencies.Length == 0 || frame.Magnitudes.Length == 0) return;

        foreach (var band in _eqBands)
        {
            double rawDb = GetMagnitudeAtFrequency(frame, band.Frequency);
            band.BandLevelDb = Math.Clamp(rawDb + band.Gain, -80, 0);
        }
    }

    private static double GetMagnitudeAtFrequency(SpectrumFrame frame, double targetFreq)
    {
        double logTarget = Math.Log10(targetFreq);
        double logMin = Math.Log10(20);
        double logMax = Math.Log10(20000);
        double normalizedPos = (logTarget - logMin) / (logMax - logMin);
        int binIndex = (int)(normalizedPos * (frame.Magnitudes.Length - 1));
        binIndex = Math.Clamp(binIndex, 0, frame.Magnitudes.Length - 1);
        return frame.Magnitudes[binIndex];
    }

    private void DrawGrid(double width, double height, SpectrumFrame frame)
    {
        double dbMin = -80, dbMax = 0;
        double[] dbLines = { -60, -40, -20, -10, 0 };

        foreach (double db in dbLines)
        {
            double y = height * (1 - (db - dbMin) / (dbMax - dbMin));
            y = Math.Clamp(y, 0, height);

            var line = new System.Windows.Shapes.Line
            {
                X1 = 0, Y1 = y, X2 = width, Y2 = y,
                Stroke = GridBrush, StrokeThickness = 0.5
            };
            SpectrumCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = $"{db}",
                FontSize = 8,
                Foreground = LabelBrush
            };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y - 10);
            LabelsCanvas.Children.Add(label);
        }

        double[] freqLabels = { 50, 100, 200, 500, 1000, 2000, 5000, 10000 };
        double logMin = Math.Log10(20);
        double logMax = Math.Log10(20000);

        foreach (double freq in freqLabels)
        {
            double logPos = (Math.Log10(freq) - logMin) / (logMax - logMin);
            double x = logPos * width;

            var line = new System.Windows.Shapes.Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = height,
                Stroke = GridBrush, StrokeThickness = 0.5
            };
            SpectrumCanvas.Children.Add(line);

            string label = freq >= 1000 ? $"{freq / 1000}k" : $"{freq}";
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 8,
                Foreground = LabelBrush
            };
            Canvas.SetLeft(tb, x - 10);
            Canvas.SetTop(tb, height - 12);
            LabelsCanvas.Children.Add(tb);
        }
    }

    private void DrawEqOverlay(double width, double height)
    {
        double dbMin = -80, dbMax = 0;
        double range = dbMax - dbMin;
        var points = new System.Windows.Media.StreamGeometry();

        using (var ctx = points.Open())
        {
            bool first = true;
            double logMin = Math.Log10(20);
            double logMax = Math.Log10(20000);

            for (int i = 0; i < 200; i++)
            {
                double logFreq = logMin + (logMax - logMin) * i / 199;
                double freq = Math.Pow(10, logFreq);
                double db = BiquadFilter.CombinedMagnitudeDb(_eqBands, freq);

                double x = (i / 199.0) * width;
                double normalized = (db - dbMin) / range;
                double y = height * (1 - Math.Clamp(normalized, 0, 1));

                if (first)
                {
                    ctx.BeginFigure(new System.Windows.Point(x, y), true, false);
                    first = false;
                }
                else
                {
                    ctx.LineTo(new System.Windows.Point(x, y), true, true);
                }
            }
        }

        points.Freeze();

        var path = new System.Windows.Shapes.Path
        {
            Stroke = EqLineBrush,
            StrokeThickness = 1.5,
            Data = points
        };
        EqOverlayCanvas.Children.Add(path);
    }

    private void SpectrumCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        EqOverlayCanvas.Width = e.NewSize.Width;
        EqOverlayCanvas.Height = e.NewSize.Height;
    }
}
