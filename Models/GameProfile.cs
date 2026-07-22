using System.IO;

namespace EQAPO_Configurator.Models;

public class GameProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string GameExe { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public bool IsDefaultForApplication { get; set; } = true;
    public bool IsGlobalDefault { get; set; }
    public string Description { get; set; } = "";
    public string ConfigFileName { get; set; } = "";
    public GameGenre Genre { get; set; } = GameGenre.FPS;
    public double Preamp { get; set; } = -6.0;
    public List<SoundCategory> SoundCategories { get; set; } = new();
    public List<string> InGameSettings { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.Now;

    public static GameProfile CreateDefault(string name, string exe, GameGenre genre, string configFile = "")
    {
        return new GameProfile
        {
            Name = name,
            GameExe = exe,
            ConfigFileName = configFile,
            Description = genre.GetDescription(),
            Genre = genre,
            Preamp = GetGenrePreamp(genre),
            SoundCategories = GetGenreCategories(genre),
            InGameSettings = GetGameInGameTips(name, genre),
        };
    }

    public string ExecutableName => Path.GetFileName(
        string.IsNullOrWhiteSpace(ExecutablePath) ? GameExe : ExecutablePath);

    private static double GetGenrePreamp(GameGenre genre) => genre switch
    {
        GameGenre.FPS => -6.0,
        GameGenre.BattleRoyale => -5.0,
        GameGenre.RPG => -4.0,
        GameGenre.Racing => -5.0,
        GameGenre.Horror => -4.0,
        GameGenre.MOBA => -5.0,
        GameGenre.Sports => -4.0,
        GameGenre.Action => -4.0,
        GameGenre.Stealth => -5.0,
        GameGenre.Strategy => -4.0,
        GameGenre.Music => -4.0,
        _ => -5.0,
    };

    private static List<string> GetGameInGameTips(string name, GameGenre genre)
    {
        string lower = name.ToLowerInvariant();

        if (lower.Contains("warzone") || lower.Contains("bo7") || lower.Contains("black ops"))
            return new()
            {
                "Audio Mix: Sucker Punch (best for footsteps)",
                "Music Volume: 0 (all music off)",
                "Dialogue Volume: 0-20",
                "Effects Volume: 100",
                "Mono Audio: Off",
                "Reduce Tinnitus Sound: On",
                "Enhanced Headphone Mode: Test On/Off",
                "Speaker Output: Stereo",
                "Enable Loudness Equalization in Windows",
            };

        if (lower.Contains("counter-strike") || lower.Contains("cs2"))
            return new()
            {
                "EQ Profile: Crisp (in-game)",
                "Master Volume: 65-80%",
                "L/R Isolation: 50-60%",
                "3D Audio Processing: 40-50%",
                "Enable HRTF in-game",
                "Console: snd_mix_async 1",
                "Console: snd_headphone_pan_exponent 2",
                "Disable Windows Sonic / Dolby",
                "Set output: 24-bit, 48000 Hz",
            };

        if (lower.Contains("valorant"))
            return new()
            {
                "HRTF: ON (built-in spatial audio)",
                "Speaker Config: Stereo",
                "Sound FX Volume: 100%",
                "Voice Volume: 50-60%",
                "All Music: 0%",
                "Disable Windows Spatial Sound",
                "Set output: 24-bit, 48000 Hz",
                "Enable exclusive mode in Windows",
            };

        if (lower.Contains("fortnite"))
            return new()
            {
                "Main Volume: 100%",
                "Music Volume: 0%",
                "Sound Effects: 100%",
                "Dialogue: 0-20%",
                "Voice Chat: 20-40%",
                "Sound Quality: High",
                "3D Headphones: Enabled (if stereo)",
                "Visualize Sound Effects: Optional",
                "Cinematics: 0%",
            };

        if (lower.Contains("apex"))
            return new()
            {
                "Sound Effects Volume: 75-85%",
                "Dialogue Volume: 50%",
                "Lobby Music: 0%",
                "3D Audio: Off (if using EQ)",
                "Enable Windows Sonic if no external EQ",
                "Master Volume: 70-80%",
                "Voice Chat: Adjust to preference",
            };

        if (lower.Contains("rainbow") || lower.Contains("siege") || lower.Contains("r6"))
            return new()
            {
                "Audio Preset: Night Mode (compresses dynamic range)",
                "Master Volume: 50-70%",
                "Music Volume: 0%",
                "Voice Chat: 30-50%",
                "HRTF: Enabled",
                "Night Mode makes footsteps louder",
                "Disable virtual surround",
            };

        if (lower.Contains("pubg") || lower.Contains("battlegrounds"))
            return new()
            {
                "Master Volume: 70-80%",
                "Sound Effects: 100%",
                "Music: Off",
                "Voice Chat: 30-50%",
                "Enable HRTF or Windows Sonic",
                "Footsteps boosted by EQ — test in training",
                "Disable any bass boost in headset software",
            };

        // Fall back to genre defaults
        return GetGenreInGameTips(genre);
    }

    private static List<string> GetGenreInGameTips(GameGenre genre) => genre switch
    {
        GameGenre.FPS => new()
        {
            "Audio Mix: Sucker Punch or Treyarch Mix",
            "Music Volume: 0 (cut all music)",
            "Dialogue Volume: 20-40",
            "Effects Volume: 100",
            "Mono Audio: Off",
            "Reduce Tinnitus Sound: On",
            "Enhanced Headphone Mode: Test On/Off",
            "Speaker Output: Stereo",
        },
        GameGenre.BattleRoyale => new()
        {
            "Audio Preset: Headphone Bass Boost",
            "Music Volume: 0",
            "Effects Volume: 100",
            "Dialogue Volume: 0-20",
            "Voice Chat: 20-40",
            "Visualize Sound Effects: Optional (Fortnite)",
            "Speaker Output: Stereo",
            "3D Audio: On (if no external EQ)",
        },
        GameGenre.RPG => new()
        {
            "Use headphone/surround mode",
            "Music Volume: 60-80",
            "Dialogue Volume: 80-100",
            "Effects Volume: 80-100",
            "Enable spatial audio for immersion",
        },
        GameGenre.Racing => new()
        {
            "Use headphones for positional audio",
            "Music Volume: 30-50",
            "Engine Volume: 80-100",
            "Effects Volume: 80-100",
        },
        GameGenre.Horror => new()
        {
            "Use headphones — critical for atmosphere",
            "Music Volume: 40-60",
            "Effects Volume: 100",
            "Dialogue Volume: 60-80",
            "Keep lights off for best experience",
        },
        GameGenre.MOBA => new()
        {
            "Use stereo headphones",
            "Music Volume: 20-40",
            "Announcer Volume: 80-100",
            "Ping Volume: 100",
            "Effects Volume: 80-100",
        },
        _ => new()
        {
            "Use stereo headphones for best results",
            "Balance music/effects to preference",
        },
    };

    public static List<SoundCategory> GetGenreCategories(GameGenre genre) => genre switch
    {
        GameGenre.FPS => GetFPSDefaults(),
        GameGenre.BattleRoyale => GetBattleRoyaleDefaults(),
        GameGenre.RPG => GetRPGDefaults(),
        GameGenre.Racing => GetRacingDefaults(),
        GameGenre.Horror => GetHorrorDefaults(),
        GameGenre.MOBA => GetMOBADefaults(),
        GameGenre.Sports => GetSportsDefaults(),
        GameGenre.Action => GetActionDefaults(),
        GameGenre.Stealth => GetStealthDefaults(),
        GameGenre.Strategy => GetStrategyDefaults(),
        GameGenre.Music => GetMusicDefaults(),
        _ => GetFPSDefaults(),
    };

    // ── FPS: Footsteps are king ──
    private static List<SoundCategory> GetFPSDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Explosions & Rumble",
            Description = "Vehicle engines, explosions, killstreaks, environmental bass",
            Icon = "💥",
            SliderValue = -2,
            FrequencyRange = "60 - 200 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = -2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Body & Impact",
            Description = "Close-range footsteps body weight, thuds, heavy impacts",
            Icon = "👣",
            SliderValue = -3,
            FrequencyRange = "200 - 500 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = -3.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 13, CenterFrequency = 400, Q = 1.50, FilterType = "PK", BaseGain = -2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Footsteps",
            Description = "Boots on concrete, gravel, metal. THE most critical FPS frequency.",
            Icon = "👟",
            SliderValue = 4,
            FrequencyRange = "1 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 3.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Weapons & Reloading",
            Description = "Gun handling, reload clicks, weapon switching",
            Icon = "🔫",
            SliderValue = 3,
            FrequencyRange = "3 - 6 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 17, CenterFrequency = 5000, Q = 4.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Voice & Callouts",
            Description = "Enemy callouts, teammate communication, announcer",
            Icon = "🗣️",
            SliderValue = 2,
            FrequencyRange = "2 - 5 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 3.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Directional Awareness",
            Description = "Spatial positioning, vertical audio, above/below enemies",
            Icon = "📍",
            SliderValue = 2,
            FrequencyRange = "6 - 10 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Environment & Detail",
            Description = "Ambient sounds, shell casings, distant movement",
            Icon = "🌿",
            SliderValue = 1,
            FrequencyRange = "8 - 14 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 20, CenterFrequency = 14000, Q = 0.70, FilterType = "HSC", BaseGain = -1.0, UserOffset = 0 }
            }
        },
    };

    // ── Battle Royale: Distance footsteps + vehicle awareness ──
    private static List<SoundCategory> GetBattleRoyaleDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Vehicles & Rumble",
            Description = "Vehicle engines, gliders, explosions at distance",
            Icon = "🚗",
            SliderValue = -1,
            FrequencyRange = "60 - 200 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = -1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Body & Weight",
            Description = "Close-range footstep weight, building sounds",
            Icon = "👣",
            SliderValue = -2,
            FrequencyRange = "200 - 500 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = -2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 13, CenterFrequency = 400, Q = 1.50, FilterType = "PK", BaseGain = -1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Footsteps (Near)",
            Description = "Close enemy footsteps — immediate threat detection",
            Icon = "👟",
            SliderValue = 4,
            FrequencyRange = "1 - 3 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 3.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Footsteps (Distant)",
            Description = "Far enemy movement — early warning system",
            Icon = "🔜",
            SliderValue = 3,
            FrequencyRange = "3 - 5 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 17, CenterFrequency = 5000, Q = 4.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Loot & Interaction",
            Description = "Item pickup sounds, chest opening, door creaks",
            Icon = "📦",
            SliderValue = 2,
            FrequencyRange = "4 - 7 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 17, CenterFrequency = 5000, Q = 4.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Directional Awareness",
            Description = "360° positioning, vertical audio, storm/ring cues",
            Icon = "📍",
            SliderValue = 2,
            FrequencyRange = "6 - 10 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Ambient & Environment",
            Description = "Wind, water, ambient world sounds, storm proximity",
            Icon = "🌿",
            SliderValue = 0,
            FrequencyRange = "8 - 14 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 20, CenterFrequency = 14000, Q = 0.70, FilterType = "HSC", BaseGain = -0.5, UserOffset = 0 }
            }
        },
    };

    // ── RPG: Immersive, wide, ambient ──
    private static List<SoundCategory> GetRPGDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Bass & Rumble",
            Description = "Dragon roars, thunder, environmental rumble, music bass",
            Icon = "🐉",
            SliderValue = 1,
            FrequencyRange = "40 - 200 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Body & Presence",
            Description = "Character footsteps, armor clanking, weapon impacts",
            Icon = "⚔️",
            SliderValue = 0,
            FrequencyRange = "200 - 800 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 13, CenterFrequency = 400, Q = 1.50, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Dialogue & Voice",
            Description = "NPC conversations, quest givers, narrators",
            Icon = "🗣️",
            SliderValue = 2,
            FrequencyRange = "1 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Ambient World",
            Description = "Wind, birds, rain, tavern chatter, forest ambience",
            Icon = "🌲",
            SliderValue = 2,
            FrequencyRange = "4 - 10 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 17, CenterFrequency = 5000, Q = 4.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Music & Score",
            Description = "Orchestral score, battle music, exploration themes",
            Icon = "🎵",
            SliderValue = 1,
            FrequencyRange = "100 - 16 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 20, CenterFrequency = 14000, Q = 0.70, FilterType = "HSC", BaseGain = 0.5, UserOffset = 0 }
            }
        },
    };

    // ── Racing: Engine, tires, environment ──
    private static List<SoundCategory> GetRacingDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Engine & Exhaust",
            Description = "Engine roar, turbo whistle, exhaust pops, backfire",
            Icon = "🏎️",
            SliderValue = 2,
            FrequencyRange = "60 - 300 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Tire & Surface",
            Description = "Tire squeal, gravel spray, curb strikes, grip feedback",
            Icon = "🛞",
            SliderValue = 2,
            FrequencyRange = "500 - 3 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Transmission & Shift",
            Description = "Gear shifts, clutch, transmission whine",
            Icon = "⚙️",
            SliderValue = 1,
            FrequencyRange = "2 - 6 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Environmental",
            Description = "Wind rush, crowd, track ambiance, weather effects",
            Icon = "🌬️",
            SliderValue = 0,
            FrequencyRange = "6 - 14 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 }
            }
        },
    };

    // ── Horror: Ambient tension, spatial creaks ──
    private static List<SoundCategory> GetHorrorDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Deep Rumble",
            Description = "Earthquake, thunder, monster footsteps, low drones",
            Icon = "🌑",
            SliderValue = 2,
            FrequencyRange = "30 - 150 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Footsteps & Movement",
            Description = "Your own footsteps, enemy approach, floor creaks",
            Icon = "👟",
            SliderValue = 2,
            FrequencyRange = "200 - 2 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Ambient Tension",
            Description = "Wind howl, distant screams, building groans, whispers",
            Icon = "👻",
            SliderValue = 3,
            FrequencyRange = "1 - 6 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Spatial Detail",
            Description = "Directional threats, behind-you cues, room acoustics",
            Icon = "👂",
            SliderValue = 2,
            FrequencyRange = "6 - 12 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "High Detail",
            Description = "Dripping water, ticking clocks, subtle audio cues",
            Icon = "💧",
            SliderValue = 1,
            FrequencyRange = "10 - 16 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 20, CenterFrequency = 14000, Q = 0.70, FilterType = "HSC", BaseGain = 0.5, UserOffset = 0 }
            }
        },
    };

    // ── MOBA: Ability cues, pings, team ──
    private static List<SoundCategory> GetMOBADefaults() => new()
    {
        new SoundCategory
        {
            Name = "Ability Cues",
            Description = "Skill shots, ultimates, summoner spells, passive procs",
            Icon = "✨",
            SliderValue = 3,
            FrequencyRange = "500 - 3 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Pings & Alerts",
            Description = "Danger pings, missing calls, objective alerts",
            Icon = "🔔",
            SliderValue = 4,
            FrequencyRange = "2 - 5 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Announcer & Voice",
            Description = "Double kill, ace, tower destroyed, champion select",
            Icon = "🎙️",
            SliderValue = 2,
            FrequencyRange = "1 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Minion & Combat",
            Description = "Minion attacks, tower shots, jungle camp sounds",
            Icon = "⚔️",
            SliderValue = 0,
            FrequencyRange = "300 - 1 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 13, CenterFrequency = 400, Q = 1.50, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Music & Score",
            Description = "Champion theme, game music, victory/defeat",
            Icon = "🎵",
            SliderValue = -1,
            FrequencyRange = "Full spectrum",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = -1.0, UserOffset = 0 }
            }
        },
    };

    // ── Sports: Commentary, crowd, ball ──
    private static List<SoundCategory> GetSportsDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Ball & Impact",
            Description = "Ball kick, bat hit, net swoosh, rim bounce",
            Icon = "⚽",
            SliderValue = 2,
            FrequencyRange = "200 - 2 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Commentary",
            Description = "Sportscaster voice, analysis, replays",
            Icon = "🎙️",
            SliderValue = 3,
            FrequencyRange = "1 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Crowd & Stadium",
            Description = "Crowd roar, chants, stadium atmosphere",
            Icon = "🏟️",
            SliderValue = 2,
            FrequencyRange = "200 - 8 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
    };

    // ── Action: Impact hits, cinematic, music ──
    private static List<SoundCategory> GetActionDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Bass Impact",
            Description = "Punches, kicks, explosions, environmental destruction",
            Icon = "👊",
            SliderValue = 2,
            FrequencyRange = "40 - 200 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Weapon Hits",
            Description = "Sword clashes, bullet impacts, melee connect",
            Icon = "⚔️",
            SliderValue = 3,
            FrequencyRange = "1 - 5 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Dialogue & Story",
            Description = "Character voice, cutscenes, narrative moments",
            Icon = "🗣️",
            SliderValue = 2,
            FrequencyRange = "1 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Music & Score",
            Description = "Orchestral score, battle themes, ambient tracks",
            Icon = "🎵",
            SliderValue = 1,
            FrequencyRange = "Full spectrum",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
    };

    // ── Stealth: Enemy footsteps, detection, ambient ──
    private static List<SoundCategory> GetStealthDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Enemy Footsteps",
            Description = "Guard patrol, enemy movement, approach direction",
            Icon = "👃",
            SliderValue = 4,
            FrequencyRange = "500 - 3 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 3.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Detection Cues",
            Description = "Alert sounds, suspicion meters, search mode triggers",
            Icon = "⚠️",
            SliderValue = 3,
            FrequencyRange = "2 - 6 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 17, CenterFrequency = 5000, Q = 4.00, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Environmental",
            Description = "Wind, rain, crowd noise (for cover), room acoustics",
            Icon = "🌧️",
            SliderValue = 1,
            FrequencyRange = "1 - 10 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Dialogue & Intel",
            Description = "Overheard conversations, radio chatter, mission updates",
            Icon = "📻",
            SliderValue = 2,
            FrequencyRange = "1 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 }
            }
        },
    };

    // ── Strategy: Unit sounds, battle ambience ──
    private static List<SoundCategory> GetStrategyDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Unit Response",
            Description = "Unit acknowledgment, selection sounds, ability confirm",
            Icon = "🎖️",
            SliderValue = 2,
            FrequencyRange = "500 - 3 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 1.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 2.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Battle Sounds",
            Description = "Combat clashes, gunfire, explosions, siege",
            Icon = "💥",
            SliderValue = 0,
            FrequencyRange = "100 - 2 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Alerts & UI",
            Description = "Base under attack, resource collected, research complete",
            Icon = "🔔",
            SliderValue = 3,
            FrequencyRange = "2 - 5 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 16, CenterFrequency = 3500, Q = 3.00, FilterType = "PK", BaseGain = 2.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Ambient & Music",
            Description = "Background score, environmental ambience",
            Icon = "🎶",
            SliderValue = 0,
            FrequencyRange = "Full spectrum",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 }
            }
        },
    };

    // ── Music: Balanced, warm, enjoyable ──
    private static List<SoundCategory> GetMusicDefaults() => new()
    {
        new SoundCategory
        {
            Name = "Sub Bass",
            Description = "Sub-bass rumble, kick drum depth, 808s",
            Icon = "🔈",
            SliderValue = 1,
            FrequencyRange = "20 - 80 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 11, CenterFrequency = 80, Q = 0.80, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Bass & Warmth",
            Description = "Bass guitar, warm low-end, body of the mix",
            Icon = "🔊",
            SliderValue = 1,
            FrequencyRange = "80 - 400 Hz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 12, CenterFrequency = 250, Q = 1.20, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Midrange & Vocals",
            Description = "Vocal clarity, guitar body, piano fundamental",
            Icon = "🎤",
            SliderValue = 1,
            FrequencyRange = "400 - 4 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 14, CenterFrequency = 1200, Q = 2.00, FilterType = "PK", BaseGain = 0.5, UserOffset = 0 },
                new FilterMapping { FilterIndex = 15, CenterFrequency = 2500, Q = 2.50, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Presence & Clarity",
            Description = "Vocal air, cymbal shimmer, string detail",
            Icon = "✨",
            SliderValue = 1,
            FrequencyRange = "4 - 10 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 17, CenterFrequency = 5000, Q = 4.00, FilterType = "PK", BaseGain = 1.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 18, CenterFrequency = 8000, Q = 3.00, FilterType = "PK", BaseGain = 0.5, UserOffset = 0 }
            }
        },
        new SoundCategory
        {
            Name = "Air & Sparkle",
            Description = "High harmonics, breath, vinyl crackle, open highs",
            Icon = "💫",
            SliderValue = 0,
            FrequencyRange = "10 - 20 kHz",
            Filters = new()
            {
                new FilterMapping { FilterIndex = 19, CenterFrequency = 12000, Q = 2.00, FilterType = "PK", BaseGain = 0.0, UserOffset = 0 },
                new FilterMapping { FilterIndex = 20, CenterFrequency = 14000, Q = 0.70, FilterType = "HSC", BaseGain = 0.0, UserOffset = 0 }
            }
        },
    };
}
