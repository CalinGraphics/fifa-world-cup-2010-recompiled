using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Fifawc10Launcher;

/// <summary>The player's choices. Saved as launcher.json; turned into fifawc10.toml for the game.</summary>
public sealed class Settings
{
    public string GamePath { get; set; } = "";
    public string Display { get; set; } = "Window";      // Window, Borderless, Fullscreen
    public int WindowWidth { get; set; } = 1920;
    public int WindowHeight { get; set; } = 1080;
    public int RenderScale { get; set; } = 2;              // 1 = 720p, 2 = 1440p, 3 = 2160p (4K)
    public bool VSync { get; set; } = true;
    public int Anisotropic { get; set; } = 5;              // 5 = 16x
    public string Controls { get; set; } = "Controller";   // Controller, Keyboard

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Same rule as the game (data_folder.cpp): portable.txt next to the exe keeps everything there.</summary>
    public static string DataFolder
    {
        get
        {
            var exeDir = AppContext.BaseDirectory;
            var dir = File.Exists(Path.Combine(exeDir, "portable.txt"))
                ? Path.Combine(exeDir, "userdata")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                               "FIFA World Cup 2010 Recompiled");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    static string JsonPath => Path.Combine(DataFolder, "launcher.json");
    public static string GameConfigPath => Path.Combine(DataFolder, "fifawc10.toml");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(JsonPath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(JsonPath)) ?? new Settings();
        }
        catch { /* a broken file falls back to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        File.WriteAllText(JsonPath, JsonSerializer.Serialize(this, Json));
        File.WriteAllText(GameConfigPath, ToToml(), new UTF8Encoding(false));
    }

    static string Str(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string Bool(bool b) => b ? "true" : "false";

    /// <summary>The game's config file: flat cvar names, as the ReXGlue runtime reads them.</summary>
    public string ToToml()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("# Written by the launcher. Changes here are replaced the next time you press Play.");
        sb.AppendLine($"game_data_root = {Str(GamePath)}");
        sb.AppendLine();
        sb.AppendLine("# Display");
        sb.AppendLine($"fullscreen = {Bool(Display != "Window")}");
        sb.AppendLine($"fullscreen_exclusive = {Bool(Display == "Fullscreen")}");
        sb.AppendLine(string.Create(c, $"window_width = {WindowWidth}"));
        sb.AppendLine(string.Create(c, $"window_height = {WindowHeight}"));
        sb.AppendLine($"vsync = {Bool(VSync)}");
        sb.AppendLine();
        sb.AppendLine("# Graphics: the game renders at 1280x720 times this scale");
        sb.AppendLine(string.Create(c, $"resolution_scale = {RenderScale}"));
        sb.AppendLine(string.Create(c, $"anisotropic_override = {Anisotropic}"));
        sb.AppendLine();
        sb.AppendLine("# Input");
        sb.AppendLine($"mnk_mode = {Bool(Controls == "Keyboard")}");
        return sb.ToString();
    }
}
