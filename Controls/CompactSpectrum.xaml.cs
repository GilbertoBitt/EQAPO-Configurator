using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Controls;

/// <summary>
/// Compact 28-bar spectrum visualization.
/// Uses DrawingVisual + CompositionTarget.Rendering for direct rendering
/// that bypasses WPF's layout system. No InvalidateVisual = no layout cascade.
/// </summary>
public partial class CompactSpectrum : UserControl
{
    private readonly DrawingVisual _visual = new();
    private SpectrumFrame? _frame;
    private DateTime _lastRender = DateTime.MinValue;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000.0 / 60);
    private static readonly Brush BarBrush;

    const int BarCount = 28;

    static CompactSpectrum()
    {
        var b = new SolidColorBrush(Color.FromArgb(210, 92, 184, 255));
        b.Freeze();
        BarBrush = b;
    }

    public CompactSpectrum()
    {
        InitializeComponent();
        AddVisualChild(_visual);
        CompositionTarget.Rendering += OnRenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRenderFrame;
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size MeasureOverride(Size constraint) => constraint;
    protected override Size ArrangeOverride(Size arrangeBounds) => arrangeBounds;

    public void UpdateSpectrum(SpectrumFrame frame)
    {
        _frame = frame;
    }

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastRender < FrameInterval) return;
        _lastRender = now;
        Draw();
    }

    private void Draw()
    {
        SpectrumFrame? frame = _frame;
        if (frame == null || !IsVisible || ActualWidth <= 0 || ActualHeight <= 0) return;

        using var dc = _visual.RenderOpen();

        double width = ActualWidth / BarCount;

        for (int i = 0; i < BarCount; i++)
        {
            int start = i * frame.Magnitudes.Length / BarCount;
            int end = Math.Max(start + 1, (i + 1) * frame.Magnitudes.Length / BarCount);

            double maxMag = double.MinValue;
            for (int j = start; j < end; j++)
                if (frame.Magnitudes[j] > maxMag) maxMag = frame.Magnitudes[j];

            double normalized = Clamp((maxMag + 72) / 72, 0.03, 1);
            double barHeight = normalized * ActualHeight;
            double x = i * width;
            double barWidth = Math.Max(1, width - 1.5);

            dc.DrawRectangle(BarBrush, null, new Rect(x, ActualHeight - barHeight, barWidth, barHeight));
        }
    }

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}
