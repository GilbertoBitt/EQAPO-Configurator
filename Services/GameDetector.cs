using System.Diagnostics;
using System.IO;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public static class GameDetector
{
    public static GameProfile? DetectRunningGame(List<GameProfile> profiles)
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    string procName = proc.ProcessName.ToLower();
                    foreach (var profile in profiles)
                    {
                        string exeName = Path.GetFileNameWithoutExtension(profile.GameExe).ToLower();
                        if (procName == exeName || procName == profile.GameExe.ToLower().Replace(".exe", ""))
                        {
                            return profile;
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
