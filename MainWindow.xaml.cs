using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EQAPO_Configurator.Models;
using EQAPO_Configurator.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Appearance;

namespace EQAPO_Configurator;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISnackbarService _snackbarService = new SnackbarService();
    private List<GameProfile> _profiles = new();
    private GameProfile? _currentProfile;
    private string? _activeProfileName;
    private DeviceInfo _deviceInfo = new();

    public MainWindow()
    {
        InitializeComponent();
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
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
            // Get the profile from the anonymous type
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

        // Sound category sliders
        SoundCategoriesPanel.ItemsSource = profile.SoundCategories;

        // Preamp
        PreampSlider.Value = profile.Preamp;
        PreampValueText.Text = $"{profile.Preamp:+0.0;-0.0;0.0} dB";

        // Active state
        bool isActive = profile.Name == _activeProfileName;
        ActivateBtn.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
        DeactivateBtn.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
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
            ConfigWriter.WriteConfig(_currentProfile);
            _activeProfileName = _currentProfile.Name;
            RefreshProfileList();
            ShowProfileDetail(_currentProfile);

            ActiveProfileLabel.Text = $"Active: {_currentProfile.Name}";
            StatusLabel.Text = $"Active — {_currentProfile.Name}";
            StatusLabel.Foreground = (Brush)FindResource("SystemAccentColor");

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

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        ShowSnackbar("Settings coming soon");
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
