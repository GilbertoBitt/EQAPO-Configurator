using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace EQAPO_Configurator.Services;

public sealed record EqualizerApoInstallation(string InstallPath, string ConfigPath)
{
    public string? SelectorPath
    {
        get
        {
            string modern = Path.Combine(InstallPath, "DeviceSelector.exe");
            if (File.Exists(modern)) return modern;
            string legacy = Path.Combine(InstallPath, "Configurator.exe");
            return File.Exists(legacy) ? legacy : null;
        }
    }
}

public static class EqualizerApoService
{
    private const string RegistryPath = @"SOFTWARE\EqualizerAPO";

    public static EqualizerApoInstallation? ResolveInstallation()
    {
        string? installPath = null;
        string? configPath = null;

        try
        {
            using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = localMachine.OpenSubKey(RegistryPath);
            installPath = key?.GetValue("InstallPath") as string;
            configPath = key?.GetValue("ConfigPath") as string;
        }
        catch { }

        installPath = NormalizeDirectory(installPath)
            ?? NormalizeDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO"));
        if (installPath == null) return null;

        configPath = NormalizeDirectory(configPath)
            ?? NormalizeDirectory(Path.Combine(installPath, "config"));
        if (configPath == null) return null;

        return new EqualizerApoInstallation(installPath, configPath);
    }

    public static Process? OpenDeviceSelector()
    {
        EqualizerApoInstallation? installation = ResolveInstallation();
        string? selector = installation?.SelectorPath;
        if (selector == null) return null;

        return Process.Start(new ProcessStartInfo
        {
            FileName = selector,
            WorkingDirectory = installation!.InstallPath,
            UseShellExecute = true,
            Verb = "runas"
        });
    }

    private static string? NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        return Directory.Exists(expanded) ? Path.GetFullPath(expanded) : null;
    }
}
