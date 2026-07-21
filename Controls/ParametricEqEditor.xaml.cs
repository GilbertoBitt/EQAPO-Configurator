using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;
using Microsoft.Win32;

namespace EQAPO_Configurator.Controls;

public partial class ParametricEqEditor : UserControl
{
    private EqProfile _profile = EqProfile.CreateDefault10Band();
    private int _dragSourceIndex = -1;

    public EqProfile Profile => _profile;

    public ParametricEqEditor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadProfile(_profile);
    }

    public void LoadProfile(EqProfile profile)
    {
        _profile = profile;
        BandsPanel.ItemsSource = null;
        BandsPanel.ItemsSource = _profile.Bands;
        PreampSlider.Value = _profile.Preamp;
        PreampValueText.Text = _profile.PreampDisplay;
        UpdateBandCount();
        DrawCurve();
    }

    public void SetPreamp(double value)
    {
        _profile.Preamp = value;
        PreampSlider.Value = value;
        PreampValueText.Text = _profile.PreampDisplay;
    }

    // ── Curve Drawing ──

    private void DrawCurve()
    {
        var canvas = CurveCanvas;
        if (canvas.ActualWidth < 1 || canvas.ActualHeight < 1) return;

        canvas.Children.Clear();
        double w = canvas.ActualWidth;
        double h = canvas.ActualHeight;

        // Draw grid lines
        DrawGrid(canvas, w, h);

        // Generate response curve
        var points = BiquadFilter.GenerateResponseCurve(_profile.Bands, 200);

        if (points.Count < 2) return;

        // Find min/max dB for scaling
        double minDb = -12, maxDb = 12;
        var pathFigure = new System.Windows.Media.PathFigure();
        bool first = true;

        foreach (var (freq, db) in points)
        {
            double x = FrequencyToX(freq, w);
            double y = DbToY(db, h, minDb, maxDb);

            if (first)
            {
                pathFigure.StartPoint = new Point(x, y);
                first = false;
            }
            else
            {
                pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
        }

        // Draw the curve path
        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        var curvePath = new Path
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            StrokeThickness = 2.5,
            Data = pathGeometry,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0, 120, 212),
                BlurRadius = 6,
                Opacity = 0.4,
                ShadowDepth = 0
            }
        };
        canvas.Children.Add(curvePath);

        // Draw zero line
        double zeroY = DbToY(0, h, minDb, maxDb);
        var zeroLine = new Line
        {
            X1 = 0, Y1 = zeroY, X2 = w, Y2 = zeroY,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        canvas.Children.Add(zeroLine);

        // Draw band markers
        foreach (var band in _profile.Bands.Where(b => b.Enabled))
        {
            double freqX = FrequencyToX(band.Frequency, w);
            double bandDb = BiquadFilter.CombinedMagnitudeDb(_profile.Bands, band.Frequency);
            double markerY = DbToY(bandDb, h, minDb, maxDb);

            var marker = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                Stroke = Brushes.White,
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(marker, freqX - 4);
            Canvas.SetTop(marker, markerY - 4);
            canvas.Children.Add(marker);
        }

        // Draw frequency labels at bottom
        double[] freqLabels = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var freq in freqLabels)
        {
            double x = FrequencyToX(freq, w);
            string label = freq >= 1000 ? $"{freq / 1000}k" : $"{freq}";
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
            };
            Canvas.SetLeft(tb, x - 10);
            Canvas.SetTop(tb, h - 16);
            canvas.Children.Add(tb);
        }
    }

    private void DrawGrid(Canvas canvas, double w, double h)
    {
        double minDb = -12, maxDb = 12;

        // Horizontal grid lines at 6 dB intervals
        for (double db = minDb; db <= maxDb; db += 6)
        {
            double y = DbToY(db, h, minDb, maxDb);
            var line = new Line
            {
                X1 = 0, Y1 = y, X2 = w, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128)),
                StrokeThickness = 0.5
            };
            canvas.Children.Add(line);
        }

        // Vertical grid lines at decade frequencies
        double[] vLines = { 50, 100, 200, 500, 1000, 2000, 5000, 10000 };
        foreach (var freq in vLines)
        {
            double x = FrequencyToX(freq, w);
            var line = new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = h,
                Stroke = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
                StrokeThickness = 0.5
            };
            canvas.Children.Add(line);
        }
    }

    private static double FrequencyToX(double freq, double width)
    {
        double logMin = Math.Log10(20);
        double logMax = Math.Log10(20000);
        return (Math.Log10(freq) - logMin) / (logMax - logMin) * width;
    }

    private static double DbToY(double db, double height, double minDb, double maxDb)
    {
        double range = maxDb - minDb;
        return (1.0 - (db - minDb) / range) * height;
    }

    // ── Event Handlers ──

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawCurve();
    }

    private void OnBandSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateBandCount();
        DrawCurve();
    }

    private void OnBandParamChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.NumberBox nb && nb.DataContext is EqBand band)
        {
            double val = nb.Value ?? 0;
            if (nb.Tag?.ToString() == "Freq")
                band.Frequency = val;
            else if (nb.Tag?.ToString() == "Q")
                band.Q = val;
        }
        DrawCurve();
    }

    private void OnBandEnabledChanged(object sender, RoutedEventArgs e)
    {
        UpdateBandCount();
        DrawCurve();
    }

    private void OnPreampChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _profile.Preamp = e.NewValue;
        PreampValueText.Text = _profile.PreampDisplay;
    }

    private void UpdateBandCount()
    {
        if (BandCountText != null)
            BandCountText.Text = $"{_profile.BandCount}/{_profile.Bands.Count}";
    }

    // ── Drag and Drop ──

    private void OnBandDragOver(object sender, DragEventArgs e)
    {
        e.Effects = _dragSourceIndex >= 0 ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnBandDrop(object sender, DragEventArgs e)
    {
        if (sender is Border border && border.DataContext is EqBand targetBand && _dragSourceIndex >= 0)
        {
            int targetIndex = _profile.Bands.IndexOf(targetBand);
            if (targetIndex >= 0 && targetIndex != _dragSourceIndex)
            {
                var sourceBand = _profile.Bands[_dragSourceIndex];
                _profile.Bands.RemoveAt(_dragSourceIndex);
                _profile.Bands.Insert(targetIndex, sourceBand);

                // Re-index
                for (int i = 0; i < _profile.Bands.Count; i++)
                    _profile.Bands[i].Index = i + 1;

                BandsPanel.ItemsSource = null;
                BandsPanel.ItemsSource = _profile.Bands;
                DrawCurve();
            }
        }
        _dragSourceIndex = -1;
    }

    private void OnBandMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is EqBand band)
        {
            _dragSourceIndex = _profile.Bands.IndexOf(band);
            try
            {
                DragDrop.DoDragDrop(border, band, DragDropEffects.Move);
            }
            catch { }
        }
    }

    // ── Import / Export ──

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "EQ Config Files (*.txt)|*.txt|JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import EQ Profile"
        };

        if (dialog.ShowDialog() == true)
        {
            string content = System.IO.File.ReadAllText(dialog.FileName);
            EqProfile? imported = null;

            if (dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                imported = EqProfile.FromJson(content);
            else
                imported = EqProfile.FromEqaPoConfig(content);

            if (imported != null && imported.Bands.Count > 0)
            {
                LoadProfile(imported);
            }
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "EQ Config (*.txt)|*.txt|JSON (*.json)|*.json",
            FileName = $"eq_profile_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            string content;
            if (dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                content = _profile.ToJson();
            else
                content = _profile.ToEqaPoString();

            System.IO.File.WriteAllText(dialog.FileName, content);
        }
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var fresh = EqProfile.CreateDefault10Band();
        fresh.Preamp = _profile.Preamp;
        LoadProfile(fresh);
    }

    // ── Helper ──

    private static T? FindChild<T>(DependencyObject parent, string name) where T : DependencyObject
    {
        // Simple visual tree search
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && typed.GetValue(NameProperty)?.ToString() == name)
                return typed;
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
