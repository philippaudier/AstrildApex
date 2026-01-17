using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game;

/// <summary>
/// Game configuration - specifies startup scene and window settings
/// Loaded from game.config in the game's directory
/// </summary>
public class GameConfig
{
    private const string CONFIG_FILE_NAME = "game.config";

    /// <summary>
    /// Path to the startup scene (relative to Assets folder)
    /// </summary>
    [JsonPropertyName("startupScene")]
    public string StartupScene { get; set; } = "";

    /// <summary>
    /// List of all scenes included in the build (in order)
    /// </summary>
    [JsonPropertyName("scenes")]
    public List<string> Scenes { get; set; } = new();

    /// <summary>
    /// Window title
    /// </summary>
    [JsonPropertyName("windowTitle")]
    public string WindowTitle { get; set; } = "Astrild Game";

    /// <summary>
    /// Default window width
    /// </summary>
    [JsonPropertyName("windowWidth")]
    public int WindowWidth { get; set; } = 1280;

    /// <summary>
    /// Default window height
    /// </summary>
    [JsonPropertyName("windowHeight")]
    public int WindowHeight { get; set; } = 720;

    /// <summary>
    /// Start in fullscreen mode
    /// </summary>
    [JsonPropertyName("fullscreen")]
    public bool Fullscreen { get; set; } = false;

    /// <summary>
    /// Enable VSync
    /// </summary>
    [JsonPropertyName("vsync")]
    public bool VSync { get; set; } = true;

    /// <summary>
    /// Product name
    /// </summary>
    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "AstrildGame";

    /// <summary>
    /// Product version
    /// </summary>
    [JsonPropertyName("productVersion")]
    public string ProductVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Company name
    /// </summary>
    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = "";

    /// <summary>
    /// Path to window icon (.ico file)
    /// </summary>
    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = "";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Load config from the current directory
    /// </summary>
    public static GameConfig Load()
    {
        return Load(Directory.GetCurrentDirectory());
    }

    /// <summary>
    /// Load config from a specific directory
    /// </summary>
    public static GameConfig Load(string directory)
    {
        var configPath = Path.Combine(directory, CONFIG_FILE_NAME);

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"[GameConfig] No config file found at {configPath}, using defaults");
            return new GameConfig();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<GameConfig>(json, _jsonOptions);
            if (config != null)
            {
                Console.WriteLine($"[GameConfig] Loaded config: startup={config.StartupScene}, {config.Scenes.Count} scenes");
                return config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameConfig] Failed to load config: {ex.Message}");
        }

        return new GameConfig();
    }

    /// <summary>
    /// Save config to a specific directory
    /// </summary>
    public void Save(string directory)
    {
        var configPath = Path.Combine(directory, CONFIG_FILE_NAME);

        try
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, _jsonOptions);
            File.WriteAllText(configPath, json);
            Console.WriteLine($"[GameConfig] Saved config to {configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameConfig] Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the full path to the startup scene
    /// </summary>
    public string? GetStartupScenePath()
    {
        if (string.IsNullOrEmpty(StartupScene))
            return null;

        // If absolute path, return as-is
        if (Path.IsPathRooted(StartupScene))
            return StartupScene;

        // Try relative to Assets folder
        var assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", StartupScene);
        if (File.Exists(assetsPath))
            return assetsPath;

        // Try relative to current directory
        var relativePath = Path.Combine(Directory.GetCurrentDirectory(), StartupScene);
        if (File.Exists(relativePath))
            return relativePath;

        return null;
    }
}
