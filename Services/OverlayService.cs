using EQAPO_Configurator.Controls;
using EQAPO_Configurator.Models;
using System.Windows.Threading;

namespace EQAPO_Configurator.Services;

/// <summary>
/// Manages the game overlay window lifecycle.
/// Shows overlay when a game is active, hides when no game is running.
/// </summary>
public static class OverlayService
{
    private static OverlayWindow? _overlay;
    private static DispatcherTimer? _topmostTimer;
    private static bool _enabled = true;

    public static bool IsEnabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) Hide();
        }
    }

    public static bool IsVisible => _overlay?.IsLoaded == true;

    /// <summary>
    /// Show the overlay for a detected game profile.
    /// </summary>
    public static void Show(GameProfile profile)
    {
        if (!_enabled) return;

        if (_overlay == null || !_overlay.IsLoaded)
        {
            _overlay = new OverlayWindow();
            _overlay.PositionAtCorner();
            _overlay.Show();

            // Re-apply topmost every 5 seconds to survive games that reset Z-order
            _topmostTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _topmostTimer.Tick += (_, _) => _overlay?.EnsureTopmost();
            _topmostTimer.Start();
        }

        _overlay.SetProfile(profile.Name, profile.Genre.GetDescription());
        _overlay.SetStatus("EQ ACTIVE", "#00CC66");
    }

    /// <summary>
    /// Hide the overlay.
    /// </summary>
    public static void Hide()
    {
        _topmostTimer?.Stop();
        _topmostTimer = null;

        if (_overlay != null)
        {
            _overlay.Close();
            _overlay = null;
        }
    }

    /// <summary>
    /// Update the overlay status text (e.g., when profile changes).
    /// </summary>
    public static void UpdateStatus(string status, string color)
    {
        _overlay?.SetStatus(status, color);
    }

    /// <summary>
    /// Update the overlay profile info.
    /// </summary>
    public static void UpdateProfile(string name, string genre)
    {
        _overlay?.SetProfile(name, genre);
    }
}
