using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EQAPO_Configurator.Services;
using EQAPO_Configurator.Models;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EQAPO_Configurator;

public partial class HeadphoneSetupWindow : Wpf.Ui.Controls.FluentWindow
{
    public event EventHandler? HeadphoneLayerChanged;
    private readonly ISnackbarService _snackbarService = new SnackbarService();
    private readonly DeviceInfo _deviceInfo;
    private HeadphoneEqProfile? _downloadedProfile;
    private bool _pythonAvailable;
    private bool _autoeqInstalled;
    private CancellationTokenSource? _searchDebounce;

    public HeadphoneSetupWindow(DeviceInfo deviceInfo)
    {
        InitializeComponent();
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        _deviceInfo = deviceInfo;
        LoadDeviceInfo();
        LoadActiveHeadphoneLayer();
        LoadLocalProfiles();
        _ = DetectPythonAsync();
    }

    private void LoadActiveHeadphoneLayer()
    {
        AppSettings settings = AppSettingsService.Load();
        ActiveHeadphoneLayerText.Text = string.IsNullOrWhiteSpace(settings.HeadphoneLayerName)
            ? "No selected profile"
            : settings.HeadphoneLayerName;
    }

    private async Task DetectPythonAsync()
    {
        var (pythonOk, autoeqOk, path, error) = await Task.Run(() => PythonService.Detect());
        _pythonAvailable = pythonOk;
        _autoeqInstalled = autoeqOk;

        Dispatcher.Invoke(() =>
        {
            if (pythonOk && autoeqOk)
            {
                PythonStatusText.Text = $"Available ({Path.GetFileName(path)})";
                PythonStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SystemAccentBrush");
                PythonErrorText.Visibility = Visibility.Collapsed;
            }
            else if (pythonOk && !autoeqOk)
            {
                PythonStatusText.Text = "Python found, autoeq missing";
                PythonStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                PythonErrorText.Text = error;
                PythonErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                PythonStatusText.Text = "Not available";
                PythonStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                PythonErrorText.Text = error;
                PythonErrorText.Visibility = Visibility.Visible;
            }
        });
    }

    private void LoadDeviceInfo()
    {
        if (_deviceInfo.DeviceName == "NotInstalled")
        {
            DetectedDeviceText.Text = "EqualizerAPO is not installed — download it from sourceforge.net/projects/equalizerapo";
        }
        else if (_deviceInfo.DeviceName == "Unknown Device")
        {
            DetectedDeviceText.Text = "No device configured — run the EqualizerAPO Configurator to set up a device";
        }
        else
        {
            DetectedDeviceText.Text = _deviceInfo.DeviceName;
        }

        if (_deviceInfo.DeviceName == "NotInstalled")
        {
            DetectedModeText.Text = "Install EqualizerAPO to enable EQ functionality";
        }
        else
        {
            DetectedModeText.Text = _deviceInfo.IsSurroundSound
                ? $"Surround Sound ({_deviceInfo.ChannelCount}.0 channels) — stereo recommended for gaming"
                : "Stereo Headphones — ideal for competitive gaming";
        }
    }

    private async void OnOpenDeviceSelector(object sender, RoutedEventArgs e)
    {
        try
        {
            Process? process = EqualizerApoService.OpenDeviceSelector();
            if (process == null)
            {
                ShowSnackbar("EqualizerAPO Device Selector was not found");
                return;
            }

            await process.WaitForExitAsync();
            var refreshed = DeviceDetector.DetectCurrentDevice();
            DetectedDeviceText.Text = DeviceDetector.GetDeviceDisplayName(refreshed);
            ShowSnackbar("Device configuration refreshed");
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Could not open Device Selector: {ex.Message}", 5);
        }
    }

    private void LoadLocalProfiles()
    {
        AppSettings settings = AppSettingsService.Load();
        string activeName = settings.HeadphoneLayerName ?? "";
        var profiles = AutoEqService.GetLocalProfiles();
        var items = profiles.Select(name => new
        {
            Name = name,
            IsActive = string.Equals(name, activeName, StringComparison.OrdinalIgnoreCase)
        }).ToList();
        LocalProfilesPanel.ItemsSource = items.Any() ? items : new[] { new { Name = "(No saved profiles)", IsActive = false } };
    }

    // ── Snackbar ──

    private void ShowSnackbar(string message, int seconds = 3)
    {
        _snackbarService.Show("Headphone Setup", message, ControlAppearance.Secondary, null, TimeSpan.FromSeconds(seconds));
    }

    // ── Search ──

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        string query = HeadphoneSearchBox.Text.Trim();
        SearchBtn.IsEnabled = query.Length >= 2;
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        if (query.Length < 2)
        {
            SearchResultsList.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            await Task.Delay(300, _searchDebounce.Token);
            await SearchAsync(query, _searchDebounce.Token);
        }
        catch (OperationCanceledException) { }
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        string query = HeadphoneSearchBox.Text.Trim();
        if (query.Length < 2) return;

        await SearchAsync(query, CancellationToken.None);
    }

    private async Task SearchAsync(string query, CancellationToken cancellationToken)
    {
        SearchLoading.Visibility = Visibility.Visible;
        try
        {
            Task<List<HeadphoneSearchResult>> downloadTask = AutoEqService.SearchHeadphonesAsync(query);
            Task<List<PythonHeadphoneResult>>? generateTask = _autoeqInstalled
                ? PythonService.ListHeadphonesAsync(query)
                : null;
            await downloadTask;
            if (generateTask != null) await generateTask;
            cancellationToken.ThrowIfCancellationRequested();

            var results = downloadTask.Result.Select(result => new UnifiedHeadphoneSearchResult
            {
                Name = result.Name,
                Detail = $"Existing profile · {result.Type} · {result.MeasurementSource}",
                ActionLabel = "Download",
                Action = HeadphoneProfileAction.Download,
                DownloadResult = result
            }).Concat((generateTask?.Result ?? new()).Select(result => new UnifiedHeadphoneSearchResult
            {
                Name = result.Name,
                Detail = $"Generate with Python · {result.Source}",
                ActionLabel = "Generate",
                Action = HeadphoneProfileAction.Generate
            }))
            .GroupBy(result => (result.Name, result.Action))
            .Select(group => group.First())
            .Take(40)
            .ToList();

            if (results.Count > 0)
            {
                SearchResultsList.ItemsSource = results;
                SearchResultsList.Visibility = Visibility.Visible;
            }
            else
            {
                SearchResultsList.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Search failed: {ex.Message}");
        }
        finally
        {
            SearchLoading.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnUseUnifiedSearchResult(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button { Tag: UnifiedHeadphoneSearchResult selected })
            return;

        SearchLoading.Visibility = Visibility.Visible;
        if (selected.Action == HeadphoneProfileAction.Generate)
            PythonProgress.Visibility = Visibility.Visible;
        ProfilePreviewPanel.Visibility = Visibility.Collapsed;

        try
        {
            _downloadedProfile = selected.Action switch
            {
                HeadphoneProfileAction.Download when selected.DownloadResult != null =>
                    await AutoEqService.DownloadProfileAsync(selected.DownloadResult),
                HeadphoneProfileAction.Generate when _autoeqInstalled =>
                    await PythonService.GenerateProfileAsync(selected.Name),
                _ => null
            };

            if (_downloadedProfile != null)
            {
                ApplyAsBaseLayer(_downloadedProfile);
                ShowProfilePreview(_downloadedProfile);
                ShowSnackbar(selected.Action == HeadphoneProfileAction.Download
                    ? $"Downloaded and applied {selected.Name} as the headphone base"
                    : $"Generated and applied {selected.Name} as the headphone base", 5);
            }
            else
            {
                ShowSnackbar("Failed to download profile — try a different model");
            }
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Download failed: {ex.Message}");
        }
        finally
        {
            SearchLoading.Visibility = Visibility.Collapsed;
            PythonProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowProfilePreview(HeadphoneEqProfile profile)
    {
        ProfileHeadphoneName.Text = profile.HeadphoneName;
        ProfileFilterCount.Text = profile.Filters.Count.ToString();
        ProfilePreamp.Text = $"{profile.Preamp:+0.0;-0.0;0.0} dB";

        // Show first 10 filters as preview
        var previewLines = profile.Filters.Take(10)
            .Select(f => $"Filter {f.FilterIndex}: ON {f.FilterType} Fc {f.CenterFrequency:F0} Hz Gain {f.BaseGain:+0.0;-0.0} dB Q {f.Q:F2}");
        ProfileFiltersPreview.Text = string.Join("\n", previewLines);
        if (profile.Filters.Count > 10)
            ProfileFiltersPreview.Text += $"\n... and {profile.Filters.Count - 10} more filters";

        ProfilePreviewPanel.Visibility = Visibility.Visible;
    }

    // ── Actions ──

    private void OnExportProfile(object sender, RoutedEventArgs e)
    {
        if (_downloadedProfile == null) return;

        try
        {
            string filename = AutoEqService.ExportToEqualizerApo(_downloadedProfile);
            ShowSnackbar($"Exported to EqualizerAPO: {filename}", 4);
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Export failed: {ex.Message}");
        }
    }

    private void OnUseAsBaseLayer(object sender, RoutedEventArgs e)
    {
        if (_downloadedProfile == null) return;

        try
        {
            ApplyAsBaseLayer(_downloadedProfile);
            ShowSnackbar("Headphone correction applied as the active Layer 1 profile", 5);
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Could not activate headphone layer: {ex.Message}", 5);
        }
    }

    private void ApplyAsBaseLayer(HeadphoneEqProfile profile)
    {
        string filename = AutoEqService.ExportToEqualizerApo(profile);
        AppSettings settings = AppSettingsService.Load();
        settings.HeadphoneLayerFilename = filename;
        settings.HeadphoneLayerName = profile.HeadphoneName;
        AppSettingsService.Save(settings);
        ActiveHeadphoneLayerText.Text = profile.HeadphoneName;
        HeadphoneLayerChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteLocalProfile(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string name)
        {
            AutoEqService.DeleteLocalProfile(name);
            LoadLocalProfiles();
            ShowSnackbar($"Deleted: {name}", 2);
        }
    }

    private void OnSelectLocalProfile(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button btn || btn.Tag is not string name)
            return;

        var profile = AutoEqService.LoadLocalProfile(name);
        if (profile == null)
        {
            ShowSnackbar($"Could not load profile: {name}", 4);
            return;
        }

        try
        {
            ApplyAsBaseLayer(profile);
            ShowSnackbar($"Activated '{name}' as the headphone base layer", 5);
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Failed to activate profile: {ex.Message}", 5);
        }
    }

    private void OnOpenAutoEq(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://autoeq.app",
            UseShellExecute = true,
        });
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

}
