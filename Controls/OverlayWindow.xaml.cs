using System.Windows;
using System.Windows.Interop;
using EQAPO_Configurator.Services;

namespace EQAPO_Configurator.Controls;

public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // Apply extended window styles for click-through, no Alt+Tab, no focus steal
        IntPtr exStyle = NativeOverlay.GetWindowLong(_hwnd, NativeOverlay.GWL_EXSTYLE);
        exStyle = new IntPtr(
            exStyle.ToInt64()
            | NativeOverlay.WS_EX_TRANSPARENT
            | NativeOverlay.WS_EX_TOOLWINDOW
            | NativeOverlay.WS_EX_NOACTIVATE
            | NativeOverlay.WS_EX_NOREDIRECTIONBITMAP);

        NativeOverlay.SetWindowLong(_hwnd, NativeOverlay.GWL_EXSTYLE, exStyle);

        // Ensure topmost
        NativeOverlay.SetWindowPos(
            _hwnd, NativeOverlay.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeOverlay.SWP_NOMOVE | NativeOverlay.SWP_NOSIZE
            | NativeOverlay.SWP_NOACTIVATE | NativeOverlay.SWP_SHOWWINDOW);

        // DWM "sheet of glass" for smooth compositing
        var margins = new NativeOverlay.MARGINS
        {
            cxLeftWidth = -1, cxRightWidth = -1,
            cyTopHeight = -1, cyBottomHeight = -1
        };
        NativeOverlay.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

        // Disable non-client rendering
        int policy = 2;
        NativeOverlay.DwmSetWindowAttribute(_hwnd, NativeOverlay.DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));
    }

    public void SetProfile(string profileName, string genre)
    {
        ProfileNameText.Text = profileName;
        InfoText.Text = genre;
    }

    public void SetStatus(string status, string color = "#00CC66")
    {
        StatusText.Text = status;
        StatusText.Foreground = new System.Windows.Media.BrushConverter().ConvertFromString(color)
            as System.Windows.Media.SolidColorBrush ?? System.Windows.Media.Brushes.LimeGreen;
    }

    /// <summary>
    /// Re-apply topmost to survive games that reset Z-order.
    /// </summary>
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

    public void PositionAtCorner(int screenIndex = 0)
    {
        var screen = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;

        // Top-right corner with 16px margin
        Left = SystemParameters.PrimaryScreenWidth - Width - 16;
        Top = 16;
    }
}
