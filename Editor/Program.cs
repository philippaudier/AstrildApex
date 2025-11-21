using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;
using Editor.Logging;
using Engine.Core;
using Engine.UI;
using Editor.ImGuiBackend;
using Editor.Panels;
using Editor.Rendering;
using Editor.SceneManagement;
using Engine.Utils;

namespace Editor;

public static class Program
{
    public static Editor.Scripting.ScriptCompiler? ScriptCompiler { get; private set; }
    public static Editor.Scripting.ScriptHost? ScriptHost { get; private set; }
    static void InitScripting()
    {
        var scriptsDir = Editor.State.ProjectPaths.ScriptsDir; // "Editor/Assets/Scripts"

        // Create ScriptHost first
        ScriptHost = new Editor.Scripting.ScriptHost();

        // Create ScriptCompiler and connect event BEFORE it starts compiling
        ScriptCompiler = new Editor.Scripting.ScriptCompiler(scriptsDir);
        ScriptCompiler.OnReloaded += asm => ScriptHost.BindAssembly(asm);

        // Force initial binding in case compilation already happened
        if (ScriptCompiler.CurrentAssembly != null)
        {
            ScriptHost.BindAssembly(ScriptCompiler.CurrentAssembly);
        }
    }
    private static GameWindow? _gameWindow;

    public static GameWindow? GameWindow => _gameWindow;

    public static void UpdateWindowTitle()
    {
        if (_gameWindow != null)
        {
            var sceneName = SceneManager.CurrentSceneName;
            var modifiedIndicator = SceneManager.IsSceneModified ? "*" : "";
            var title = $"{EngineInfo.Name} Editor - {sceneName}{modifiedIndicator}";
            _gameWindow.Title = title;
        }
    }
    
    [STAThread] // Required for WinForms
    public static void Main(string[] args)
    {
        // If invoked with PMREM import arguments, run the importer and exit.
        // Usage example:
        // dotnet run --project Editor/Editor.csproj -- --pmrem --cmgen "C:\path\cmgen.exe" --input "C:\path\env.hdr" --out "Generated/Env" --size 512
        if (args != null && args.Length > 0 && Array.Exists(args, a => string.Equals(a, "--pmrem", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var ret = Editor.Tools.PMREMImporter.RunFromArgs(args);
                Environment.Exit(ret);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PMREM Import failed: {ex.Message}\n{ex.StackTrace}");
                Environment.Exit(10);
            }
        }
        // CRITICAL FIX: Set working directory to executable location so shader files are found
        // When using 'dotnet run', the working directory is the project root, not bin/Debug/
        var exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(exeDir))
        {
            System.IO.Directory.SetCurrentDirectory(exeDir);
            
            // Redirect Console output to the engine debug logger to keep the terminal clean.
            // TEMPORARILY DISABLED FOR DEBUGGING
            /*try
            {
                Console.SetOut(new Engine.Utils.ConsoleLogWriter());
                Console.SetError(new Engine.Utils.ConsoleLogWriter());
            }
            catch { }*/
            
            // Also write a short startup message to the engine log
            DebugLogger.Log($"[Program] Set working directory to: {exeDir}");
        }

        // Configure Serilog: route events to both the terminal and the in-editor Console panel.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.Sink(new ConsolePanelSink())
            .CreateLogger();

        Log.Information("{Name} Editor starting v{Version}", EngineInfo.Name, EngineInfo.Version);

        var native = new NativeWindowSettings()
        {
            Title = $"{EngineInfo.Name} Editor",
            ClientSize = new Vector2i(1280, 800),
            APIVersion = new Version(4, 6),
            Flags = ContextFlags.ForwardCompatible,
            StartFocused = true,
            StartVisible = true
        };

        using var game = new GameWindow(GameWindowSettings.Default, native);
        _gameWindow = game;
        // Apply persisted VSync preference immediately on the created window
        try
        {
            game.VSync = Editor.State.EditorSettings.VSync ? OpenTK.Windowing.Common.VSyncMode.On : OpenTK.Windowing.Common.VSyncMode.Off;
        }
        catch { }
        ImGuiController? imgui = null;
        ViewportRenderer? viewport = null;
        Editor.Utils.LoadingManager? loadingManager = null;

        game.Load += () =>
        {
            // Start profiling
            Editor.Utils.StartupProfiler.Start();

            Editor.Utils.StartupProfiler.BeginSection("OpenGL Initialization");
            GL.Enable(EnableCap.FramebufferSrgb);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);

            string gl = GL.GetString(StringName.Version) ?? "GL?";
            string gpu = GL.GetString(StringName.Renderer) ?? "GPU?";
            Log.Information("OpenGL: {gl} | GPU: {gpu}", gl, gpu);

            GL.ClearColor(0.08f, 0.08f, 0.10f, 1f);
            Editor.Utils.StartupProfiler.EndSection();

            // Sanitize any existing ImGui .ini files to avoid '##' sequences in Window names
            // which some parsers/tools treat as comment markers and can corrupt parsing.
            Editor.Utils.StartupProfiler.BeginSection("ImGui Sanitization");
            try
            {
                SanitizeImGuiIni(System.IO.Path.Combine(Environment.CurrentDirectory, "imgui.ini"));
                SanitizeImGuiIni(System.IO.Path.Combine(Environment.CurrentDirectory, "Editor", "imgui.ini"));
            }
            catch { }
            Editor.Utils.StartupProfiler.EndSection();

            Editor.Utils.StartupProfiler.BeginSection("ImGui Controller Init");
            imgui = new ImGuiController(game);
            Editor.Utils.StartupProfiler.EndSection();

            // Initialize ProgressManager for use throughout the editor
            Editor.UI.ProgressManager.Initialize(imgui);

            // Initialize loading manager and show progress popup
            loadingManager = new Editor.Utils.LoadingManager(game, imgui);
            loadingManager.Start();
            loadingManager.UpdateStep("Initializing OpenGL...");

            // Initialize theme system with saved theme
            loadingManager.UpdateStep("Loading editor theme...");
            Editor.Utils.StartupProfiler.BeginSection("Theme Manager Init");
            var savedTheme = Editor.State.EditorSettings.ThemeName;
            Log.Information("Initializing theme system with theme: {Theme}", savedTheme);
            Editor.Themes.ThemeManager.Initialize(savedTheme);
            Editor.Utils.StartupProfiler.EndSection();

            loadingManager.UpdateStep("Creating viewport renderer...");
            Editor.Utils.StartupProfiler.BeginSection("Viewport Renderer Init");
            viewport = new ViewportRenderer();
            EditorUI.MainViewport.Renderer = viewport;
            Editor.Utils.StartupProfiler.EndSection();

            loadingManager.UpdateStep("Configuring SSAO settings...");
            // Load saved SSAO settings from EditorSettings
            // NOTE: SSAO settings load removed - SSAO is now configured via GlobalEffects component
            // Editor.Utils.StartupProfiler.BeginSection("SSAO Settings Load");
            // var loadedSSAO = Editor.State.EditorSettings.SSAOSettings;
            // Console.WriteLine($"[Program] Loading SSAO settings: Radius={loadedSSAO.Radius}, Intensity={loadedSSAO.Intensity}, SampleCount={loadedSSAO.SampleCount}, Enabled={loadedSSAO.Enabled}");
            // viewport.SSAOSettings = loadedSSAO;
            // Editor.Utils.StartupProfiler.EndSection();

            // Ensure viewport subscribes to material changes for real-time updates
            Engine.Assets.AssetDatabase.MaterialSaved += viewport.OnMaterialSaved;

            // --- ScriptCompiler/ScriptHost init ---
            loadingManager.UpdateStep("Initializing script compiler...");
            Editor.Utils.StartupProfiler.BeginSection("Script System Init");
            InitScripting();
            Editor.Utils.StartupProfiler.EndSection();

            // --- Initialize AudioEngine ---
            loadingManager.UpdateStep("Initializing audio engine...");
            Editor.Utils.StartupProfiler.BeginSection("AudioEngine Init");
            try
            {
                Engine.Audio.Core.AudioEngine.Instance.Initialize();
                Log.Information("AudioEngine initialized successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize AudioEngine");
            }
            Editor.Utils.StartupProfiler.EndSection();

            // --- Initialize AssetDatabase BEFORE loading scene ---
            loadingManager.UpdateStep("Loading asset database...");
            Editor.Utils.StartupProfiler.BeginSection("AssetDatabase Init");
            System.IO.Directory.CreateDirectory(Editor.State.ProjectPaths.AssetsDir);
            Engine.Assets.AssetDatabase.Initialize(Editor.State.ProjectPaths.AssetsDir);
            Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial();
            Log.Information("AssetDatabase initialized");
            Editor.Utils.StartupProfiler.EndSection();

            // Auto-load last scene if available
            loadingManager.UpdateStep("Loading scene...");
            Editor.Utils.StartupProfiler.BeginSection("Scene Loading");
            SceneManager.LoadLastSceneOnStartup();
            Editor.Utils.StartupProfiler.EndSection();

            // Update window title initially
            UpdateWindowTitle();

            // --- InputSystem init ---
            loadingManager.UpdateStep("Setting up input system...");
            Editor.Utils.StartupProfiler.BeginSection("Input System Init");
            Engine.Input.InputManager.Initialize(game);
            Engine.Input.InputManager.Instance?.SetupDefaultPlayerControls();
            Editor.State.InputSettings.ApplySettingsToInputManager();
            Editor.Utils.StartupProfiler.EndSection();

            // --- PostProcessManager init ---
            loadingManager.UpdateStep("Initializing post-processing...");
            Editor.Utils.StartupProfiler.BeginSection("PostProcess Manager Init");
            Log.Information("About to initialize PostProcessManager...");
            Engine.Rendering.PostProcessManager.Initialize();
            Log.Information("PostProcessManager initialized successfully");
            Editor.Utils.StartupProfiler.EndSection();

            // Complete loading
            loadingManager.Complete();

            // Print profiling report
            Editor.Utils.StartupProfiler.PrintReport();
            
            // Configurer le callback pour recharger les bindings en Play Mode
            Engine.Input.InputManager.ReloadPersistedBindings = () =>
            {
                Editor.State.InputSettings.ApplySettingsToInputManager();
            };

            // OS-level drag & drop: import external files/folders into Assets
            game.FileDrop += (FileDropEventArgs e) =>
            {
                try
                {
                    if (e.FileNames != null && e.FileNames.Length > 0)
                    {
                        Editor.Panels.AssetsPanel.EnqueueExternalImport(e.FileNames);
                    }
                }
                catch { /* ignore import errors at event time */ }
            };
        };

        game.Unload += () =>
        {
            // Unsubscribe from material events
            if (viewport != null)
            {
                Engine.Assets.AssetDatabase.MaterialSaved -= viewport.OnMaterialSaved;
            }
            viewport?.Dispose();
        };

    game.UpdateFrame += (FrameEventArgs e) =>
        {
            // Démarrer une nouvelle frame ImGui
            imgui!.NewFrame((float)e.Time);

            // Intégration côté ImGui (important) - après NewFrame, avant InputManager.Update
            var io = ImGuiNET.ImGui.GetIO();
            // If cursor is locked or confined, force ImGui to not capture the mouse
            // so that gameplay scripts (CameraController, etc.) receive mouse deltas.
            // Cursor state is now managed by gameplay scripts (CursorStateController).
            bool wantMouse = io.WantCaptureMouse;
            try {
                if ((Engine.Input.Cursor.isLocked || Engine.Input.Cursor.isConfined) && Editor.PlayMode.IsInPlayMode)
                    wantMouse = false;
            } catch { }
            Engine.Input.InputManager.Instance?.SetImGuiCapture(
                io.WantCaptureKeyboard,
                wantMouse
            );

            // Update InputSystem
            Engine.Input.InputManager.Instance?.Update();

            // Update AudioEngine
            Engine.Audio.Core.AudioEngine.Instance.Update((float)e.Time);

            // Mettre à jour la simulation du jeu en Play Mode
            PlayMode.UpdateSimulation((float)e.Time);

            // UI Editor
            EditorUI.DrawDockspaceAndMainMenu();
            EditorUI.DrawDefaultLayoutWindows();
        };

        game.RenderFrame += (FrameEventArgs e) =>
        {
            // Backbuffer pour ImGui
            GL.Viewport(0, 0, game.ClientSize.X, game.ClientSize.Y);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Rendu ImGui (ViewportPanel appelera Renderer.RenderScene() avant d'afficher l'image)
            imgui!.Render();

            game.SwapBuffers();

            // After the ImGui frame has been rendered, run any deferred actions
            // We run them here because ImGuiController.Render() will have finished
            // the current ImGui frame (so ForceRender / NewFrame may be used safely).
            try
            {
                Editor.Utils.DeferredActions.ProcessAll();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeferredActions.ProcessAll failed");
            }
        };

        game.Run();
    }

    private static void SanitizeImGuiIni(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return;
            var text = System.IO.File.ReadAllText(path);
            if (!text.Contains("##")) return;
            var newText = text.Replace("##", "_");
            System.IO.File.WriteAllText(path, newText);
            Log.Information("Sanitized ImGui ini: {Path}", path);
        }
        catch (Exception ex)
        {
            try { Log.Warning(ex, "Failed to sanitize ini {Path}", path); } catch { }
        }
    }
}
