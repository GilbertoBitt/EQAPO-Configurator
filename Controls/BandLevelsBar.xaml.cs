using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Controls;

/// <summary>
/// Per-band category volume levels with color-based before/after reference.
/// Uses DrawingVisual + CompositionTarget.Rendering for direct rendering
/// that bypasses WPF's layout system. No InvalidateVisual = no layout cascade.
/// </summary>
public partial class BandLevelsBar : UserControl
{
    private readonly DrawingVisual _visual = new();
    private CategoryLevel[] _levels = [];
    private DateTime _lastRender = DateTime.MinValue;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(1000.0 / 60);

    // Cached formatted text per category (rebuilt only when categories change)
    private FormattedText[] _labelCache = [];
    private string[] _cachedNames = [];

    private static readonly Brush RawBgBrush;
    private static readonly Brush IdleBrush;
    private static readonly Brush QuietBrush;
    private static readonly Brush MidBrush;
    private static readonly Brush LoudBrush;
    private static readonly Brush SeparatorBrush;
    private static readonly Pen SeparatorPen;
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    static BandLevelsBar()
    {
        RawBgBrush = Freeze(new SolidColorBrush(Color.FromArgb(60, 100, 100, 100)));
        IdleBrush = Freeze(new SolidColorBrush(Color.FromArgb(180, 80, 80, 80)));
        QuietBrush = Freeze(new SolidColorBrush(Color.FromArgb(200, 0, 180, 80)));
        MidBrush = Freeze(new SolidColorBrush(Color.FromArgb(200, 220, 180, 0)));
        LoudBrush = Freeze(new SolidColorBrush(Color.FromArgb(200, 255, 70, 50)));
        SeparatorBrush = Freeze(new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)));
        SeparatorPen = new Pen(SeparatorBrush, 0.5);
        SeparatorPen.Freeze();
    }

    private static Brush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

    public BandLevelsBar()
    {
        InitializeComponent();
        AddVisualChild(_visual);
        CompositionTarget.Rendering += OnRenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= OnRenderFrame;
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    // Bypass WPF layout for the DrawingVisual — it manages its own size
    protected override Size MeasureOverride(Size constraint) => constraint;
    protected override Size ArrangeOverride(Size arrangeBounds) => arrangeBounds;

    public void SetCategories(string[] categoryNames)
    {
        _levels = new CategoryLevel[categoryNames.Length];
        for (int i = 0; i < categoryNames.Length; i++)
            _levels[i] = new CategoryLevel(categoryNames[i], -80, -80);

        // Rebuild label cache
        _cachedNames = categoryNames;
        _labelCache = new FormattedText[categoryNames.Length];
        for (int i = 0; i < categoryNames.Length; i++)
        {
            _labelCache[i] = new FormattedText(categoryNames[i],
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 9, Brushes.White, 96);
            _labelCache[i].SetFontWeight(FontWeights.SemiBold);
        }
    }

    public void UpdateCategoryLevels(CategoryLevel[] levels)
    {
        _levels = levels;
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
        if (_levels.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0) return;

        using var dc = _visual.RenderOpen();

        int count = _levels.Length;
        double labelWidth = 72;
        double gap = 8;
        double rightMargin = 8;
        double availWidth = ActualWidth - labelWidth - gap - rightMargin;
        double rowHeight = ActualHeight / count;

        for (int i = 0; i < count; i++)
        {
            var level = _levels[i];
            double y = i * rowHeight;

            double rawNorm = Clamp((level.RawDb + 80) / 80, 0, 1);
            double postNorm = Clamp((level.PostEqDb + 80) / 80, 0, 1);

            double barY = y + 4;
            double barH = rowHeight - 8;

            // Separator line above each row (except first)
            if (i > 0)
                dc.DrawLine(SeparatorPen, new Point(0, y), new Point(ActualWidth, y));

            // Raw level: thin muted background bar (drawn first)
            double rawWidth = Clamp(rawNorm * availWidth, 1, availWidth);
            dc.DrawRectangle(RawBgBrush, null, new Rect(0, barY, rawWidth, barH));

            // Post-EQ level: colored foreground bar (drawn on top)
            double postWidth = Clamp(postNorm * availWidth, 2, availWidth);
            dc.DrawRectangle(LevelToBrush(level.PostEqDb), null, new Rect(0, barY, postWidth, barH));

            // Bar outline (bottom border for visual separation)
            dc.DrawLine(SeparatorPen, new Point(0, barY + barH), new Point(availWidth, barY + barH));

            // Category label (drawn last, right side)
            if (i < _labelCache.Length)
                dc.DrawText(_labelCache[i], new Point(availWidth + gap, y + (rowHeight - _labelCache[i].Height) / 2));
        }
    }

    private static Brush LevelToBrush(double db)
    {
        if (db < -70) return IdleBrush;
        if (db < -40) return QuietBrush;
        if (db < -20) return MidBrush;
        return LoudBrush;
    }

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}
