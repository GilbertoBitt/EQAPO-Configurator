using System.IO;
using System.Text.Json;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator.Services;

public static class ProfileManager
{
    private static readonly string ProfilesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQAPO-Configurator", "profiles");

    private static readonly string ProfilesIndex = Path.Combine(ProfilesDir, "profiles.json");

    public static void EnsureDirectory()
    {
        Directory.CreateDirectory(ProfilesDir);
    }

    public static List<GameProfile> LoadAll()
    {
        EnsureDirectory();

        if (!File.Exists(ProfilesIndex))
        {
            var defaults = CreateDefaults();
            SaveAll(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(ProfilesIndex);
            var profiles = JsonSerializer.Deserialize<List<GameProfile>>(json, GetOptions());
            return profiles ?? CreateDefaults();
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public static void SaveAll(List<GameProfile> profiles)
    {
        EnsureDirectory();
        var options = GetOptions();
        string json = JsonSerializer.Serialize(profiles, options);
        File.WriteAllText(ProfilesIndex, json);
    }

    public static void Save(GameProfile profile)
    {
        var profiles = LoadAll();
        var existing = profiles.FindIndex(p => p.Name == profile.Name);
        profile.LastModified = DateTime.Now;
        if (existing >= 0)
            profiles[existing] = profile;
        else
            profiles.Add(profile);
        SaveAll(profiles);
    }

    public static void Delete(string profileName)
    {
        var profiles = LoadAll();
        profiles.RemoveAll(p => p.Name == profileName);
        SaveAll(profiles);
    }

    private static List<GameProfile> CreateDefaults()
    {
        return new List<GameProfile>
        {
            GameProfile.CreateDefault("Call of Duty: BO7", "cod.exe", Models.GameGenre.FPS, "unified_bo7.txt"),
            GameProfile.CreateDefault("Warzone", "cod.exe", Models.GameGenre.FPS, "unified_warzone.txt"),
            GameProfile.CreateDefault("Counter-Strike 2", "cs2.exe", Models.GameGenre.FPS, "unified_cs2.txt"),
            GameProfile.CreateDefault("Valorant", "valorant.exe", Models.GameGenre.FPS, "unified_valorant.txt"),
            GameProfile.CreateDefault("Apex Legends", "r5apex.exe", Models.GameGenre.BattleRoyale, "unified_apex.txt"),
            GameProfile.CreateDefault("Fortnite", "fortniteclient-win64-shipping.exe", Models.GameGenre.BattleRoyale, "unified_fortnite.txt"),
            GameProfile.CreateDefault("Rainbow Six Siege", "r6_vulkan.exe", Models.GameGenre.FPS, "unified_r6siege.txt"),
            GameProfile.CreateDefault("PUBG", "tslgame.exe", Models.GameGenre.BattleRoyale, "unified_pubg.txt"),
            GameProfile.CreateDefault("The Witcher 3", "witcher3.exe", Models.GameGenre.RPG),
            GameProfile.CreateDefault("Forza Horizon 5", "forzahorizon5.exe", Models.GameGenre.Racing),
            GameProfile.CreateDefault("League of Legends", "league of legends.exe", Models.GameGenre.MOBA),
        };
    }

    private static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }
}
