using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;
using R3;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

namespace EQAPO_Configurator;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISnackbarService _snackbarService = new SnackbarService();
    private readonly DispatcherTimer _gameDetectionTimer;
    private readonly SpectrumService _spectrumService;
    private readonly CaptureService _captureService;
    private List<GameProfile> _profiles = new();
    private GameProfile? _currentProfile;
    private string? _activeProfileName;
    private DeviceInfo _deviceInfo = new();
    private string? _lastDetectedGame;
    private Guid? _preferredRunningApplication;
    private GameProfile? _profileBeforeAutomaticSwitch;

    public MainWindow()
    {
        InitializeComponent();
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);

        _spectrumService = new SpectrumService();
        _captureService = new CaptureService(_spectrumService, captureDurationMs: 5000);
        _captureService.ClipCaptured += OnClipCaptured;
        _spectrumService.SpectrumUpdated += OnSpectrumUpdated;

        // Auto game detection timer — checks every 3 seconds
        _gameDetectionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _gameDetectionTimer.Tick += OnGameDetectionTick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _gameDetectionTimer.Stop();
        OverlayService.StopSpectrumSubscription();
        _spectrumService?.Stop();
        _spectrumService?.Dispose();
        OverlayService.Hide();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Detect audio device
        _deviceInfo = DeviceDetector.DetectCurrentDevice();
        UpdateDeviceInfo();

        // Load profiles
        _profiles = ProfileManager.LoadAll();
        RefreshProfileList();

        // Start spectrum capture
        if (!_spectrumService.Start())
        {
            ShowSnackbar("Spectrum: WASAPI loopback not available — launch audio to enable");
        }

        // R3 subscription: overlay spectrum via background thread computation
        OverlayService.StartSpectrumSubscription(_spectrumService);

        // Set initial UI state
        ShowProfilesPage();

        // Start auto game detection
        _gameDetectionTimer.Start();
    }

    private void OnSpectrumUpdated(SpectrumFrame frame)
    {
        Dispatcher.BeginInvoke(() =>
        {
            EqEditor?.UpdateSpectrum(frame);
        });
    }

    private void OnClipCaptured(AudioClip clip)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ShowSnackbar($"Clip captured: {clip.Name} ({clip.DurationSeconds:F1}s)");
        });
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
            ExecutableIcon = ExecutableIconService.GetIcon(p.ExecutablePath),
            Profile = p,
            IsActive = p.Name == _activeProfileName,
        }).ToList();

        ProfileList.ItemsSource = displayProfiles;
    }

    private static SymbolRegular GetGenreIcon(GameGenre genre) => genre switch
    {
        GameGenre.FPS => SymbolRegular.TargetArrow24,
        GameGenre.BattleRoyale => SymbolRegular.Globe24,
        GameGenre.RPG => SymbolRegular.Games24,
        GameGenre.Racing => SymbolRegular.VehicleCar24,
        GameGenre.Horror => SymbolRegular.WeatherMoon24,
        GameGenre.MOBA => SymbolRegular.PuzzlePiece24,
        GameGenre.Sports => SymbolRegular.Sport24,
        GameGenre.Action => SymbolRegular.Fire24,
        GameGenre.Stealth => SymbolRegular.Eye24,
        GameGenre.Strategy => SymbolRegular.Board24,
        GameGenre.Music => SymbolRegular.HeadphonesSoundWave24,
        _ => SymbolRegular.Games24,
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
            ExecutableIcon = ExecutableIconService.GetIcon(p.ExecutablePath),
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
        UpdateLayerChain(profile);

        // In-game tips
        InGameTipsText.Text = string.Join(" • ", profile.InGameSettings.Take(4));

        // Sound category sliders — show value next to each slider
        SoundCategoriesPanel.ItemsSource = profile.SoundCategories;
        foreach (var category in profile.SoundCategories)
        {
            category.PropertyChanged -= OnSoundCategoryChanged;
            category.PropertyChanged += OnSoundCategoryChanged;
        }

        // Preamp
        PreampSlider.Value = profile.Preamp;
        PreampValueText.Text = $"{profile.Preamp:+0.0;-0.0;0.0} dB";

        // Active state
        bool isActive = profile.Name == _activeProfileName;
        ActivateBtn.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
        DeactivateBtn.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        ApplyBtn.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLayerChain(GameProfile profile)
    {
        AppSettings settings = AppSettingsService.Load();
        HeadphoneLayerNameText.Text = string.IsNullOrWhiteSpace(settings.HeadphoneLayerName)
            ? "Not selected"
            : settings.HeadphoneLayerName;
        ApplicationLayerNameText.Text = profile.Name;
        bool isActive = profile.Name == _activeProfileName;
        LayerChainStatusText.Text = isActive ? "Active" : "Preview";
        LayerChainStatusText.Foreground = isActive
            ? (Brush)FindResource("SystemAccentBrush")
            : (Brush)FindResource("TextFillColorSecondaryBrush");
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
            newProfile.ExecutablePath = dialog.ExecutablePath;
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
            StatusLabel.Foreground = (Brush)FindResource("SystemAccentBrush");

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

    private void OnApplyProfile(object sender, RoutedEventArgs e)
    {
        if (_currentProfile == null) return;

        try
        {
            SaveEqEditorToProfile();
            ConfigWriter.WriteConfig(_currentProfile);
            ShowSnackbar($"Applied: {_currentProfile.Name}");
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

    private void OnSoundCategoryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SoundCategory.SliderValue) || _currentProfile == null) return;
        _currentProfile.LastModified = DateTime.Now;
        ProfileManager.SaveAll(_profiles);
    }

    private void OnHeadphoneSetup(object sender, RoutedEventArgs e)
    {
        var window = new HeadphoneSetupWindow(_deviceInfo)
        {
            Owner = this
        };
        window.HeadphoneLayerChanged += OnHeadphoneLayerChanged;
        window.ShowDialog();
        window.HeadphoneLayerChanged -= OnHeadphoneLayerChanged;
    }

    private void OnHeadphoneLayerChanged(object? sender, EventArgs e)
    {
        GameProfile? active = _profiles.FirstOrDefault(p => p.Name == _activeProfileName) ?? _currentProfile;
        if (active == null)
        {
            try
            {
                ConfigWriter.WriteHeadphoneBaseOnly();
                ShowSnackbar("Headphone base applied globally. Game fine-tuning will layer on top automatically.");
            }
            catch (Exception ex)
            {
                ShowSnackbar($"Headphone base saved, but could not be applied: {ex.Message}");
            }
            return;
        }

        try
        {
            ConfigWriter.WriteConfig(active);
            if (_currentProfile != null) UpdateLayerChain(_currentProfile);
            ShowSnackbar($"Headphone base updated; reapplied {active.Name} fine-tuning");
        }
        catch (Exception ex)
        {
            ShowSnackbar($"Headphone base saved, but active profile could not be reapplied: {ex.Message}");
        }
    }

    private void OnToggleOverlay(object sender, RoutedEventArgs e)
    {
        OverlayService.IsEnabled = !OverlayService.IsEnabled;
        OverlayToggleBtn.Appearance = OverlayService.IsEnabled
            ? Wpf.Ui.Controls.ControlAppearance.Secondary
            : Wpf.Ui.Controls.ControlAppearance.Transparent;

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
            IReadOnlyList<GameProfile> running = GameDetector.DetectRunningGames(_profiles);

            if (running.Count == 0)
            {
                if (_lastDetectedGame != null)
                {
                    _lastDetectedGame = null;
                    _preferredRunningApplication = null;
                    OverlayService.Hide();
                    GameProfile? fallback = _profiles.FirstOrDefault(p => p.IsGlobalDefault) ?? _profileBeforeAutomaticSwitch;
                    if (fallback != null)
                    {
                        ConfigWriter.WriteConfig(fallback);
                        _activeProfileName = fallback.Name;
                        ActiveProfileLabel.Text = $"Active: {fallback.Name}";
                    }
                    else
                    {
                        ConfigWriter.WriteToPeace();
                        _activeProfileName = null;
                        ActiveProfileLabel.Text = "No active profile";
                    }
                    RefreshProfileList();
                    _profileBeforeAutomaticSwitch = null;
                }
                return;
            }

            GameProfile? runningGame = running.FirstOrDefault(p => p.ApplicationId == _preferredRunningApplication);
            if (runningGame == null && running.Count > 1)
            {
                var dialog = new ApplicationChoiceDialog(running) { Owner = this };
                if (dialog.ShowDialog() != true || dialog.SelectedProfile == null) return;
                runningGame = dialog.SelectedProfile;
            }
            runningGame ??= running[0];
            _preferredRunningApplication = runningGame.ApplicationId;
            if (runningGame.Name == _lastDetectedGame) return;

            _profileBeforeAutomaticSwitch ??= _profiles.FirstOrDefault(p => p.Name == _activeProfileName);
            _lastDetectedGame = runningGame.Name;
            _currentProfile = runningGame;
            ConfigWriter.WriteConfig(runningGame);
            _activeProfileName = runningGame.Name;
            RefreshProfileList();
            ActiveProfileLabel.Text = $"Active: {runningGame.Name}";
            StatusLabel.Text = $"Auto — {runningGame.Name}";
            StatusLabel.Foreground = (Brush)FindResource("SystemAccentBrush");
            ShowSnackbar($"Auto-detected: {runningGame.Name} — profile activated");
            OverlayService.Show(runningGame);
            UpdateLayerChain(runningGame);
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
