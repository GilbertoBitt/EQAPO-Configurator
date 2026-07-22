using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public static class ConfigWriter
{
    /// <summary>
    /// Generate a self-contained EqualizerAPO profile file.
    /// Headphone correction filters (1-10) + game fine-tuning (11+).
    /// No nested Includes — everything in one file, like the working Python switcher.
    /// </summary>
    public static string GenerateConfig(GameProfile profile)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Device: headphones");
        sb.AppendLine("Channel: All");
        sb.AppendLine();
        sb.AppendLine($"# {profile.Name} — EQAPO Configurator");
        sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Preamp: {profile.Preamp:F1} dB");
        sb.AppendLine();

        // Layer 1: Headphone correction — read from file and inline
        int filterIndex = 1;
        AppSettings settings = AppSettingsService.Load();
        string headphoneFile = settings.HeadphoneLayerFilename;

        if (!string.IsNullOrWhiteSpace(headphoneFile))
        {
            string headphonePath = Path.Combine(
                EqualizerApoService.ResolveInstallation()?.ConfigPath ?? "", headphoneFile);
            if (File.Exists(headphonePath))
            {
                sb.AppendLine("# ── HEADPHONE CORRECTION ──");
                foreach (string line in File.ReadLines(headphonePath))
                {
                    string trimmed = line.Trim();
                    // Rewrite filter lines with sequential numbering
                    var m = Regex.Match(trimmed,
                        @"^Filter\s+\d+:\s*(ON\s+\S+\s+Fc\s+[\d.,]+\s*Hz\s+Gain\s+[-\d.,]+\s*dB\s+Q\s+[\d.,]+)",
                        RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        sb.AppendLine($"Filter {filterIndex++}: {m.Groups[1].Value}");
                    }
                    else if (trimmed.StartsWith("Preamp:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip — we already wrote our own preamp
                    }
                    else if (trimmed.StartsWith("Device:", StringComparison.OrdinalIgnoreCase) ||
                             trimmed.StartsWith("Channel:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip — already written
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        sb.AppendLine(trimmed);
                    }
                }
                sb.AppendLine();
            }
        }

        if (filterIndex == 1)
        {
            // No headphone file found — use hardcoded fallback
            sb.AppendLine("# ── HEADPHONE CORRECTION (default) ──");
            sb.AppendLine($"Filter {filterIndex++}: ON LSC Fc 105.0 Hz Gain -3.8 dB Q 0.70");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 54.4 Hz Gain 3.3 dB Q 2.01");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 138.2 Hz Gain -5.8 dB Q 1.13");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 363.0 Hz Gain 2.3 dB Q 1.72");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 726.0 Hz Gain -1.6 dB Q 1.66");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 1579.5 Hz Gain 1.6 dB Q 2.10");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 3910.5 Hz Gain 3.3 dB Q 5.72");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 6357.0 Hz Gain 3.7 dB Q 4.87");
            sb.AppendLine($"Filter {filterIndex++}: ON PK Fc 8359.0 Hz Gain 1.9 dB Q 2.51");
            sb.AppendLine($"Filter {filterIndex++}: ON HSC Fc 10000.0 Hz Gain -1.7 dB Q 0.70");
            sb.AppendLine();
        }

        // Layer 2: Game-specific fine-tuning (continues from headphone filter count)
        sb.AppendLine($"# ── {profile.Name.ToUpper()} EQ ──");
        foreach (var category in profile.SoundCategories)
        {
            sb.AppendLine();
            sb.AppendLine($"# [{category.Name}] — {category.Description}");

            foreach (var filter in category.Filters)
            {
                double finalGain = filter.BaseGain + filter.UserOffset;
                sb.AppendLine($"Filter {filterIndex++}: ON {filter.FilterType} Fc {filter.CenterFrequency:F0} Hz Gain {finalGain:+0.0;-0.0} dB Q {filter.Q:F2}");
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

    /// <summary>
    /// Write a self-contained profile file and point config.txt to it.
    /// Simple like the Python switcher: just "Include: filename" in config.txt.
    /// </summary>
    public static void WriteConfig(GameProfile profile)
    {
        var installation = EqualizerApoService.ResolveInstallation()
            ?? throw new DirectoryNotFoundException("EqualizerAPO is not installed.");
        string configDir = installation.ConfigPath;
        string configTxtPath = Path.Combine(configDir, "config.txt");

        // Use pre-made config file if available, otherwise generate
        string filename;
        if (!string.IsNullOrEmpty(profile.ConfigFileName) &&
            File.Exists(Path.Combine(configDir, profile.ConfigFileName)))
        {
            filename = profile.ConfigFileName;
        }
        else
        {
            filename = GenerateConfigFilename(profile);
            string config = GenerateConfig(profile);
            DirectWrite(Path.Combine(configDir, filename), config);
        }

        // Write config.txt — just one Include line, like the Python switcher
        WriteSimpleConfigInclude(configTxtPath, filename, profile.Name);
    }

    /// <summary>
    /// Switch to Peace GUI (no custom EQ).
    /// </summary>
    public static void WriteToPeace()
    {
        var installation = EqualizerApoService.ResolveInstallation()
            ?? throw new DirectoryNotFoundException("EqualizerAPO is not installed.");
        string configTxtPath = Path.Combine(installation.ConfigPath, "config.txt");
        WriteSimpleConfigInclude(configTxtPath, "peace.txt", "Peace GUI");
    }

    /// <summary>
    /// Apply headphone correction only (no game fine-tuning).
    /// </summary>
    public static void WriteHeadphoneBaseOnly()
    {
        var installation = EqualizerApoService.ResolveInstallation()
            ?? throw new DirectoryNotFoundException("EqualizerAPO is not installed.");
        string configDir = installation.ConfigPath;
        string configTxtPath = Path.Combine(configDir, "config.txt");

        AppSettings settings = AppSettingsService.Load();
        string headphoneFile = settings.HeadphoneLayerFilename;
        if (string.IsNullOrWhiteSpace(headphoneFile))
            throw new InvalidOperationException("No headphone base profile is selected.");

        // Generate a headphone-only config: read the headphone file, inline its filters
        string headphonePath = Path.Combine(configDir, headphoneFile);
        if (!File.Exists(headphonePath))
            throw new FileNotFoundException($"Headphone file not found: {headphoneFile}");

        var sb = new StringBuilder();
        sb.AppendLine("Device: headphones");
        sb.AppendLine("Channel: All");
        sb.AppendLine($"# Headphone correction — {settings.HeadphoneLayerName}");
        foreach (string line in File.ReadLines(headphonePath))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Device:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Channel:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(trimmed))
                sb.AppendLine(trimmed);
        }

        const string filename = "eqapo_headphone_base.txt";
        DirectWrite(Path.Combine(configDir, filename), sb.ToString());
        WriteSimpleConfigInclude(configTxtPath, filename, "Headphone base");
    }

    /// <summary>
    /// Write a simple config.txt with just an Include line.
    /// Matches the Python switcher format: "Include: filename\n# comment"
    /// </summary>
    private static void WriteSimpleConfigInclude(string configTxtPath, string filename, string profileName)
    {
        string content = $"Include: {filename}\n# EQAPO Configurator — {profileName}\n";
        DirectWrite(configTxtPath, content);
    }

    /// <summary>
    /// Write file content directly. EqualizerAPO watches config.txt via file system
    /// watcher — it needs the same file handle/inode modified, not replaced.
    /// Matches the Python switcher: open("config.txt", "w") + write.
    /// </summary>
    private static void DirectWrite(string filePath, string content)
    {
        File.WriteAllText(filePath, content, new UTF8Encoding(false));
    }
}
