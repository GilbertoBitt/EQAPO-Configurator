using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EQAPO_Configurator.Services;

public class DeviceInfo
{
    public string DeviceName { get; set; } = "";
    public string ConnectionName { get; set; } = "";
    public string DeviceGuid { get; set; } = "";
    public bool IsSurroundSound { get; set; }
    public int ChannelCount { get; set; } = 2;
    public string AudioEndpoint { get; set; } = "";
    public bool IsHeadphones { get; set; } = true;
}

public static class DeviceDetector
{
    private static readonly string EqapoConfigPath = @"C:\Program Files\EqualizerAPO\config";
    private static readonly string ConfigTxtPath = Path.Combine(EqapoConfigPath, "config.txt");
    private static readonly string RegistryKey = @"SOFTWARE\EqualizerAPO";

    public static DeviceInfo DetectCurrentDevice()
    {
        var device = new DeviceInfo();

        // Try to read device info from EqualizerAPO config
        device.DeviceName = GetDeviceNameFromConfig();
        device.DeviceGuid = GetDeviceGuidFromConfig();
        device.ConnectionName = GetConnectionNameFromConfig();

        // Detect surround sound from config files
        device.IsSurroundSound = DetectSurroundFromConfig();
        device.ChannelCount = DetectChannelCount();
        device.IsHeadphones = DetectHeadphoneMode();

        // Get audio endpoint from registry
        device.AudioEndpoint = GetAudioEndpointFromRegistry();

        return device;
    }

    private static string GetDeviceNameFromConfig()
    {
        try
        {
            if (!File.Exists(ConfigTxtPath)) return "Unknown Device";

            string content = File.ReadAllText(ConfigTxtPath);

            // Look for Device: line and extract device name
            var deviceMatch = Regex.Match(content, @"Device:\s*(.+)", RegexOptions.IgnoreCase);
            if (deviceMatch.Success)
            {
                string devicePattern = deviceMatch.Groups[1].Value.Trim();
                if (devicePattern.Equals("all", StringComparison.OrdinalIgnoreCase))
                    return "All Devices";

                return devicePattern;
            }
        }
        catch { }
        return "Unknown Device";
    }

    private static string GetDeviceGuidFromConfig()
    {
        try
        {
            if (!File.Exists(ConfigTxtPath)) return "";

            string content = File.ReadAllText(ConfigTxtPath);
            var guidMatch = Regex.Match(content, @"Device:.*\{([A-F0-9-]+)\}", RegexOptions.IgnoreCase);
            if (guidMatch.Success)
                return "{" + guidMatch.Groups[1].Value + "}";
        }
        catch { }
        return "";
    }

    private static string GetConnectionNameFromConfig()
    {
        try
        {
            if (!File.Exists(ConfigTxtPath)) return "";

            string content = File.ReadAllText(ConfigTxtPath);

            // Check for common connection names
            if (content.Contains("headphone", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Headphone", StringComparison.OrdinalIgnoreCase))
                return "Headphones";
            if (content.Contains("Speaker", StringComparison.OrdinalIgnoreCase))
                return "Speakers";
        }
        catch { }
        return "Headphones";
    }

    private static bool DetectSurroundFromConfig()
    {
        try
        {
            // Check all config files for surround indicators
            string[] surroundKeywords = {
                "SL SR", "RL RR", "FL FR",
                "Copy: SL", "Copy: SR",
                "Channel: SL", "Channel: SR",
                "LFE", "multichannel",
                "surround", "hesuvi"
            };

            string[] configFiles = Directory.GetFiles(EqapoConfigPath, "*.txt");
            foreach (var file in configFiles)
            {
                string content = File.ReadAllText(file);
                foreach (var keyword in surroundKeywords)
                {
                    if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Check peace.txt which often has surround config
            string peacePath = Path.Combine(EqapoConfigPath, "peace.txt");
            if (File.Exists(peacePath))
            {
                string content = File.ReadAllText(peacePath);
                if (content.Contains("SL SR", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("RL RR", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static int DetectChannelCount()
    {
        try
        {
            string[] configFiles = Directory.GetFiles(EqapoConfigPath, "*.txt");
            foreach (var file in configFiles)
            {
                string content = File.ReadAllText(file);

                // Check for 7.1 surround indicators
                if (content.Contains("SL SR RL RR", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("FL FR SL SR RL RR", StringComparison.OrdinalIgnoreCase))
                    return 8;

                // Check for 5.1 surround
                if (content.Contains("SL SR", StringComparison.OrdinalIgnoreCase) &&
                    content.Contains("LFE", StringComparison.OrdinalIgnoreCase))
                    return 6;

                // Check for stereo with sub
                if (content.Contains("LFE", StringComparison.OrdinalIgnoreCase))
                    return 3;
            }
        }
        catch { }
        return 2;
    }

    private static bool DetectHeadphoneMode()
    {
        try
        {
            // Check peace.ini for device type
            string peaceIni = Path.Combine(EqapoConfigPath, "peace.ini");
            if (File.Exists(peaceIni))
            {
                string content = File.ReadAllText(peaceIni);
                if (content.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("headphone", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (content.Contains("Speaker", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("speaker", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Check if Device line contains headphone-related keywords
            if (File.Exists(ConfigTxtPath))
            {
                string content = File.ReadAllText(ConfigTxtPath);
                if (content.Contains("headphone", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return true; // Default to headphones for gaming
    }

    private static string GetAudioEndpointFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryKey);
            if (key != null)
            {
                var configPath = key.GetValue("ConfigPath") as string;
                if (!string.IsNullOrEmpty(configPath))
                    return configPath;
            }
        }
        catch { }
        return EqapoConfigPath;
    }

    public static string GetDeviceDisplayName(DeviceInfo device)
    {
        string name = device.DeviceName;
        if (name == "All Devices") return "All Devices (Stereo)";
        if (name == "Unknown Device") return "Detecting...";

        string surround = device.IsSurroundSound ? $" ({device.ChannelCount}.0 Surround)" : " (Stereo)";
        return name + surround;
    }

    public static List<string> GetSurroundTips(DeviceInfo device)
    {
        var tips = new List<string>();

        if (device.IsSurroundSound)
        {
            tips.Add("Surround sound detected in your config");
            tips.Add("For competitive FPS: Stereo is recommended over virtual surround");
            tips.Add("Game HRTF + stereo headphones gives better positional accuracy");
            tips.Add("Disable Windows Sonic, Dolby Atmos, DTS if using in-game HRTF");
            tips.Add("Consider using peace.txt for surround, unified_*.txt for stereo gaming");
        }
        else
        {
            tips.Add("Stereo mode — ideal for competitive gaming");
            tips.Add("Enable HRTF in-game (CS2, Valorant, Fortnite support it)");
            tips.Add("Disable any Windows spatial sound processing");
            tips.Add("For surround体验, use peace.txt with Peace GUI");
        }

        return tips;
    }
}
