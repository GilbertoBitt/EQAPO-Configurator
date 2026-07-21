using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

namespace EQAPO_Configurator;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISnackbarService _snackbarService = new SnackbarService();
    private readonly DispatcherTimer _gameDetectionTimer;
    private List<GameProfile> _profiles = new();
    private GameProfile? _currentProfile;
    private string? _activeProfileName;
    private DeviceInfo _deviceInfo = new();
    private string? _lastDetectedGame;

    public MainWindow()
    {
        InitializeComponent();
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        // Auto game detection timer — checks every 3 seconds
        _gameDetectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _gameDetectionTimer.Tick += OnGameDetectionTick;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Detect audio device
        _deviceInfo = DeviceDetector.DetectCurrentDevice();
        UpdateDeviceInfo();

        // Load profiles
        _profiles = ProfileManager.LoadAll();
        RefreshProfileList();

        // Set initial UI state
        ShowProfilesPage();

        // Start auto game detection
        _gameDetectionTimer.Start();
    }

    // ── Navigation ──

    private void ShowProfilesPage()
    {
        ProfilesPage.Visibility = Visibility.Visible;
        EqualizerPage.Visibility = Visibility.Collapsed;
        DevicePage.Visibility = Visibility.Collapsed;
        UpdateNavButtons("Profiles");
    }

    private void ShowEqualizerPage()
    {
        ProfilesPage.Visibility = Visibility.Collapsed;
        EqualizerPage.Visibility = Visibility.Visible;
        DevicePage.Visibility = Visibility.Collapsed;
        UpdateNavButtons("Equalizer");

        // Load the current profile's EQ into the editor
        if (_currentProfile != null)
        {
            var eqProfile = BuildEqProfileFromGameProfile(_currentProfile);
            EqEditor.LoadProfile(eqProfile);
            EqProfileNameText.Text = $"— {_currentProfile.Name}";
        }
    }

    private void ShowDevicePage()
    {
        ProfilesPage.Visibility = Visibility.Collapsed;
        EqualizerPage.Visibility = Visibility.Collapsed;
        DevicePage.Visibility = Visibility.Visible;
        UpdateNavButtons("Device");
    }

    private void UpdateNavButtons(string activePage)
    {
        NavProfiles.Appearance = activePage == "Profiles" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Transparent;
        NavEqualizer.Appearance = activePage == "Equalizer" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Transparent;
        NavDevice.Appearance = activePage == "Device" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Transparent;
    }

    private void OnNavProfiles(object sender, RoutedEventArgs e) => ShowProfilesPage();
    private void OnNavEqualizer(object sender, RoutedEventArgs e) => ShowEqualizerPage();
    private void OnNavDevice(object sender, RoutedEventArgs e) => ShowDevicePage();

    // ── Profile List ──

    private void RefreshProfileList()
    {
        ProfileList.ItemsSource = null;

        var displayProfiles = _profiles.Select(p => new
        {
            p.Name,
            p.GameExe,
            GenreDisplay = p.Genre.GetDescription(),
            IconSymbol = GetGenreIcon(p.Genre),
            Profile = p,
            IsActive = p.Name == _activeProfileName,
        }).ToList();

        ProfileList.ItemsSource = displayProfiles;
    }

    private string GetGenreIcon(GameGenre genre) => genre switch
    {
        GameGenre.FPS => "TargetArrow24",
        GameGenre.BattleRoyale => "Globe24",
        GameGenre.RPG => "Games24",
        GameGenre.Racing => "CarProfile24",
        GameGenre.Horror => "WeatherMoon24",
        GameGenre.MOBA => "Puzzle24",
        GameGenre.Sports => "Sports24",
        GameGenre.Action => "Sword24",
        GameGenre.Stealth => "Eye24",
        GameGenre.Strategy => "Board24",
        GameGenre.Music => "MusicNote24",
        _ => "Game24",
    };

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(query))
        {
            RefreshProfileList();
            return;
        }

        var filtered = _profiles.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Genre.GetDescription().Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ProfileList.ItemsSource = filtered.Select(p => new
        {
            p.Name,
            p.GameExe,
            GenreDisplay = p.Genre.GetDescription(),
            IconSymbol = GetGenreIcon(p.Genre),
            Profile = p,
            IsActive = p.Name == _activeProfileName,
        }).ToList();
    }

    private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is not null)
        {
            var selectedItem = ProfileList.SelectedItem;
            var profileProp = selectedItem.GetType().GetProperty("Profile");
            if (profileProp?.GetValue(selectedItem) is GameProfile profile)
            {
                _currentProfile = profile;
                ShowProfileDetail(profile);
            }
        }
    }

    private void ShowProfileDetail(GameProfile profile)
    {
        NoSelectionPanel.Visibility = Visibility.Collapsed;
        ProfileDetailPanel.Visibility = Visibility.Visible;

        ProfileTitle.Text = profile.Name;
        ProfileSubtitle.Text = $"{profile.Genre.GetDescription()} — {profile.GameExe}";

        // In-game tips
        InGameTipsText.Text = string.Join(" • ", profile.InGameSettings.Take(4));

        // Sound category sliders — show value next to each slider
        SoundCategoriesPanel.ItemsSource = profile.SoundCategories;

        // Preamp
        PreampSlider.Value = profile.Preamp;
        PreampValueText.Text = $"{profile.Preamp:+0.0;-0.0;0.0} dB";

        // Active state
        bool isActive = profile.Name == _activeProfileName;
        ActivateBtn.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
        DeactivateBtn.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Build EqProfile from GameProfile ──

    private EqProfile BuildEqProfileFromGameProfile(GameProfile gameProfile)
    {
        var eq = EqProfile.CreateDefault10Band();
        eq.Name = gameProfile.Name;

        // Map sound category slider values to EQ bands
        if (gameProfile.SoundCategories?.Count > 0)
        {
            var bands = new List<EqBand>();
            int idx = 1;
            foreach (var cat in gameProfile.SoundCategories)
            {
                foreach (var filter in cat.Filters)
                {
                    bands.Add(new EqBand
                    {
                        Index = idx++,
                        FilterType = filter.FilterType == "LSC" ? EqFilterType.LSC
                            : filter.FilterType == "HSC" ? EqFilterType.HSC
                            : EqFilterType.PK,
                        Frequency = filter.CenterFrequency,
                        Gain = filter.BaseGain + filter.UserOffset,
                        Q = filter.Q,
                        Enabled = true
                    });
                }
            }
            if (bands.Count > 0)
            {
                // Pad to 10 bands or trim
                while (bands.Count < 10)
                    bands.Add(new EqBand { Index = bands.Count + 1, Frequency = 1000 });
                eq.Bands = bands.Take(10).ToList();
            }
        }

        eq.Preamp = gameProfile.Preamp;
        return eq;
    }

    // ── Actions ──

    private void OnAddGame(object sender, RoutedEventArgs e)
    {
        var dialog = new AddGameDialog();
        if (dialog.ShowDialog() == true)
        {
            var newProfile = GameProfile.CreateDefault(dialog.GameName, dialog.ExeName, dialog.SelectedGenre);
            _profiles.Add(newProfile);
            ProfileManager.SaveAll(_profiles);
            RefreshProfileList();
            ShowSnackbar($"Added: {newProfile.Name}");
        }
    }

    private void OnActivateProfile(object sender, RoutedEventArgs e)
    {
        if (_currentProfile == null) return;

        try
        {
            // Save any EQ editor changes back to the profile
            SaveEqEditorToProfile();

            ConfigWriter.WriteConfig(_currentProfile);
            _activeProfileName = _currentProfile.Name;
            RefreshProfileList();
            ShowProfileDetail(_currentProfile);

            ActiveProfileLabel.Text = $"Active: {_currentProfile.Name}";
            StatusLabel.Text = $"Active — {_currentProfile.Name}";
            StatusLabel.Foreground = (Brush)FindResource("SystemAccentColor");

            // Show overlay for manually activated profile
            OverlayService.Show(_currentProfile);

            ShowSnackbar($"Activated: {_currentProfile.Name}");
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Error: {ex.Message}");
        }
    }

    private void OnDeactivateProfile(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigWriter.WriteToPeace();
            _activeProfileName = null;
            RefreshProfileList();
            if (_currentProfile != null)
                ShowProfileDetail(_currentProfile);

            ActiveProfileLabel.Text = "";
            StatusLabel.Text = "Ready";
            StatusLabel.Foreground = (Brush)FindResource("TextFillColorSecondaryBrush");

            ShowSnackbar("Deactivated — using Peace GUI");
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Error: {ex.Message}");
        }
    }

    private void OnDeleteProfile(object sender, RoutedEventArgs e)
    {
        if (_currentProfile == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Delete profile '{_currentProfile.Name}'?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            _profiles.Remove(_currentProfile);
            ProfileManager.SaveAll(_profiles);

            if (_activeProfileName == _currentProfile.Name)
                _activeProfileName = null;

            _currentProfile = null;
            NoSelectionPanel.Visibility = Visibility.Visible;
            ProfileDetailPanel.Visibility = Visibility.Collapsed;
            RefreshProfileList();

            ShowSnackbar("Profile deleted");
        }
    }

    private void OnPreampChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_currentProfile != null && PreampValueText != null)
        {
            _currentProfile.Preamp = e.NewValue;
            PreampValueText.Text = $"{e.NewValue:+0.0;-0.0;0.0} dB";
        }
    }

    private void OnHeadphoneSetup(object sender, RoutedEventArgs e)
    {
        var window = new HeadphoneSetupWindow(_deviceInfo)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OnToggleOverlay(object sender, RoutedEventArgs e)
    {
        OverlayService.IsEnabled = !OverlayService.IsEnabled;
        OverlayToggleBtn.Appearance = OverlayService.IsEnabled
            ? Wpf.Ui.Controls.ControlAppearance.Transparent
            : Wpf.Ui.Controls.ControlAppearance.Secondary;

        if (OverlayService.IsEnabled && _currentProfile != null)
            OverlayService.Show(_currentProfile);
        else
            OverlayService.Hide();

        ShowSnackbar(OverlayService.IsEnabled ? "Overlay enabled" : "Overlay disabled");
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        ShowSnackbar("Settings coming soon");
    }

    // ── Save EQ Editor Changes ──

    private void SaveEqEditorToProfile()
    {
        if (_currentProfile == null || EqEditor?.Profile == null) return;

        var eq = EqEditor.Profile;
        _currentProfile.Preamp = eq.Preamp;

        // Map EQ bands back to sound category filters
        int bandIdx = 0;
        foreach (var cat in _currentProfile.SoundCategories)
        {
            foreach (var filter in cat.Filters)
            {
                if (bandIdx < eq.Bands.Count)
                {
                    var band = eq.Bands[bandIdx];
                    filter.UserOffset = band.Gain - filter.BaseGain;
                    bandIdx++;
                }
            }
        }
    }

    // ── Auto Game Detection ──

    private void OnGameDetectionTick(object? sender, EventArgs e)
    {
        try
        {
            var runningGame = GameDetector.DetectRunningGame(_profiles);

            if (runningGame != null && runningGame.Name != _lastDetectedGame)
            {
                _lastDetectedGame = runningGame.Name;

                // Auto-activate the detected game profile
                if (_activeProfileName != runningGame.Name)
                {
                    _currentProfile = runningGame;
                    ConfigWriter.WriteConfig(runningGame);
                    _activeProfileName = runningGame.Name;
                    RefreshProfileList();

                    ActiveProfileLabel.Text = $"Active: {runningGame.Name}";
                    StatusLabel.Text = $"Auto — {runningGame.Name}";
                    StatusLabel.Foreground = (Brush)FindResource("SystemAccentColor");

                    ShowSnackbar($"Auto-detected: {runningGame.Name} — profile activated");
                }

                // Show overlay
                OverlayService.Show(runningGame);
            }
            else if (runningGame == null && _lastDetectedGame != null)
            {
                _lastDetectedGame = null;
                OverlayService.Hide();
            }
        }
        catch
        {
            // Silently ignore detection errors
        }
    }

    // ── Device Info ──

    private void UpdateDeviceInfo()
    {
        DeviceNameText.Text = DeviceDetector.GetDeviceDisplayName(_deviceInfo);
        AudioModeText.Text = _deviceInfo.IsSurroundSound
            ? $"Surround Sound ({_deviceInfo.ChannelCount}.0 channels)"
            : "Stereo Headphones";

        var tips = DeviceDetector.GetSurroundTips(_deviceInfo);
        DeviceTipsPanel.ItemsSource = tips;
    }

    // ── Snackbar ──

    private void ShowSnackbar(string message)
    {
        _snackbarService.Show("EQAPO Configurator", message, ControlAppearance.Secondary, null, TimeSpan.FromSeconds(3));
    }
}
