using System.Diagnostics;
using System.IO;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public static class GameDetector
{
    public static List<GameProfile> DetectRunningGames(List<GameProfile> profiles)
    {
        var matches = new Dictionary<Guid, GameProfile>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string procName = proc.ProcessName;
                    string? processPath = null;
                    try { processPath = proc.MainModule?.FileName; } catch { }
                    foreach (var profile in profiles)
                    {
                        bool pathMatch = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
                            && !string.IsNullOrWhiteSpace(processPath)
                            && string.Equals(Path.GetFullPath(profile.ExecutablePath), Path.GetFullPath(processPath), StringComparison.OrdinalIgnoreCase);
                        bool nameMatch = string.Equals(procName, Path.GetFileNameWithoutExtension(profile.ExecutableName), StringComparison.OrdinalIgnoreCase);
                        if (pathMatch || nameMatch)
                        {
                            GameProfile selected = profiles.FirstOrDefault(p =>
                                p.ApplicationId == profile.ApplicationId && p.IsDefaultForApplication) ?? profile;
                            matches.TryAdd(selected.ApplicationId, selected);
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return matches.Values.ToList();
    }

    public static GameProfile? DetectRunningGame(List<GameProfile> profiles) => DetectRunningGames(profiles).FirstOrDefault();

    public static List<string> GetRunningProcesses()
    {
        var result = new List<string>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    result.Add(proc.ProcessName);
                }
                catch { }
            }
        }
        catch { }
        return result;
    }
}
