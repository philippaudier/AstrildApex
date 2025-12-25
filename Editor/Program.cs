using System;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;
using System.Runtime.InteropServices;
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
        // Parse verbose CLI flag early so we can enable verbose logging and console redirection
        var verbose = args != null && Array.Exists(args, a => string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-v", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(exeDir))
        {
            System.IO.Directory.SetCurrentDirectory(exeDir);
            
            // If verbose requested, enable file-based verbose logging and forward console output
            try
            {
                DebugLogger.EnableVerbose = verbose;
            }
            catch { }

            if (verbose)
            {
                try
                {
                    var originalOut = Console.Out;
                    var originalErr = Console.Error;
                    Console.SetOut(new Engine.Utils.DualConsoleLogWriter(originalOut));
                    Console.SetError(new Engine.Utils.DualConsoleLogWriter(originalErr));
                    Console.WriteLine("[Program] Verbose logging enabled (console + astrild_debug.log)");
                }
                catch { }
            }
            else
            {
                // Also write a short startup message to the engine log (non-verbose)
                try { DebugLogger.Log($"[Program] Set working directory to: {exeDir}"); } catch { }
            }
        }

        // Configure Serilog: route events to both the terminal and the in-editor Console panel.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.Sink(new ConsolePanelSink())
            .CreateLogger();

        Log.Information("{Name} Editor starting v{Version}", EngineInfo.Name, EngineInfo.Version);

        // Indicate to the Engine that we're running inside the Editor so engine
        // components can avoid editor-only behaviors (e.g., PlayOnAwake auto-play).
        try
        {
            Engine.Core.RuntimeEnvironment.IsEditor = true;
        }
        catch { }

        var native = new NativeWindowSettings()
        {
            Title = $"{EngineInfo.Name} Editor",
            ClientSize = new Vector2i(1920, 1080),
            APIVersion = new Version(4, 6),
            Flags = ContextFlags.ForwardCompatible,
            StartFocused = true,
            StartVisible = true
        };

        // Try to create a multi-resolution .ico from the provided PNG and set it on the native window settings (best compatibility on Windows)
        try
        {
            var iconRel = System.IO.Path.Combine("Editor", "Assets", "Icons", "Logos", "editor_logo_1024.png");
            var iconPath = System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, iconRel);
            if (!System.IO.File.Exists(iconPath))
            {
                var alt = System.IO.Path.Combine(Environment.CurrentDirectory, iconRel);
                if (System.IO.File.Exists(alt)) iconPath = alt;
            }

            if (System.IO.File.Exists(iconPath))
            {
                try
                {
                    var logosDir = System.IO.Path.GetDirectoryName(iconPath) ?? System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, "Editor", "Assets", "Icons", "Logos");
                    var icoPath = System.IO.Path.Combine(logosDir, "editor_logo.ico");

                    // Build ICO with PNG-encoded entries (Windows supports PNG inside ICO)
                    var sizes = new int[] { 256, 48, 32, 16 };
                    var pngBytes = new System.Collections.Generic.List<byte[]>();

                    using (var srcBmp = new System.Drawing.Bitmap(iconPath))
                    {
                        foreach (var s in sizes)
                        {
                            using var bmp = new System.Drawing.Bitmap(srcBmp, new System.Drawing.Size(s, s));
                            using var ms = new System.IO.MemoryStream();
                            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            pngBytes.Add(ms.ToArray());
                        }
                    }

                    using (var fs = new System.IO.FileStream(icoPath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
                    using (var bw = new System.IO.BinaryWriter(fs))
                    {
                        // ICONDIR header
                        bw.Write((ushort)0); // reserved
                        bw.Write((ushort)1); // type = 1 for icons
                        bw.Write((ushort)pngBytes.Count);

                        int imageDataOffset = 6 + 16 * pngBytes.Count;

                        for (int i = 0; i < pngBytes.Count; i++)
                        {
                            var data = pngBytes[i];
                            int size = sizes[i];
                            bw.Write((byte)(size == 256 ? 0 : size)); // width (0 == 256)
                            bw.Write((byte)(size == 256 ? 0 : size)); // height
                            bw.Write((byte)0); // color palette
                            bw.Write((byte)0); // reserved
                            bw.Write((ushort)0); // color planes (for PNG entries set 0)
                            bw.Write((ushort)32); // bit count (32)
                            bw.Write((uint)data.Length); // bytes in resource
                            bw.Write((uint)imageDataOffset); // offset
                            imageDataOffset += data.Length;
                        }

                        // write image data
                        foreach (var d in pngBytes) bw.Write(d);
                    }

                    // Load generated .ico and assign to NativeWindowSettings if supported
                    if (System.IO.File.Exists(icoPath))
                    {
                        Log.Information("Editor icon: generated ico at {IcoPath}", icoPath);
                        Console.WriteLine($"[Program] Editor icon: generated ico at {icoPath}");
                        try
                        {
                            var sysIcon = new System.Drawing.Icon(icoPath);
                            var iconProp = typeof(NativeWindowSettings).GetProperty("Icon");
                            if (iconProp != null && iconProp.PropertyType.IsAssignableFrom(typeof(System.Drawing.Icon)))
                            {
                                try { iconProp.SetValue(native, sysIcon); Log.Information("Editor icon: assigned NativeWindowSettings.Icon"); Console.WriteLine("[Program] Editor icon: assigned NativeWindowSettings.Icon"); }
                                catch { Log.Warning("Editor icon: failed to assign NativeWindowSettings.Icon"); Console.WriteLine("[Program] Editor icon: failed to assign NativeWindowSettings.Icon"); }
                            }
                            else
                            {
                                Log.Information("Editor icon: NativeWindowSettings.Icon property not available, trying WindowIcon fallback");
                                Console.WriteLine("[Program] Editor icon: NativeWindowSettings.Icon property not available, trying WindowIcon fallback");
                                // Fallback: try OpenTK WindowIcon creation via reflection using the original PNG bitmap
                                var windowIconType = Type.GetType("OpenTK.Windowing.Common.WindowIcon, OpenTK")
                                                     ?? Type.GetType("OpenTK.Windowing.Common.WindowIcon")
                                                     ?? Type.GetType("OpenTK.Windowing.Common.WindowIcon, OpenTK.Windowing.Common");
                                if (windowIconType != null)
                                {
                                    using var bmp = new System.Drawing.Bitmap(iconPath);
                                    var loadMethod = windowIconType.GetMethod("Load", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                                  ?? windowIconType.GetMethod("FromBitmap", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                                  ?? windowIconType.GetMethod("LoadFromFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                    object? winIcon = null;
                                    if (loadMethod != null)
                                    {
                                        try { winIcon = loadMethod.Invoke(null, new object[] { bmp }); } catch { winIcon = null; }
                                    }
                                    if (winIcon == null)
                                    {
                                        var ctor = windowIconType.GetConstructor(new Type[] { typeof(System.Drawing.Bitmap) });
                                        if (ctor != null) try { winIcon = ctor.Invoke(new object[] { bmp }); } catch { winIcon = null; }
                                    }
                                    if (winIcon != null)
                                    {
                                        var prop = typeof(NativeWindowSettings).GetProperty("WindowIcon");
                                        if (prop != null && prop.PropertyType.IsAssignableFrom(windowIconType))
                                        {
                                            try { prop.SetValue(native, winIcon); Log.Information("Editor icon: assigned NativeWindowSettings.WindowIcon via reflection"); Console.WriteLine("[Program] Editor icon: assigned NativeWindowSettings.WindowIcon via reflection"); } catch { Log.Warning("Editor icon: failed to assign WindowIcon via reflection"); Console.WriteLine("[Program] Editor icon: failed to assign WindowIcon via reflection"); }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { Log.Warning(ex, "Editor icon: exception while assigning icon"); Console.WriteLine("[Program] Editor icon: exception while assigning icon: " + ex.Message); }
                    }
                }
                catch { }
            }
        }
        catch { }

        using var game = new GameWindow(GameWindowSettings.Default, native);
        _gameWindow = game;
        // Also attempt to set the icon on the created GameWindow instance (some OpenTK builds require runtime assignment)
        try
        {
            var iconRel2 = System.IO.Path.Combine("Editor", "Assets", "Icons", "Logos", "editor_logo_1024.png");
            var iconPath2 = System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, iconRel2);
            if (!System.IO.File.Exists(iconPath2))
            {
                var alt2 = System.IO.Path.Combine(Environment.CurrentDirectory, iconRel2);
                if (System.IO.File.Exists(alt2)) iconPath2 = alt2;
            }
            var logosDir2 = System.IO.Path.GetDirectoryName(iconPath2) ?? System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, "Editor", "Assets", "Icons", "Logos");
            var icoPath2 = System.IO.Path.Combine(logosDir2, "editor_logo.ico");
            if (System.IO.File.Exists(icoPath2))
            {
                try
                {
                    var icon = new System.Drawing.Icon(icoPath2);
                    var prop = game.GetType().GetProperty("Icon");
                    if (prop != null && prop.PropertyType.IsAssignableFrom(typeof(System.Drawing.Icon)))
                    {
                        try { prop.SetValue(game, icon); Log.Information("Editor icon: assigned GameWindow.Icon property"); Console.WriteLine("[Program] Editor icon: assigned GameWindow.Icon property"); }
                        catch { Log.Warning("Editor icon: failed to assign GameWindow.Icon"); Console.WriteLine("[Program] Editor icon: failed to assign GameWindow.Icon"); }
                    }
                }
                catch (Exception ex) { Log.Warning(ex, "Editor icon: failed to set GameWindow icon"); Console.WriteLine("[Program] Editor icon: failed to set GameWindow icon: " + ex.Message); }
            }
        }
        catch { }
        // Windows-specific fallback: send WM_SETICON to the native window handle
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var logosDir2 = System.IO.Path.GetDirectoryName(System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, "Editor", "Assets", "Icons", "Logos")) ?? Editor.State.ProjectPaths.ProjectRoot;
                var icoPath3 = System.IO.Path.Combine(logosDir2, "editor_logo.ico");
                if (System.IO.File.Exists(icoPath3))
                {
                    var hWnd = GetWindowHandle(game);
                    if (hWnd != IntPtr.Zero)
                    {
                        try
                        {
                            using var icon = new System.Drawing.Icon(icoPath3);
                            var hIcon = icon.Handle;
                            const uint WM_SETICON = 0x0080;
                            const int ICON_SMALL = 0;
                            const int ICON_BIG = 1;
                            SendMessage(hWnd, WM_SETICON, (IntPtr)ICON_SMALL, hIcon);
                            SendMessage(hWnd, WM_SETICON, (IntPtr)ICON_BIG, hIcon);
                            Log.Information("Editor icon: WM_SETICON posted to native window");
                            Console.WriteLine("[Program] Editor icon: WM_SETICON posted to native window");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Editor icon: WM_SETICON failed");
                            Console.WriteLine("[Program] Editor icon: WM_SETICON failed: " + ex.Message);
                        }
                    }
                    else
                    {
                        Log.Warning("Editor icon: could not locate native window handle for WM_SETICON");
                        Console.WriteLine("[Program] Editor icon: could not locate native window handle for WM_SETICON");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            try { Log.Warning(ex, "Editor icon: windows fallback raised"); } catch { }
        }
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
            Editor.Utils.ImGuiControllerManager.Initialize(imgui);
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
                if (Engine.Utils.DebugLogger.EnableVerbose) Engine.Utils.DebugLogger.Log("[AudioEngine] initialized successfully");
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

            // Physics system removed
            // Engine.Physics.CollisionSystem.ClearAll();

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

            // --- Initialize Engine Update Pipeline ---
            loadingManager.UpdateStep("Initializing engine pipeline...");
            Editor.Utils.StartupProfiler.BeginSection("Engine Pipeline Init");
            var pipeline = Engine.Core.EngineUpdatePipeline.Instance;
            pipeline.RegisterSystem(new Engine.Core.Systems.InputUpdateSystem());
            // NOTE: Physics and CharacterController are handled in PlayMode.UpdateSimulation()
            // to ensure they run in the SAME FixedUpdate accumulator with correct ordering
            pipeline.RegisterSystem(new Editor.Systems.PlayModeSimulationSystem());
            pipeline.RegisterSystem(new Engine.Core.Systems.AudioUpdateSystem());
            Log.Information("Engine update pipeline initialized with {Count} systems", pipeline.GetAllSystems().Count);
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

            // Execute engine update pipeline (handles Input, Audio, PlayMode simulation, etc.)
            // The pipeline provides crash isolation and performance profiling
            Engine.Core.EngineUpdatePipeline.Instance.ExecuteFrame((float)e.Time);

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
            // Process any engine main-thread actions enqueued by background tasks
            try
            {
                Engine.Utils.MainThreadInvoker.ProcessPending();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MainThreadInvoker.ProcessPending failed");
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

    // P/Invoke for Windows fallback to force window icon
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr GetWindowHandle(GameWindow game)
    {
        try
        {
            if (game == null) return IntPtr.Zero;
            var t = game.GetType();
            // Try GameWindow.NativeWindow
            var prop = t.GetProperty("NativeWindow");
            object? native = null;
            if (prop != null) native = prop.GetValue(game);
            if (native == null)
            {
                // Try WindowInfo
                prop = t.GetProperty("WindowInfo");
                if (prop != null) native = prop.GetValue(game);
            }
            if (native == null) return IntPtr.Zero;

            // Try common Handle property names
            var nh = native.GetType().GetProperty("Handle") ?? native.GetType().GetProperty("WindowHandle") ?? native.GetType().GetProperty("NativeWindowHandle");
            if (nh != null)
            {
                var val = nh.GetValue(native);
                if (val is IntPtr ip) return ip;
                if (val is long l) return new IntPtr(l);
                if (val is int i) return new IntPtr(i);
            }

            // Try a method that returns the handle
            var mi = native.GetType().GetMethod("GetWindowHandle") ?? native.GetType().GetMethod("GetHandle");
            if (mi != null)
            {
                var val = mi.Invoke(native, null);
                if (val is IntPtr ip2) return ip2;
                if (val is long l2) return new IntPtr(l2);
                if (val is int i2) return new IntPtr(i2);
            }
        }
        catch { }
        return IntPtr.Zero;
    }
}
