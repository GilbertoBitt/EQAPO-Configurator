using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using EQAPO_Configurator.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EQAPO_Configurator;

public partial class HeadphoneSetupWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISnackbarService _snackbarService = new SnackbarService();
    private readonly DeviceInfo _deviceInfo;
    private HeadphoneEqProfile? _downloadedProfile;
    private List<HeadphoneSearchResult> _searchResults = new();

    public HeadphoneSetupWindow(DeviceInfo deviceInfo)
    {
        InitializeComponent();
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        _deviceInfo = deviceInfo;
        LoadDeviceInfo();
        LoadLocalProfiles();
    }

    private void LoadDeviceInfo()
    {
        DetectedDeviceText.Text = _deviceInfo.DeviceName == "Unknown Device"
            ? "No EqualizerAPO device detected — run the Configurator to set one up"
            : _deviceInfo.DeviceName;

        DetectedModeText.Text = _deviceInfo.IsSurroundSound
            ? $"Surround Sound ({_deviceInfo.ChannelCount}.0 channels) — stereo recommended for gaming"
            : "Stereo Headphones — ideal for competitive gaming";
    }

    private void LoadLocalProfiles()
    {
        var profiles = AutoEqService.GetLocalProfiles();
        LocalProfilesPanel.ItemsSource = profiles.Any() ? profiles : new List<string> { "(No saved profiles)" };
    }

    // ── Snackbar ──

    private void ShowSnackbar(string message, int seconds = 3)
    {
        _snackbarService.Show("Headphone Setup", message, ControlAppearance.Secondary, null, TimeSpan.FromSeconds(seconds));
    }

    // ── Search ──

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchBtn.IsEnabled = HeadphoneSearchBox.Text.Trim().Length >= 2;
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e)
    {
        string query = HeadphoneSearchBox.Text.Trim();
        if (query.Length < 2) return;

        SearchLoading.Visibility = Visibility.Visible;
        SearchResultsList.Visibility = Visibility.Collapsed;

        try
        {
            _searchResults = await AutoEqService.SearchHeadphonesAsync(query);

            if (_searchResults.Any())
            {
                SearchResultsList.ItemsSource = _searchResults;
                SearchResultsList.Visibility = Visibility.Visible;
            }
            else
            {
                ShowSnackbar("No results found — try a different search term");
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

    private async void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not HeadphoneSearchResult selected)
            return;

        SearchLoading.Visibility = Visibility.Visible;
        ProfilePreviewPanel.Visibility = Visibility.Collapsed;

        try
        {
            _downloadedProfile = await AutoEqService.DownloadProfileAsync(selected);

            if (_downloadedProfile != null)
            {
                ShowProfilePreview(_downloadedProfile);
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

        ShowSnackbar("Headphone profile ready — it will be used as Layer 1 (correction) when you activate a game profile", 5);
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
