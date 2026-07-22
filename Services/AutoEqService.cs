using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public class HeadphoneSearchResult
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // "over-ear", "in-ear", "earbud"
    public string MeasurementSource { get; set; } = "";
    public string DownloadUrl { get; set; } = ""; // Full raw GitHub URL to ParametricEQ.txt
}

public class HeadphoneEqProfile
{
    public string HeadphoneName { get; set; } = "";
    public List<FilterMapping> Filters { get; set; } = new();
    public double Preamp { get; set; } = -6.0;
    public string RawText { get; set; } = "";
}

public static class AutoEqService
{
    private static readonly string HeadphonesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQAPO-Configurator", "headphones");
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQAPO-Configurator", "cache");

    // Bundled profiles ship with the app
    private static readonly string BundledProfilesDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Profiles");

    // AutoEq GitHub raw base
    private static readonly string AutoEqRawBase = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results";
    private static readonly string ReadmeUrl = $"{AutoEqRawBase}/README.md";
    private static readonly string IndexUrl = $"{AutoEqRawBase}/INDEX.md";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    // In-memory cache of parsed README entries
    private static List<ReadmeEntry>? _readmeCache;
    private static readonly object _cacheLock = new();

    static AutoEqService()
    {
        Directory.CreateDirectory(HeadphonesDir);
        Directory.CreateDirectory(CacheDir);
    }

    /// <summary>
    /// A single parsed line from the AutoEQ results README.md
    /// Format: - [Headphone Name](./source/type/headphone/) - Source
    /// </summary>
    private class ReadmeEntry
    {
        public string Name { get; set; } = "";
        public string RelativePath { get; set; } = ""; // e.g. "./oratory1990/over-ear/Sennheiser HD 600/"
        public string Source { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
    }

    /// <summary>
    /// Download and cache the README.md from AutoEQ, parse all headphone entries
    /// </summary>
    private static async Task<List<ReadmeEntry>> GetReadmeEntriesAsync()
    {
        lock (_cacheLock)
        {
            if (_readmeCache != null) return _readmeCache;
        }

        string readme = "";

        // Try to use cached file if fresh (< 24h old)
        string cacheFile = Path.Combine(CacheDir, "autoeq_readme.md");
        if (File.Exists(cacheFile))
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile);
            if (age < TimeSpan.FromHours(24))
                readme = File.ReadAllText(cacheFile);
        }

        // Download if cache miss
        if (string.IsNullOrEmpty(readme))
        {
            try
            {
                readme = await Http.GetStringAsync(ReadmeUrl);
                File.WriteAllText(cacheFile, readme, Encoding.UTF8);
            }
            catch
            {
                // If download fails and we have a stale cache, use it
                if (File.Exists(cacheFile))
                    readme = File.ReadAllText(cacheFile);
                else
                    return new List<ReadmeEntry>();
            }
        }

        var entries = new List<ReadmeEntry>();

        // Parse lines like:
        // - [Sennheiser HD 600](./oratory1990/over-ear/Sennheiser%20HD%20600/) - oratory1990
        // or with HTML table format:
        // <tr><td><a href="./oratory1990/over-ear/Sennheiser%20HD%20600/">Sennheiser HD 600</a></td><td>oratory1990</td></tr>
        var lines = readme.Split('\n');
        foreach (var line in lines)
        {
            // Markdown link format: [Name](./path/)
            var mdMatch = Regex.Match(line, @"\[([^\]]+)\]\(\./([^)]+)\)");
            if (mdMatch.Success)
            {
                string name = mdMatch.Groups[1].Value.Trim();
                string relPath = "./" + mdMatch.Groups[2].Value.Trim().TrimEnd('/') + "/";

                // Extract source from the line (usually after " - ")
                string source = "";
                var sourceMatch = Regex.Match(line, @"\)\s*-\s*(.+)$");
                if (sourceMatch.Success)
                    source = sourceMatch.Groups[1].Value.Trim();

                string encodedPath = mdMatch.Groups[2].Value.Trim().TrimEnd('/');
                entries.Add(new ReadmeEntry
                {
                    Name = name,
                    RelativePath = relPath,
                    Source = source,
                    DownloadUrl = BuildDownloadUrl(encodedPath),
                });
                continue;
            }

            // HTML table format: <a href="./path/">Name</a>
            var htmlMatch = Regex.Match(line, @"href=""\./([^""]+)/""[^>]*>([^<]+)</a>");
            if (htmlMatch.Success)
            {
                string relPath = "./" + htmlMatch.Groups[1].Value.Trim().TrimEnd('/') + "/";
                string name = htmlMatch.Groups[2].Value.Trim();

                string source = "";
                var sourceMatch = Regex.Match(line, @"<td>([^<]+)</td>\s*</tr>");
                if (sourceMatch.Success)
                    source = sourceMatch.Groups[1].Value.Trim();

                string encodedPath = htmlMatch.Groups[1].Value.Trim().TrimEnd('/');
                entries.Add(new ReadmeEntry
                {
                    Name = name,
                    RelativePath = relPath,
                    Source = source,
                    DownloadUrl = BuildDownloadUrl(encodedPath),
                });
            }
        }

        lock (_cacheLock)
        {
            _readmeCache = entries;
        }

        return entries;
    }

    /// <summary>
    /// Search for headphones by name using AutoEQ README index.
    /// Returns the recommended (highest accuracy) profile per headphone.
    /// </summary>
    public static async Task<List<HeadphoneSearchResult>> SearchHeadphonesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<HeadphoneSearchResult>();

        var entries = await GetReadmeEntriesAsync();
        string normalizedQuery = Normalize(query);
        string[] tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return entries
            .Select(entry => new { Entry = entry, Score = Score(entry.Name, tokens, normalizedQuery) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.Name.Length)
            .GroupBy(x => Normalize(x.Entry.Name))
            .Select(g => g.First().Entry)
            .Take(25)
            .Select(entry =>
            {
                return new HeadphoneSearchResult
                {
                    Name = entry.Name,
                    Type = DetectType(entry.RelativePath),
                    MeasurementSource = entry.Source,
                    DownloadUrl = entry.DownloadUrl,
                };
            }).ToList();
    }

    private static int Score(string name, string[] tokens, string normalizedQuery)
    {
        string normalized = Normalize(name);
        if (normalized == normalizedQuery) return 1000;
        int score = normalized.StartsWith(normalizedQuery, StringComparison.Ordinal) ? 500 : 0;
        foreach (string token in tokens)
        {
            int index = normalized.IndexOf(token, StringComparison.Ordinal);
            if (index < 0) return 0;
            score += index == 0 ? 120 : 60 - Math.Min(index, 40);
        }
        return score;
    }

    private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();

    private static string BuildDownloadUrl(string encodedDirectoryPath)
    {
        string directoryName = encodedDirectoryPath.Split('/').Last();
        return $"{AutoEqRawBase}/{encodedDirectoryPath}/{directoryName}%20ParametricEQ.txt";
    }

    /// <summary>
    /// Download the ParametricEQ.txt for a specific headphone from AutoEQ GitHub
    /// </summary>
    public static async Task<HeadphoneEqProfile?> DownloadProfileAsync(HeadphoneSearchResult headphone)
    {
        if (string.IsNullOrEmpty(headphone.DownloadUrl))
            return null;

        try
        {
            string eqData = await Http.GetStringAsync(headphone.DownloadUrl);
            var profile = ParseParametricEq(eqData);
            if (profile != null)
            {
                profile.HeadphoneName = headphone.Name;
                SaveLocalProfile(headphone.Name, eqData);
                return profile;
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"AutoEQ download failed ({ex.StatusCode}): {headphone.Name}", ex);
        }

        return null;
    }

    /// <summary>
    /// Download a raw ParametricEQ.txt file from any URL
    /// </summary>
    public static async Task<HeadphoneEqProfile?> DownloadFromUrlAsync(string url)
    {
        try
        {
            string eqData = await Http.GetStringAsync(url);
            var profile = ParseParametricEq(eqData);
            if (profile != null)
            {
                profile.HeadphoneName = ExtractNameFromUrl(url);
                SaveLocalProfile(profile.HeadphoneName, eqData);
                return profile;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Parse a ParametricEQ.txt content into a HeadphoneEqProfile
    /// Format: Preamp: -6.1 dB / Filter N: ON PK Fc XXX Hz Gain X.X dB Q X.XX
    /// </summary>
    public static HeadphoneEqProfile? ParseParametricEq(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var profile = new HeadphoneEqProfile { RawText = content };
        var filters = new List<FilterMapping>();
        int filterIndex = 1;

        // Extract preamp
        var preampMatch = Regex.Match(content, @"Preamp:\s*(-?\d+[.,]?\d*)\s*dB", RegexOptions.IgnoreCase);
        if (preampMatch.Success)
        {
            string val = preampMatch.Groups[1].Value.Replace(",", ".");
            if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double preamp))
                profile.Preamp = preamp;
        }

        // Parse filter lines (dot and comma decimal formats)
        foreach (var line in content.Split('\n'))
        {
            var m = Regex.Match(line.Trim(),
                @"Filter\s+\d+:\s*ON\s+(PK|LS|HS|HP|LP)\s+Fc\s+([\d.,]+)\s*Hz\s+Gain\s+(-?[\d.,]+)\s*dB\s+Q\s+([\d.,]+)",
                RegexOptions.IgnoreCase);

            if (m.Success)
            {
                string type = m.Groups[1].Value.ToUpper();
                string freqStr = m.Groups[2].Value.Replace(",", ".");
                string gainStr = m.Groups[3].Value.Replace(",", ".");
                string qStr = m.Groups[4].Value.Replace(",", ".");

                if (double.TryParse(freqStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double freq) &&
                    double.TryParse(gainStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double gain) &&
                    double.TryParse(qStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double q))
                {
                    filters.Add(new FilterMapping
                    {
                        FilterIndex = filterIndex++,
                        FilterType = type,
                        CenterFrequency = freq,
                        BaseGain = gain,
                        Q = q,
                        UserOffset = 0,
                    });
                }
            }
        }

        if (filters.Count == 0) return null;
        profile.Filters = filters;
        return profile;
    }

    // ── Bundled + Local profiles ──

    public static List<string> GetBundledProfiles()
    {
        var profiles = new List<string>();
        if (Directory.Exists(BundledProfilesDir))
        {
            foreach (var file in Directory.GetFiles(BundledProfilesDir, "*.txt"))
                profiles.Add(Path.GetFileNameWithoutExtension(file));
        }
        return profiles;
    }

    public static HeadphoneEqProfile? LoadBundledProfile(string name)
    {
        string filePath = Path.Combine(BundledProfilesDir, name + ".txt");
        if (!File.Exists(filePath)) return null;

        string content = File.ReadAllText(filePath);
        var profile = ParseParametricEq(content);
        if (profile != null) profile.HeadphoneName = name;
        return profile;
    }

    public static List<string> GetLocalProfiles()
    {
        var profiles = new HashSet<string>();
        foreach (var name in GetBundledProfiles()) profiles.Add(name);
        if (Directory.Exists(HeadphonesDir))
        {
            foreach (var file in Directory.GetFiles(HeadphonesDir, "*.txt"))
                profiles.Add(Path.GetFileNameWithoutExtension(file));
        }
        return profiles.OrderBy(p => p).ToList();
    }

    public static HeadphoneEqProfile? LoadLocalProfile(string name)
    {
        var bundled = LoadBundledProfile(name);
        if (bundled != null) return bundled;

        string filePath = Path.Combine(HeadphonesDir, name + ".txt");
        if (!File.Exists(filePath)) return null;

        string content = File.ReadAllText(filePath);
        var profile = ParseParametricEq(content);
        if (profile != null) profile.HeadphoneName = name;
        return profile;
    }

    public static void SaveLocalProfile(string name, string eqData)
    {
        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        string filePath = Path.Combine(HeadphonesDir, safeName + ".txt");
        File.WriteAllText(filePath, eqData, Encoding.UTF8);
    }

    public static bool DeleteLocalProfile(string name)
    {
        string filePath = Path.Combine(HeadphonesDir, name + ".txt");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        return false;
    }

    public static string ExportToEqualizerApo(HeadphoneEqProfile profile)
    {
        string eqapoConfigPath = EqualizerApoService.ResolveInstallation()?.ConfigPath
            ?? throw new DirectoryNotFoundException("EqualizerAPO is not installed.");
        string safeName = string.Concat(profile.HeadphoneName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))
            .Replace(" ", "_").ToLowerInvariant();
        string filename = $"headphone_{safeName}.txt";
        string filePath = Path.Combine(eqapoConfigPath, filename);
        File.WriteAllText(filePath, profile.RawText, Encoding.UTF8);
        return filename;
    }

    private static string DetectType(string relativePath)
    {
        if (relativePath.Contains("in-ear", StringComparison.OrdinalIgnoreCase)) return "in-ear";
        if (relativePath.Contains("earbud", StringComparison.OrdinalIgnoreCase)) return "earbud";
        return "over-ear";
    }

    private static string ExtractNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            string lastSegment = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            return Uri.UnescapeDataString(lastSegment).Replace(" ParametricEQ", "").Trim();
        }
        catch { return "Unknown Headphone"; }
    }
}
