using System;
using System.IO;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;

namespace Game;

/// <summary>
/// Astrild Game Runtime - Standalone game player without editor UI
/// Double-click to launch - opens a window even without a scene
/// </summary>
public static class Program
{
    private static GameWindow? _window;
    private static GameRuntime? _runtime;
    private static GameConfig? _config;

    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("[Game] Astrild Game Runtime starting...");

        // Set working directory to executable location
        var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exeDir))
        {
            Directory.SetCurrentDirectory(exeDir);
            Console.WriteLine($"[Game] Working directory: {exeDir}");
        }

        // Load game config
        _config = GameConfig.Load();

        // Parse command line arguments (override config)
        string? sceneToLoad = null;
        bool? fullscreenOverride = null;
        int? widthOverride = null;
        int? heightOverride = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--scene" when i + 1 < args.Length:
                    sceneToLoad = args[++i];
                    break;
                case "--fullscreen":
                case "-f":
                    fullscreenOverride = true;
                    break;
                case "--width" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var w)) widthOverride = w;
                    break;
                case "--height" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var h)) heightOverride = h;
                    break;
                case "--windowed":
                case "-w":
                    fullscreenOverride = false;
                    break;
            }
        }

        // Use config values, overridden by command line
        int width = widthOverride ?? _config.WindowWidth;
        int height = heightOverride ?? _config.WindowHeight;
        bool fullscreen = fullscreenOverride ?? _config.Fullscreen;
        string windowTitle = _config.WindowTitle;

        // Determine scene to load
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            // First try config's startup scene
            sceneToLoad = _config.GetStartupScenePath();
        }

        // Fallback to searching for default scene
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            sceneToLoad = FindDefaultScene();
        }

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Console.WriteLine("[Game] No scene found - will show empty window");
            Console.WriteLine("[Game] Tip: Place .scene files in Assets/Scenes/ folder");
        }
        else
        {
            Console.WriteLine($"[Game] Scene to load: {sceneToLoad}");
        }

        Console.WriteLine($"[Game] Resolution: {width}x{height}, Fullscreen: {fullscreen}");

        // Create window - ALWAYS open window even without scene
        try
        {
            var nativeSettings = new NativeWindowSettings
            {
                Title = windowTitle,
                ClientSize = new Vector2i(width, height),
                APIVersion = new Version(4, 1), // More compatible than 4.6
                Flags = ContextFlags.ForwardCompatible,
                StartFocused = true,
                StartVisible = true,
                WindowState = fullscreen ? WindowState.Fullscreen : WindowState.Normal
            };

            using var window = new GameWindow(GameWindowSettings.Default, nativeSettings);
            _window = window;

            // Set window icon if configured (safely)
            try
            {
                if (_config != null && !string.IsNullOrEmpty(_config.IconPath))
                {
                    var iconPath = Path.Combine(Directory.GetCurrentDirectory(), _config.IconPath);
                    if (File.Exists(iconPath))
                    {
                        var icon = LoadWindowIcon(iconPath);
                        if (icon != null)
                        {
                            window.Icon = icon;
                            Console.WriteLine($"[Game] Window icon set: {_config.IconPath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Game] Warning: Icon file not found: {iconPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Game] Warning: Failed to set icon: {ex.Message}");
            }

            // Create game runtime (can work without scene)
            _runtime = new GameRuntime(window, sceneToLoad);

            window.Load += () =>
            {
                Console.WriteLine("[Game] Window loaded, initializing...");
                _runtime.Initialize();
                Console.WriteLine("[Game] Initialization complete!");
            };

            window.UpdateFrame += (FrameEventArgs e) =>
            {
                _runtime.Update((float)e.Time);
            };

            window.RenderFrame += (FrameEventArgs e) =>
            {
                _runtime.Render();
                window.SwapBuffers();
            };

            window.Unload += () =>
            {
                Console.WriteLine("[Game] Shutting down...");
                _runtime.Shutdown();
            };

            window.Resize += (ResizeEventArgs e) =>
            {
                _runtime.OnResize(e.Width, e.Height);
            };

            // Handle ESC to quit
            window.KeyDown += (KeyboardKeyEventArgs e) =>
            {
                if (e.Key == OpenTK.Windowing.GraphicsLibraryFramework.Keys.Escape)
                {
                    window.Close();
                }
            };

            Console.WriteLine("[Game] Starting main loop...");
            window.Run();
            Console.WriteLine("[Game] Exited normally.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Game] FATAL ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

    private static string? FindDefaultScene()
    {
        // Try common default scene names
        var possibleScenes = new[]
        {
            "Assets/Scenes/Main.scene",
            "Assets/Scenes/MainMenu.scene",
            "Assets/Scenes/Game.scene",
            "Assets/Main.scene",
            "Main.scene"
        };

        foreach (var scene in possibleScenes)
        {
            if (File.Exists(scene))
            {
                return scene;
            }
        }

        // Look in Assets folder
        var assetsDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
        if (Directory.Exists(assetsDir))
        {
            try
            {
                var scenes = Directory.GetFiles(assetsDir, "*.scene", SearchOption.AllDirectories);
                if (scenes.Length > 0)
                {
                    return scenes[0];
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>
    /// Load a window icon from a file (.ico, .png, .jpg, etc.)
    /// </summary>
    private static WindowIcon? LoadWindowIcon(string path)
    {
        try
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();

            // For .ico files, try to parse manually (simple ICO format)
            if (extension == ".ico")
            {
                return LoadWindowIconFromIco(path);
            }

            // For other image formats, use StbImageSharp
            using var fs = File.OpenRead(path);
            var image = ImageResult.FromStream(fs, ColorComponents.RedGreenBlueAlpha);

            if (image != null && image.Data != null)
            {
                var iconImage = new Image(image.Width, image.Height, image.Data);
                return new WindowIcon(iconImage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Game] Failed to load icon: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Load window icon from .ico file (simple ICO parser)
    /// </summary>
    private static WindowIcon? LoadWindowIconFromIco(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // ICO Header
            var reserved = br.ReadInt16(); // Should be 0
            var type = br.ReadInt16(); // 1 = ICO, 2 = CUR
            var count = br.ReadInt16(); // Number of images

            if (type != 1 || count == 0)
            {
                Console.WriteLine("[Game] Invalid ICO file format");
                return null;
            }

            // Find the best image (largest size)
            int bestIndex = 0;
            int bestSize = 0;
            int bestOffset = 0;
            int bestDataSize = 0;

            for (int i = 0; i < count; i++)
            {
                var width = br.ReadByte();
                var height = br.ReadByte();
                var colorCount = br.ReadByte();
                var reserved2 = br.ReadByte();
                var planes = br.ReadInt16();
                var bitCount = br.ReadInt16();
                var dataSize = br.ReadInt32();
                var dataOffset = br.ReadInt32();

                // 0 means 256
                int w = width == 0 ? 256 : width;
                int h = height == 0 ? 256 : height;

                if (w * h > bestSize)
                {
                    bestSize = w * h;
                    bestIndex = i;
                    bestOffset = dataOffset;
                    bestDataSize = dataSize;
                }
            }

            // Seek to the best image data
            fs.Seek(bestOffset, SeekOrigin.Begin);

            // Check if it's a PNG (starts with PNG signature)
            var pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            var header = br.ReadBytes(4);
            fs.Seek(bestOffset, SeekOrigin.Begin);

            if (header.Length >= 4 && header[0] == pngSignature[0] && header[1] == pngSignature[1] &&
                header[2] == pngSignature[2] && header[3] == pngSignature[3])
            {
                // It's a PNG embedded in the ICO
                var pngData = br.ReadBytes(bestDataSize);
                using var pngStream = new MemoryStream(pngData);
                var image = ImageResult.FromStream(pngStream, ColorComponents.RedGreenBlueAlpha);

                if (image != null && image.Data != null)
                {
                    var iconImage = new Image(image.Width, image.Height, image.Data);
                    return new WindowIcon(iconImage);
                }
            }
            else
            {
                // It's a BMP - this is more complex, skip for now
                Console.WriteLine("[Game] ICO contains BMP data, use PNG-based ICO or PNG file instead");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Game] Failed to load .ico file: {ex.Message}");
        }

        return null;
    }
}
