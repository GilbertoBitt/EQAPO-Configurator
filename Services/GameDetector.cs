using System.Diagnostics;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public static class GameDetector
{
    public static string? DetectRunningGame(List<GameProfile> profiles)
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string procName = proc.ProcessName.ToLower() + ".exe";
                    foreach (var profile in profiles)
                    {
                        if (procName == profile.GameExe.ToLower())
                        {
                            return profile.GameExe;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

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
