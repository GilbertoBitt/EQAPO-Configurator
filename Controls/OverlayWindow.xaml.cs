using System.Windows;
using System.Windows.Interop;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Controls;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;

    public OverlayWindow()
    {
        InitializeComponent();

        var settings = AppSettingsService.Load();
        Spectrum.Visibility = settings.OverlayShowSpectrum ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        IntPtr exStyle = NativeOverlay.GetWindowLong(_hwnd, NativeOverlay.GWL_EXSTYLE);
        exStyle = new IntPtr(
            exStyle.ToInt64()
            | NativeOverlay.WS_EX_TRANSPARENT
            | NativeOverlay.WS_EX_TOOLWINDOW
            | NativeOverlay.WS_EX_NOACTIVATE);

        NativeOverlay.SetWindowLong(_hwnd, NativeOverlay.GWL_EXSTYLE, exStyle);

        NativeOverlay.SetWindowPos(
            _hwnd, NativeOverlay.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeOverlay.SWP_NOMOVE | NativeOverlay.SWP_NOSIZE | NativeOverlay.SWP_NOACTIVATE);
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => Opacity = 1, System.Windows.Threading.DispatcherPriority.Render);
    }

    public void SetProfile(string profileName, string genre, string[] categoryNames)
    {
        ProfileNameText.Text = profileName;
        InfoText.Text = genre;
        BandLevels.SetCategories(categoryNames);
    }

    public void SetStatus(string status, string color = "#00CC66")
    {
        StatusText.Text = status;
        StatusText.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(color)
            as System.Windows.Media.SolidColorBrush ?? System.Windows.Media.Brushes.LimeGreen;
    }

    public void UpdateSpectrum(SpectrumFrame frame, CategoryLevel[] levels)
    {
        Spectrum.UpdateSpectrum(frame);
        BandLevels.UpdateCategoryLevels(levels);
    }

    public void EnsureTopmost()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeOverlay.SetWindowPos(
                _hwnd, NativeOverlay.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeOverlay.SWP_NOMOVE | NativeOverlay.SWP_NOSIZE
                | NativeOverlay.SWP_NOACTIVATE);
        }
    }

    public void PositionAtCorner()
    {
        Left = SystemParameters.PrimaryScreenWidth - Width - 16;
        Top = 16;
    }
}
