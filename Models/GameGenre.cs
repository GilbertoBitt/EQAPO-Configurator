namespace EQAPO_Configurator.Models;

public enum GameGenre
{
    FPS,           // Counter-strike, Valorant, COD, Apex
    BattleRoyale,  // Fortnite, Warzone, PUBG
    RPG,           // Skyrim, Cyberpunk, Baldur's Gate
    Racing,        // Forza, Assetto, Dirt
    Horror,        // Resident Evil, Silent Hill, Dead Space
    MOBA,          // League, Dota 2, HotS
    Sports,        // FIFA, NBA 2K
    Action,        // Devil May Cry, God of War, Bayonetta
    Stealth,       // Hitman, Splinter Cell, MGS
    Strategy,      // StarCraft, Civ, Total War
    Music,         // Spotify, YouTube, casual listening
}

public static class GameGenreExtensions
{
    public static string GetDisplayName(this GameGenre genre) => genre switch
    {
        GameGenre.FPS => "FPS / Tactical Shooter",
        GameGenre.BattleRoyale => "Battle Royale",
        GameGenre.RPG => "RPG / Open World",
        GameGenre.Racing => "Racing / Sim",
        GameGenre.Horror => "Horror / Survival",
        GameGenre.MOBA => "MOBA",
        GameGenre.Sports => "Sports",
        GameGenre.Action => "Action / Adventure",
        GameGenre.Stealth => "Stealth",
        GameGenre.Strategy => "Strategy / RTS",
        GameGenre.Music => "Music / Media",
        _ => genre.ToString(),
    };

    public static string GetDescription(this GameGenre genre) => genre switch
    {
        GameGenre.FPS => "Footsteps, reloads, directional audio, weapon handling",
        GameGenre.BattleRoyale => "Footsteps at distance, ambient awareness, vehicle detection",
        GameGenre.RPG => "Wide soundstage, ambient immersion, dialogue clarity",
        GameGenre.Racing => "Engine roar, tire grip, environmental detail",
        GameGenre.Horror => "Ambient tension, spatial creaks, distant sounds",
        GameGenre.MOBA => "Ability cues, pings, announcer, team callouts",
        GameGenre.Sports => "Commentary, crowd ambience, ball impact",
        GameGenre.Action => "Impact hits, music balance, cinematic punches",
        GameGenre.Stealth => "Enemy footsteps, detection cues, environmental sounds",
        GameGenre.Strategy => "Unit callouts, ambient battle, interface sounds",
        GameGenre.Music => "Balanced, warm, enjoyable for long listening",
        _ => "General audio profile",
    };
}
