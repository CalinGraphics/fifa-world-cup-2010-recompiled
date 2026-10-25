using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Fifawc10Launcher;

/// <summary>The player's choices. Saved as launcher.json; turned into fifawc10.toml for the game.</summary>
public sealed class Settings
{
    public string GamePath { get; set; } = "";
    public string Display { get; set; } = "Borderless";    // Window, Borderless, Fullscreen
    public int WindowWidth { get; set; } = 1920;
    public int WindowHeight { get; set; } = 1080;
    public int RenderScale { get; set; } = 2;              // 1 = 720p, 2 = 1440p, 3 = 2160p (4K)
    public bool VSync { get; set; } = true;
    public int Anisotropic { get; set; } = 5;              // -1 original, 3 = 4x, 4 = 8x, 5 = 16x
    public string Controls { get; set; } = "Controller";   // Controller, Keyboard
    public int FrameRateLimit { get; set; } = 60;          // 60 = the game's own pacing, 30 = locked to 30
    public bool ShowFps { get; set; } = false;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);

    /// <summary>Primary monitor size in pixels.</summary>
    public static (int W, int H) Screen => (Math.Max(1280, GetSystemMetrics(0)), Math.Max(720, GetSystemMetrics(1)));

    /// <summary>Defaults that fit this PC: the monitor's size, and a render scale that matches it.</summary>
    public static Settings ForThisPc()
    {
        var (w, h) = Screen;
        return new Settings
        {
            WindowWidth = w,
            WindowHeight = h,
            RenderScale = h >= 2000 ? 3 : 2,
        };
    }

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
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(JsonPath)) ?? ForThisPc();
        }
        catch { /* a broken file falls back to defaults */ }
        return ForThisPc();
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
        sb.AppendLine("# 0: the game's own pacing (60 in matches, 30 in menus and cutscenes)");
        sb.AppendLine(string.Create(c, $"frame_rate_limit = {(FrameRateLimit >= 60 ? 0 : FrameRateLimit)}"));
        sb.AppendLine($"show_fps = {Bool(ShowFps)}");
        sb.AppendLine();
        sb.AppendLine("# Graphics: the game renders at 1280x720 times this scale");
        sb.AppendLine(string.Create(c, $"resolution_scale = {RenderScale}"));
        sb.AppendLine(string.Create(c, $"anisotropic_override = {Anisotropic}"));
        sb.AppendLine("# The game samples textures whose fetch constants say \"invalid\"; without this they render black");
        sb.AppendLine("gpu_allow_invalid_fetch_constants = true");
        sb.AppendLine("# Full-screen passes drawn with clipping off get their real size (no black screens)");
        sb.AppendLine("execute_unclipped_draw_vs_on_cpu = true");
        sb.AppendLine();
        sb.AppendLine("# Input");
        sb.AppendLine($"mnk_mode = {Bool(Controls == "Keyboard")}");
        return sb.ToString();
    }
}
