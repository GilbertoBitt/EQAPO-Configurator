using System.Windows;
using System.Windows.Threading;
using EQAPO_Configurator.Controls;
using EQAPO_Configurator.Models;
using R3;

namespace EQAPO_Configurator.Services;

public record CategoryLevel(string Name, double RawDb, double PostEqDb);

public static class OverlayService
{
    private static OverlayWindow? _overlay;
    private static DispatcherTimer? _topmostTimer;
    private static bool _enabled = true;
    private static GameProfile? _currentProfile;
    private static IDisposable? _spectrumSubscription;

    // Latest computed levels — written on thread pool, read on UI thread
    private static volatile CategoryLevel[] _latestLevels = [];

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

    public static void StartSpectrumSubscription(SpectrumService spectrumService)
    {
        _spectrumSubscription?.Dispose();

        _spectrumSubscription = spectrumService.Frames
            .SubscribeOnThreadPool()
            .Subscribe(frame =>
            {
                if (_currentProfile == null) return;

                // Compute category levels on thread pool — off UI thread
                CategoryLevel[] levels = ComputeCategoryLevels(frame, _currentProfile);

                // Marshal to UI thread for overlay update
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    _latestLevels = levels;
                    _overlay?.UpdateSpectrum(frame, levels);
                });
            });
    }

    public static void StopSpectrumSubscription()
    {
        _spectrumSubscription?.Dispose();
        _spectrumSubscription = null;
    }

    public static CategoryLevel[] GetLatestLevels() => _latestLevels;

    public static void Show(GameProfile profile)
    {
        if (!_enabled) return;

        _currentProfile = profile;

        if (_overlay == null || !_overlay.IsLoaded)
        {
            _overlay = new OverlayWindow();
            _overlay.PositionAtCorner();
            _overlay.Show();

            _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _topmostTimer.Tick += (_, _) => _overlay?.EnsureTopmost();
            _topmostTimer.Start();
        }

        string[] categoryNames = GetCategoryNames(profile);
        _overlay.SetProfile(profile.Name, profile.Genre.GetDescription(), categoryNames);
        _overlay.SetStatus("EQ ACTIVE", "#00CC66");
    }

    public static void Hide()
    {
        _topmostTimer?.Stop();
        _topmostTimer = null;
        _currentProfile = null;

        if (_overlay != null)
        {
            _overlay.Close();
            _overlay = null;
        }
    }

    public static void UpdateStatus(string status, string color)
    {
        _overlay?.SetStatus(status, color);
    }

    public static string[] GetCategoryNames(GameProfile profile)
    {
        if (profile.SoundCategories == null) return [];
        return profile.SoundCategories.Select(c => c.Name).ToArray();
    }

    /// <summary>
    /// Compute per-category raw and post-EQ dB levels.
    /// For categories with multiple filters, uses the max dB across all filters.
    /// </summary>
    public static CategoryLevel[] ComputeCategoryLevels(SpectrumFrame frame, GameProfile profile)
    {
        if (profile.SoundCategories == null || frame.Magnitudes.Length == 0)
            return [];

        var result = new CategoryLevel[profile.SoundCategories.Count];
        double logMin = Math.Log10(20);
        double logMax = Math.Log10(20000);
        double logRange = logMax - logMin;

        for (int c = 0; c < profile.SoundCategories.Count; c++)
        {
            var cat = profile.SoundCategories[c];
            double maxRaw = -80;
            double maxPostEq = -80;

            foreach (var filter in cat.Filters)
            {
                double logTarget = Math.Log10(Math.Max(20, filter.CenterFrequency));
                double normalizedPos = (logTarget - logMin) / logRange;
                int binIndex = Math.Clamp((int)(normalizedPos * (frame.Magnitudes.Length - 1)), 0, frame.Magnitudes.Length - 1);

                double rawDb = frame.Magnitudes[binIndex];
                double gain = filter.BaseGain + filter.UserOffset;
                double postEq = Math.Clamp(rawDb + gain, -80, 0);

                if (rawDb > maxRaw) maxRaw = rawDb;
                if (postEq > maxPostEq) maxPostEq = postEq;
            }

            result[c] = new CategoryLevel(cat.Name, maxRaw, maxPostEq);
        }

        return result;
    }
}
