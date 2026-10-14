using System.Text.Json;

namespace Halls.Game;

public enum ViewMode { Classic2D, TopDown3D }

/// <summary>What is kept between sessions (%APPDATA%\HallsOfTheThings\settings.json).</summary>
public sealed class Settings
{
    public bool ModernKeys { get; set; } = true;
    /// <summary>The screen follows the man, the man always in the middle (FOL% of the RISC OS version).</summary>
    public bool FollowMan { get; set; } = true;
    public bool Statistics { get; set; } = true;

    /// <summary>The strong Things in bright red (not in 1983, where they look like the others).</summary>
    public bool StrongInRed { get; set; } = true;
    public ViewMode View { get; set; } = ViewMode.Classic2D;
    public int CameraPreset { get; set; }

    /// <summary>
    /// Cycles of GameLoop per second. The Spectrum had no clock: about 300-450
    /// (docs/halls-analisi.md §9.1).
    /// </summary>
    public int CyclesPerSecond { get; set; } = 300;

    /// <summary>In 1983 the high score lived only in memory; here it is kept.</summary>
    public int HighScore { get; set; } = 100;
    public string HighName { get; set; } = "Kickaha";
    public bool Fullscreen { get; set; }

    /// <summary>The window as it was left: position and size when not maximized (missing = the default 1280x1024).</summary>
    public int[]? Window { get; set; }
    public bool Maximized { get; set; }

    /// <summary>The zoom of the 3D view, 1 (the classic screen) to 2.</summary>
    public float Zoom { get; set; } = 1;

    /// <summary>The redefined keys of the modern set: action -> raylib key name (missing = default).</summary>
    public Dictionary<string, string> Keys { get; set; } = new();

    public static string Folder => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HallsOfTheThings");

    static string Path => System.IO.Path.Combine(Folder, "settings.json");

    /// <summary>The game stopped with F5 or by closing the window, taken again (and deleted) at the next start.</summary>
    public static string SavedGamePath => System.IO.Path.Combine(Folder, "suspended.sav");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(Path)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path), Json) ?? new();
        }
        catch (Exception) { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, Json));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
