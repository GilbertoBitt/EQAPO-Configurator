using System.IO;
using System.Text.Json;

namespace EQAPO_Configurator.Services;

public sealed class AppSettings
{
    public string HeadphoneLayerFilename { get; set; } = "";
    public string HeadphoneLayerName { get; set; } = "";
    public string OverlayBarMode { get; set; } = "SideBySide";
    public bool OverlayShowSpectrum { get; set; } = true;
}

public static class AppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EQAPO-Configurator", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
