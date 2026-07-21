using System.IO;
using System.Text;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public static class ConfigWriter
{
    private static readonly string EqapoConfigPath = @"C:\Program Files\EqualizerAPO\config";
    private static readonly string ConfigTxtPath = Path.Combine(EqapoConfigPath, "config.txt");

    // Headphone correction filters (AutoEQ for Arctis Nova Pro Wireless)
    private static readonly List<string> HeadphoneCorrection = new()
    {
        "# [CORRECTION] Sub-Bass Bloat Tame",
        "Filter 1: ON LSC Fc 105.0 Hz Gain -3.8 dB Q 0.70",
        "# [CORRECTION] Sub-Bass Detail Restore",
        "Filter 2: ON PK Fc 54.4 Hz Gain 3.3 dB Q 2.01",
        "# [CORRECTION] Mid-Bass Hump Cut",
        "Filter 3: ON PK Fc 138.2 Hz Gain -5.8 dB Q 1.13",
        "# [CORRECTION] Lower-Mid Body Restore",
        "Filter 4: ON PK Fc 363.0 Hz Gain 2.3 dB Q 1.72",
        "# [CORRECTION] Boxiness Cut",
        "Filter 5: ON PK Fc 726.0 Hz Gain -1.6 dB Q 1.66",
        "# [CORRECTION] Presence Restore",
        "Filter 6: ON PK Fc 1579.5 Hz Gain 1.6 dB Q 2.10",
        "# [CORRECTION] Upper-Mid Detail",
        "Filter 7: ON PK Fc 3910.5 Hz Gain 3.3 dB Q 5.72",
        "# [CORRECTION] Treble Sparkle",
        "Filter 8: ON PK Fc 6357.0 Hz Gain 3.7 dB Q 4.87",
        "# [CORRECTION] Air Restore",
        "Filter 9: ON PK Fc 8359.0 Hz Gain 1.9 dB Q 2.51",
        "# [CORRECTION] Treble Rolloff",
        "Filter 10: ON HSC Fc 10000.0 Hz Gain -1.7 dB Q 0.70",
    };

    public static string GenerateConfig(GameProfile profile)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Device: headphones");
        sb.AppendLine("Channel: All");
        sb.AppendLine();
        sb.AppendLine($"# {profile.Name} — EQAPO Configurator");
        sb.AppendLine($"# {profile.Description}");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Preamp: {profile.Preamp:F1} dB");
        sb.AppendLine();

        // Layer 1: Headphone correction
        sb.AppendLine("# ════════════════════════════════════════");
        sb.AppendLine("# LAYER 1: HEADPHONE CORRECTION (AutoEQ)");
        sb.AppendLine("# ════════════════════════════════════════");
        foreach (var line in HeadphoneCorrection)
        {
            sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("# ════════════════════════════════════════");
        sb.AppendLine($"# LAYER 2: {profile.Name.ToUpper()}");
        sb.AppendLine("# ════════════════════════════════════════");

        // Layer 2: User's sound categories
        foreach (var category in profile.SoundCategories)
        {
            sb.AppendLine();
            sb.AppendLine($"# [{category.Name}]");
            sb.AppendLine($"# {category.Description}");

            foreach (var filter in category.Filters)
            {
                double finalGain = filter.BaseGain + filter.UserOffset;
                string filterLine = $"Filter {filter.FilterIndex}: ON {filter.FilterType} Fc {filter.CenterFrequency:F0} Hz Gain {finalGain:+0.0;-0.0} dB Q {filter.Q:F2}";
                sb.AppendLine(filterLine);
            }
        }

        return sb.ToString();
    }

    public static string GenerateConfigFilename(GameProfile profile)
    {
        string safeName = profile.Name.ToLower()
            .Replace(" ", "_")
            .Replace("&", "and")
            .Replace("'", "")
            .Replace("\"", "");
        return $"custom_{safeName}.txt";
    }

    public static void WriteConfig(GameProfile profile)
    {
        string filename;

        // Use pre-made config file if available
        if (!string.IsNullOrEmpty(profile.ConfigFileName))
        {
            string presetPath = Path.Combine(EqapoConfigPath, profile.ConfigFileName);
            if (File.Exists(presetPath))
            {
                filename = profile.ConfigFileName;
            }
            else
            {
                // Fallback: generate from profile
                string config = GenerateConfig(profile);
                filename = GenerateConfigFilename(profile);
                string configPath = Path.Combine(EqapoConfigPath, filename);
                File.WriteAllText(configPath, config, Encoding.UTF8);
            }
        }
        else
        {
            // Generate from profile sound categories
            string config = GenerateConfig(profile);
            filename = GenerateConfigFilename(profile);
            string configPath = Path.Combine(EqapoConfigPath, filename);
            File.WriteAllText(configPath, config, Encoding.UTF8);
        }

        // Update config.txt to point to this profile
        string includeLine = $"Include: {filename}\n# EQAPO Configurator — {profile.Name}\n";
        File.WriteAllText(ConfigTxtPath, includeLine, Encoding.UTF8);
    }

    public static void WriteToPeace()
    {
        string includeLine = "Include: peace.txt\n# Peace GUI mode\n";
        File.WriteAllText(ConfigTxtPath, includeLine, Encoding.UTF8);
    }
}
