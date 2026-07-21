using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EQAPO_Configurator.Services;

/// <summary>
/// Wrapper service that calls the Python autoeq library to generate
/// dynamic headphone EQ profiles from measurement data.
/// Requires Python 3.11+ with autoeq installed (pip install autoeq).
/// </summary>
public static class PythonService
{
    // Bundled Python ships with the app — no install required
    private static readonly string BundledPythonDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "python", "embedded");
    private static readonly string BundledPythonExe = Path.Combine(BundledPythonDir, "python.exe");
    private static readonly string BundledScript = Path.Combine(BundledPythonDir, "generate_peq.py");

    // Common Python install paths on Windows
    private static readonly string[] PythonPaths = new[]
    {
        @"C:\Users\{USER}\AppData\Local\Programs\Python\Python311\python.exe",
        @"C:\Users\{USER}\AppData\Local\Programs\Python\Python312\python.exe",
        @"C:\Users\{USER}\AppData\Local\Programs\Python\Python310\python.exe",
        @"C:\Python311\python.exe",
        @"C:\Python312\python.exe",
    };

    private static string? _pythonPath;

    /// <summary>
    /// Detect if Python is available and autoeq is installed.
    /// Checks bundled Python first, then falls back to system Python.
    /// </summary>
    public static (bool pythonAvailable, bool autoeqInstalled, string pythonPath, string error) Detect()
    {
        // 1. Check bundled Python first (preferred — no install needed)
        if (File.Exists(BundledPythonExe) && File.Exists(BundledScript))
        {
            try
            {
                var result = RunPython(BundledPythonExe, "-c \"import autoeq; print('ok')\"");
                if (result.ExitCode == 0 && result.Output.Contains("ok"))
                    return (true, true, BundledPythonExe, "");
                else
                    return (true, false, BundledPythonExe, $"Bundled Python found but autoeq failed: {result.Error}");
            }
            catch { }
        }

        // 2. Fall back to system Python
        var path = FindSystemPython();
        if (path == null)
            return (false, false, "", "Python not found. Install Python 3.11 from python.org or ensure bundled Python is present");

        if (!File.Exists(BundledScript))
            return (true, false, path, "generate_peq.py not found in app directory");

        try
        {
            var result = RunPython(path, "-c \"import autoeq; print('ok')\"");
            if (result.ExitCode == 0 && result.Output.Contains("ok"))
                return (true, true, path, "");
            else
                return (true, false, path, $"autoeq not installed. Run: pip install autoeq\n{result.Error}");
        }
        catch (Exception ex)
        {
            return (true, false, path, $"Failed to check autoeq: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a ParametricEQ.txt for the given headphone using Python autoeq
    /// </summary>
    public static async Task<HeadphoneEqProfile?> GenerateProfileAsync(
        string headphoneName,
        string target = "harman_overear_2018",
        string config = "8_PEAKING_WITH_SHELVES",
        IProgress<string>? progress = null)
    {
        var pythonPath = FindPythonToUse();
        if (pythonPath == null)
            throw new InvalidOperationException("Python not found");

        string scriptPath = File.Exists(BundledScript) ? BundledScript : throw new InvalidOperationException("generate_peq.py not found");

        progress?.Report($"Generating EQ for {headphoneName}...");
        progress?.Report("Downloading measurement data from AutoEQ...");

        var args = $"\"{scriptPath}\" --name \"{headphoneName}\" --target {target} --config {config} --json";
        var result = await RunPythonAsync(pythonPath, args);

        if (result.ExitCode != 0)
        {
            string error = result.Error.Contains("Could not find measurement")
                ? $"No measurement data found for '{headphoneName}'. Try searching on autoeq.app"
                : $"Python error: {result.Error}";
            throw new InvalidOperationException(error);
        }

        // Parse JSON output
        try
        {
            var json = JsonDocument.Parse(result.Output);
            var root = json.RootElement;

            if (root.GetProperty("success").GetBoolean())
            {
                string peqContent = root.GetProperty("content").GetString() ?? "";
                var profile = AutoEqService.ParseParametricEq(peqContent);
                if (profile != null)
                {
                    profile.HeadphoneName = headphoneName;
                    progress?.Report($"Generated {profile.Filters.Count} filters for {headphoneName}");
                    return profile;
                }
            }
            else
            {
                string error = root.TryGetProperty("error", out var err) ? err.GetString() ?? "Unknown" : "Unknown";
                throw new InvalidOperationException(error);
            }
        }
        catch (JsonException)
        {
            // Fallback: treat raw output as PEQ content
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                var profile = AutoEqService.ParseParametricEq(result.Output);
                if (profile != null)
                {
                    profile.HeadphoneName = headphoneName;
                    return profile;
                }
            }
            throw new InvalidOperationException("Failed to parse Python output");
        }

        return null;
    }

    /// <summary>
    /// List available headphones from the AutoEQ database
    /// </summary>
    public static async Task<List<(string Name, string Source)>> ListHeadphonesAsync(string? query = null)
    {
        var pythonPath = FindPythonToUse();
        if (pythonPath == null) return new List<(string, string)>();

        string scriptPath = File.Exists(BundledScript) ? BundledScript : "";
        if (string.IsNullOrEmpty(scriptPath)) return new List<(string, string)>();

        string args = $"\"{scriptPath}\" --list --json";
        if (!string.IsNullOrEmpty(query))
            args += $" --query \"{query}\"";

        var result = await RunPythonAsync(pythonPath, args);
        if (result.ExitCode != 0) return new List<(string, string)>();

        try
        {
            var json = JsonDocument.Parse(result.Output);
            var list = new List<(string, string)>();
            foreach (var item in json.RootElement.EnumerateArray())
            {
                string name = item.GetProperty("name").GetString() ?? "";
                string source = item.GetProperty("source").GetString() ?? "";
                list.Add((name, source));
            }
            return list;
        }
        catch { return new List<(string, string)>(); }
    }

    // ── Python detection and execution ──

    private static string? FindPythonToUse()
    {
        // Bundled first
        if (File.Exists(BundledPythonExe))
            return BundledPythonExe;
        // Then system
        return FindSystemPython();
    }

    private static string? FindSystemPython()
    {
        if (_pythonPath != null && File.Exists(_pythonPath))
            return _pythonPath;

        string user = Environment.UserName;

        // Check common install paths
        foreach (var template in PythonPaths)
        {
            string path = template.Replace("{USER}", user);
            if (File.Exists(path))
            {
                _pythonPath = path;
                return path;
            }
        }

        // Try PATH
        try
        {
            var result = RunPython("python", "--version");
            if (result.ExitCode == 0)
            {
                // Find the full path
                var findResult = RunCommand("where.exe", "python");
                if (findResult.ExitCode == 0)
                {
                    string firstLine = findResult.Output.Split('\n').FirstOrDefault()?.Trim() ?? "";
                    if (File.Exists(firstLine))
                    {
                        _pythonPath = firstLine;
                        return firstLine;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    private static (string Output, string Error, int ExitCode) RunPython(string python, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi)!;
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(30000);
            return (stdout, stderr, proc.ExitCode);
        }
        catch (Exception ex)
        {
            return ("", ex.Message, -1);
        }
    }

    private static async Task<(string Output, string Error, int ExitCode)> RunPythonAsync(string python, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = python,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi)!;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            
            // Wait up to 2 minutes for profile generation (downloads + processing)
            await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));

            string stdout = await stdoutTask;
            string stderr = await stderrTask;
            return (stdout, stderr, proc.ExitCode);
        }
        catch (TimeoutException)
        {
            return ("", "Python process timed out after 2 minutes", -1);
        }
        catch (Exception ex)
        {
            return ("", ex.Message, -1);
        }
    }

    private static (string Output, string Error, int ExitCode) RunCommand(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi)!;
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(10000);
            return (stdout, stderr, proc.ExitCode);
        }
        catch (Exception ex)
        {
            return ("", ex.Message, -1);
        }
    }
}
