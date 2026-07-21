using System.Windows;
using System.Windows.Controls;
using EQAPO_Configurator.Models;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace EQAPO_Configurator;

public partial class AddGameDialog : Wpf.Ui.Controls.FluentWindow
{
    private readonly ISnackbarService _snackbarService = new SnackbarService();
    public string GameName { get; private set; } = "";
    public string ExeName { get; private set; } = "";
    public GameGenre SelectedGenre { get; private set; } = GameGenre.FPS;

    private static readonly Dictionary<string, GameGenre> GenreMap = new()
    {
        ["FPS"] = GameGenre.FPS,
        ["BattleRoyale"] = GameGenre.BattleRoyale,
        ["RPG"] = GameGenre.RPG,
        ["Racing"] = GameGenre.Racing,
        ["Horror"] = GameGenre.Horror,
        ["MOBA"] = GameGenre.MOBA,
        ["Sports"] = GameGenre.Sports,
        ["Action"] = GameGenre.Action,
        ["Stealth"] = GameGenre.Stealth,
        ["Strategy"] = GameGenre.Strategy,
        ["Music"] = GameGenre.Music,
    };

    private static readonly Dictionary<string, string> GenreDescriptions = new()
    {
        ["FPS"] = "FPS: Boosts footsteps (2-4kHz), cuts explosion rumble, directional clarity",
        ["BattleRoyale"] = "BR: Footsteps at distance, vehicle detection, ambient awareness",
        ["RPG"] = "RPG: Deep bass for immersion, clear dialogue, wide soundstage, warm",
        ["Racing"] = "Racing: Engine roar (100Hz), tire squeal (2kHz), wind/environmental",
        ["Horror"] = "Horror: Ambient tension, spatial creaks, deep drones, directional threats",
        ["MOBA"] = "MOBA: Ability cues, ping emphasis, announcer clarity, team calls",
        ["Sports"] = "Sports: Ball impact, commentary clarity, crowd atmosphere",
        ["Action"] = "Action: Impact bass, weapon hits, cinematic dialogue, score",
        ["Stealth"] = "Stealth: Enemy footsteps, detection cues, environmental awareness",
        ["Strategy"] = "Strategy: Unit callouts, battle ambience, alert clarity",
        ["Music"] = "Music: Balanced warm curve, sub-bass, vocal clarity, air/sparkle",
    };

    public AddGameDialog()
    {
        InitializeComponent();
        _snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        LoadGenres();
    }

    private void LoadGenres()
    {
        var genres = GenreMap.Select(g => new
        {
            Key = g.Key,
            Label = g.Key switch
            {
                "FPS" => "First-Person Shooter (CoD, CS2, Valorant)",
                "BattleRoyale" => "Battle Royale (Fortnite, Apex, Warzone)",
                "RPG" => "RPG (Witcher, Skyrim, Baldur's Gate)",
                "Racing" => "Racing (Forza, Gran Turismo)",
                "Horror" => "Horror (Silent Hill, RE, Amnesia)",
                "MOBA" => "MOBA (LoL, Dota 2, Smite)",
                "Sports" => "Sports (FIFA, NBA 2K)",
                "Action" => "Action (Devil May Cry, Bayonetta)",
                "Stealth" => "Stealth (Hitman, MGS, Splinter Cell)",
                "Strategy" => "Strategy (StarCraft, Civ, AOE)",
                "Music" => "Music Listening",
                _ => g.Key,
            }
        }).ToList();

        GenrePanel.ItemsSource = genres;
    }

    private void OnGenreChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            if (GenreMap.TryGetValue(tag, out var genre))
                SelectedGenre = genre;

            if (GenreDescriptions.TryGetValue(tag, out var desc))
            {
                GenreDescriptionText.Text = desc;
                GenreDescription.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        GameName = GameNameBox.Text.Trim();
        ExeName = ExeNameBox.Text.Trim();

        if (string.IsNullOrEmpty(GameName) || string.IsNullOrEmpty(ExeName))
        {
            _snackbarService.Show("Add Game", "Please fill in both game name and executable", ControlAppearance.Secondary, null, TimeSpan.FromSeconds(3));
            return;
        }

        if (!ExeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ExeName += ".exe";

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
