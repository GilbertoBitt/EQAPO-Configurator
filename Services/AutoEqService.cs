using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public class HeadphoneSearchResult
{
    public string Name { get; set; } = "";
    public string Id { get; set; } = "";
    public string Type { get; set; } = ""; // "over-ear", "in-ear", "earbud"
    public string MeasurementSource { get; set; } = "";
    public string Target { get; set; } = "Harman 2018";
}

public class HeadphoneEqProfile
{
    public string HeadphoneName { get; set; } = "";
    public List<FilterMapping> Filters { get; set; } = new();
    public double Preamp { get; set; } = -6.0;
    public string Target { get; set; } = "";
    public string RawText { get; set; } = "";
}

public static class AutoEqService
{
    private static readonly string EqapoConfigPath = @"C:\Program Files\EqualizerAPO\config";
    private static readonly string HeadphonesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQAPO-Configurator", "headphones");

    // AutoEqApi endpoints (timschneeb/AutoEqApi)
    private static readonly string AutoEqApiBase = "https://autoeqapi.timschneeb.dev";

    // AutoEq GitHub raw results
    private static readonly string AutoEqGitHubBase = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static AutoEqService()
    {
        Directory.CreateDirectory(HeadphonesDir);
    }

    /// <summary>
    /// Search for headphones by name using AutoEqApi
    /// </summary>
    public static async Task<List<HeadphoneSearchResult>> SearchHeadphonesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<HeadphoneSearchResult>();

        var results = new List<HeadphoneSearchResult>();

        try
        {
            // Try AutoEqApi first
            string url = $"{AutoEqApiBase}/results/search/{Uri.EscapeDataString(query)}";
            var response = await Http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var apiResults = JsonSerializer.Deserialize<List<JsonElement>>(json);

                if (apiResults != null)
                {
                    foreach (var item in apiResults.Take(20))
                    {
                        results.Add(new HeadphoneSearchResult
                        {
                            Name = item.GetProperty("name").GetString() ?? "",
                            Id = item.GetProperty("id").GetInt64().ToString(),
                            Type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                            MeasurementSource = item.TryGetProperty("measurement", out var m) ? m.GetString() ?? "" : "",
                        });
                    }
                    return results;
                }
            }
        }
        catch { }

        // Fallback: search the GitHub results README
        try
        {
            results = await SearchGitHubResultsAsync(query);
        }
        catch { }

        return results;
    }

    private static async Task<List<HeadphoneSearchResult>> SearchGitHubResultsAsync(string query)
    {
        var results = new List<HeadphoneSearchResult>();

        try
        {
            // Download the README which lists all headphones
            string readmeUrl = $"{AutoEqGitHubBase}/README.md";
            string readme = await Http.GetStringAsync(readmeUrl);

            // Parse headphone names from the list
            var lines = readme.Split('\n');
            foreach (var line in lines)
            {
                // Look for lines like: - [Headphone Name](./brand/model/)
                var match = Regex.Match(line, @"\[([^\]]+)\]\(\./[^)]+\)");
                if (match.Success)
                {
                    string name = match.Groups[1].Value;
                    if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new HeadphoneSearchResult
                        {
                            Name = name,
                            Type = line.Contains("in-ear") ? "in-ear" :
                                   line.Contains("earbud") ? "earbud" : "over-ear",
                        });
                    }
                }

                if (results.Count >= 20) break;
            }
        }
        catch { }

        return results;
    }

    /// <summary>
    /// Download the ParametricEQ.txt file for a specific headphone
    /// </summary>
    public static async Task<HeadphoneEqProfile?> DownloadProfileAsync(HeadphoneSearchResult headphone)
    {
        try
        {
            // Try AutoEqApi first
            if (!string.IsNullOrEmpty(headphone.Id))
            {
                string url = $"{AutoEqApiBase}/results/{headphone.Id}";
                var response = await Http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string eqData = await response.Content.ReadAsStringAsync();
                    var profile = ParseParametricEq(eqData);
                    if (profile != null)
                    {
                        profile.HeadphoneName = headphone.Name;
                        profile.Target = headphone.Target;

                        // Save locally
                        SaveLocalProfile(headphone.Name, eqData);
                        return profile;
                    }
                }
            }
        }
        catch { }

        try
        {
            // Fallback: try GitHub raw
            string safeName = headphone.Name.Replace(" ", "%20");
            string githubUrl = $"{AutoEqGitHubBase}/oratory1990/harman_over-ear_2018/{safeName}/{safeName} ParametricEQ.txt";
            string eqData = await Http.GetStringAsync(githubUrl);

            var profile = ParseParametricEq(eqData);
            if (profile != null)
            {
                profile.HeadphoneName = headphone.Name;
                SaveLocalProfile(headphone.Name, eqData);
                return profile;
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Download a raw ParametricEQ.txt file from a URL
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
    /// Parse a ParametricEQ.txt file content into a HeadphoneEqProfile
    /// Format: Filter N: ON/OFF TYPE Fc XXX Hz Gain X.X dB Q X.XX
    /// </summary>
    public static HeadphoneEqProfile? ParseParametricEq(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var profile = new HeadphoneEqProfile
        {
            RawText = content
        };

        var filters = new List<FilterMapping>();
        int filterIndex = 1;

        // Extract preamp
        var preampMatch = Regex.Match(content, @"Preamp:\s*(-?\d+\.?\d*)\s*dB", RegexOptions.IgnoreCase);
        if (preampMatch.Success)
            profile.Preamp = double.Parse(preampMatch.Groups[1].Value);

        // Parse filter lines
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            // Match: Filter N: ON/OFF PK/LS/HS/HP/LP Fc XXX Hz Gain X.X dB Q X.XX
            var filterMatch = Regex.Match(line.Trim(),
                @"Filter\s+\d+:\s*ON\s+(PK|LS|HS|HP|LP)\s+Fc\s+([\d.]+)\s*Hz\s+Gain\s+(-?[\d.]+)\s*dB\s+Q\s+([\d.]+)",
                RegexOptions.IgnoreCase);

            if (!filterMatch.Success)
            {
                // Try comma format: Filter  1: ON  PK       Fc    50,0 Hz  Gain   -3,0 dB  Q 10,00
                filterMatch = Regex.Match(line.Trim(),
                    @"Filter\s+\d+:\s*ON\s+(PK|LS|HS|HP|LP)\s+Fc\s+([\d,]+)\s*Hz\s+Gain\s+(-?[\d,]+)\s*dB\s+Q\s+([\d,]+)",
                    RegexOptions.IgnoreCase);
            }

            if (filterMatch.Success)
            {
                string type = filterMatch.Groups[1].Value.ToUpper();
                string freqStr = filterMatch.Groups[2].Value.Replace(",", ".");
                string gainStr = filterMatch.Groups[3].Value.Replace(",", ".");
                string qStr = filterMatch.Groups[4].Value.Replace(",", ".");

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

    /// <summary>
    /// Get list of locally saved headphone profiles
    /// </summary>
    public static List<string> GetLocalProfiles()
    {
        var profiles = new List<string>();
        if (Directory.Exists(HeadphonesDir))
        {
            foreach (var file in Directory.GetFiles(HeadphonesDir, "*.txt"))
            {
                profiles.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        return profiles;
    }

    /// <summary>
    /// Load a local headphone profile by name
    /// </summary>
    public static HeadphoneEqProfile? LoadLocalProfile(string name)
    {
        string filePath = Path.Combine(HeadphonesDir, name + ".txt");
        if (!File.Exists(filePath)) return null;

        string content = File.ReadAllText(filePath);
        var profile = ParseParametricEq(content);
        if (profile != null)
            profile.HeadphoneName = name;
        return profile;
    }

    /// <summary>
    /// Save a headphone profile locally
    /// </summary>
    public static void SaveLocalProfile(string name, string eqData)
    {
        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        string filePath = Path.Combine(HeadphonesDir, safeName + ".txt");
        File.WriteAllText(filePath, eqData, Encoding.UTF8);
    }

    /// <summary>
    /// Delete a local headphone profile
    /// </summary>
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

    /// <summary>
    /// Export a headphone correction file to EqualizerAPO config directory
    /// </summary>
    public static string ExportToEqualizerApo(HeadphoneEqProfile profile)
    {
        string filename = $"headphone_{profile.HeadphoneName.Replace(" ", "_").ToLower()}.txt";
        string filePath = Path.Combine(EqapoConfigPath, filename);
        File.WriteAllText(filePath, profile.RawText, Encoding.UTF8);
        return filename;
    }

    private static string ExtractNameFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            string lastSegment = Path.GetFileNameWithoutExtension(uri.AbsolutePath);
            return lastSegment.Replace("%20", " ");
        }
        catch
        {
            return "Unknown Headphone";
        }
    }
}
