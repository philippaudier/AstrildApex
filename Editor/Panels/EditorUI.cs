using Engine.Assets;
using System;
using ImGuiNET;
using Editor.Serialization;
using Editor.State;
using Editor.Panels;
using System.Numerics;
using Editor.Icons;
using Editor.SceneManagement;
using Editor.UI;

namespace Editor.Panels;

public static class EditorUI
{
    // Panel instances
    // Using the modern ViewportPanel for iterative work
    public static ViewportPanelModern MainViewport = new ViewportPanelModern();
    public static AstrildApex.Editor.UI.GamePanelModern GamePanelModern = new AstrildApex.Editor.UI.GamePanelModern();
    public static PreferencesWindow Preferences = new PreferencesWindow();
    //private static int _debugFrameCounter = 0;
    
    // View toggles
    static bool ShowHierarchy = true;
    static bool ShowInspector = true;
    static bool ShowAssets = true;
    static bool ShowConsole = true;
    static bool ShowGame = true;
    static bool ShowEnvironment = true;
    public static bool ShowRenderingSettings = LoadUIPreference("ShowRenderingSettings", false);

    // --- Scene commands - now using SceneManager ---

    public static bool ShowDemoWindow = false;
    public static bool ShowIconManager = false;

    public static void DrawDockspaceAndMainMenu()
    {
        System.IO.Directory.CreateDirectory(Editor.State.ProjectPaths.AssetsDir);
        AssetDatabase.Initialize(Editor.State.ProjectPaths.AssetsDir);
        // S'assure qu'un seul Default White existe (et ne le recrée pas à chaque lancement)
        Engine.Assets.AssetDatabase.EnsureDefaultWhiteMaterial();
        
        // Initialize IconManager
    var iconsPath = System.IO.Path.Combine(Editor.State.ProjectPaths.ProjectRoot, "Editor", "Icons", "astrild-apex-icons.json");
        if (!IconManager.HasIcon("save")) // Only initialize once
        {
            IconManager.Initialize(iconsPath);
        }

        var vp = ImGui.GetMainViewport();

        // Barre de menu globale
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Scene", "Ctrl+N")) SceneManager.NewScene();
                if (ImGui.MenuItem("Open Scene...", "Ctrl+O")) SceneManager.OpenScene();
                ImGui.Separator();
                if (ImGui.MenuItem("Save Scene", "Ctrl+S")) SceneManager.SaveScene();
                if (ImGui.MenuItem("Save Scene As...", "Ctrl+Shift+S")) SceneManager.SaveSceneAs();
                ImGui.Separator();
                if (ImGui.MenuItem("Import 3D Model...", "Ctrl+Shift+I")) ImportModel();
                ImGui.Separator();
                if (ImGui.MenuItem("Exit")) System.Environment.Exit(0);
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View"))
            {
                var sa = ShowAssets; if (ImGui.MenuItem("Assets", null, sa)) ShowAssets = !ShowAssets;
                var sh = ShowHierarchy; if (ImGui.MenuItem("Hierarchy", null, sh)) ShowHierarchy = !ShowHierarchy;
                var si = ShowInspector; if (ImGui.MenuItem("Inspector", null, si)) ShowInspector = !ShowInspector;
                var se = ShowEnvironment; if (ImGui.MenuItem("Environment", null, se)) ShowEnvironment = !ShowEnvironment;
                var sr = ShowRenderingSettings; if (ImGui.MenuItem("Rendering Settings", null, sr)) { ShowRenderingSettings = !ShowRenderingSettings; SaveUIPreference("ShowRenderingSettings", ShowRenderingSettings); }
                var sc = ShowConsole; if (ImGui.MenuItem("Console", null, sc)) ShowConsole = !ShowConsole;
                var sg = ShowGame; if (ImGui.MenuItem("Game", null, sg)) ShowGame = !ShowGame;
                ImGui.Separator();
                var sd = ShowDemoWindow; if (ImGui.MenuItem("ImGui Demo Window", null, sd)) ShowDemoWindow = !ShowDemoWindow;
                var sim = ShowIconManager; if (ImGui.MenuItem("🎨 SVG Icons Manager", null, sim)) ShowIconManager = !ShowIconManager;
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Help"))
            {
                ImGui.TextDisabled("AstrildApex Editor");
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", "Ctrl+Z"))
                {
                    var sc = MainViewport.Renderer?.Scene;
                    if (sc != null) UndoRedo.Undo(sc);
                }
                if (ImGui.MenuItem("Redo", "Ctrl+Shift+Z / Ctrl+Y"))
                {
                    var sc = MainViewport.Renderer?.Scene;
                    if (sc != null) UndoRedo.Redo(sc);
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Preferences...", "Ctrl+,"))
                {
                    Preferences.Open();
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Reload Water Shader", "F5"))
                {
                    try
                    {
                        Engine.Rendering.ShaderLibrary.ReloadShader("Water");
                        Console.WriteLine("✅ Water shader reloaded!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Failed to reload Water shader: {ex.Message}");
                    }
                }
                ImGui.Separator();
                if (ImGui.BeginMenu("Project Settings"))
                {
                    if (ImGui.MenuItem("Input Settings..."))
                    {
                        InputSettingsPanel.Open();
                    }
                    ImGui.EndMenu();
                }
                ImGui.EndMenu();
            }
            
            // Play Mode Controls - centered in menu bar
            DrawPlayModeControls();
            
            ImGui.EndMainMenuBar();
        }
        
        // Dockspace sous la barre de titre OS & la main menu bar
        ImGui.DockSpaceOverViewport(0, vp, ImGuiDockNodeFlags.None, IntPtr.Zero);

        // --- Global shortcuts ---
        // À placer après ImGui.DockSpaceOverViewport(...)
        var io = ImGui.GetIO();

        // Ne pas voler le clavier si on tape du texte dans un InputText/Drag
        bool typingText = io.WantTextInput;

        // On lit les modifieurs fournis par le backend (AddKeyEvent ModCtrl/ModShift)
        bool ctrl  = io.KeyCtrl;
        bool shift = io.KeyShift;

        var sc2 = MainViewport.Renderer?.Scene;
        if (!typingText && sc2 != null)
        {
            // UNDO : Ctrl+Z
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.W))
                UndoRedo.Undo(sc2);

            // REDO : Ctrl+Shift+Z  (standard Adobe)  OU  Ctrl+Y (standard Windows)
            bool redoChord = (ctrl && shift && ImGui.IsKeyPressed(ImGuiKey.W))
                          || (ctrl && ImGui.IsKeyPressed(ImGuiKey.Y));

            if (redoChord)
                UndoRedo.Redo(sc2);
        }

        // Raccourcis de scène
        if (!typingText)
        {
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.N)) SceneManager.NewScene();
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.O)) SceneManager.OpenScene();
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.S)) SceneManager.SaveScene();
            if (ctrl && shift && ImGui.IsKeyPressed(ImGuiKey.S)) SceneManager.SaveSceneAs();
        }
    }

    public static void DrawDefaultLayoutWindows()
    {
        // Panneaux réels (branchés sur la scène + sélection)
    if (ShowHierarchy) HierarchyPanel.Draw();
    if (ShowInspector) InspectorPanel.Draw();
    if (ShowEnvironment) EnvironmentPanel.Draw();
    if (ShowRenderingSettings) RenderingSettingsPanel.Draw();
    if (ShowAssets) AssetsPanel.Draw();
    if (ShowConsole) ConsolePanel.Draw();
    if (ShowGame) GamePanel.Draw();
    MainViewport.Draw();

        if (ShowDemoWindow) ImGui.ShowDemoWindow(ref ShowDemoWindow);
        if (ShowIconManager) IconManager.RenderIconsTestWindow();

        // Render settings panels
        InputSettingsPanel.Draw();
        Preferences.Draw();

        // Render scene management dialogs
        SceneManager.RenderDialogs();

        // Render global progress popup (if any)
        Editor.UI.ProgressManager.Render();
    }

    private static void DrawPlayModeControls()
    {
        // Center the play controls in the menu bar
        var menuBarWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = 30f;
        var totalWidth = buttonWidth * 3 + ImGui.GetStyle().ItemSpacing.X * 2; // 3 buttons + spacing
        var centerPos = (menuBarWidth - totalWidth) * 0.5f;
        
        ImGui.SameLine(centerPos);
        
        // Play Mode state indicator and controls
        var state = PlayMode.State;
        
        // Play button (or Stop if playing)
        if (state == PlayMode.PlayState.Edit)
        {
            ImGui.PushID("play_btn");
            if (IconManager.IconButton("play", "Enter Play Mode"))
            {
                PlayMode.Play();
            }
            ImGui.PopID();
        }
        else
        {
            ImGui.PushID("stop_btn");
            bool stopButtonPressed = IconManager.IconButton("stop", "Stop Play Mode");
            if (stopButtonPressed)
            {
                PlayMode.Stop();
            }
            else
            {
            }
            ImGui.PopID();
        }
        
        ImGui.SameLine();
        
        // Pause/Resume button (only available in Play Mode)
        if (state != PlayMode.PlayState.Edit)
        {
            if (state == PlayMode.PlayState.Playing)
            {
                if (IconManager.IconButton("pause", "Pause"))
                {
                    PlayMode.TogglePause();
                }
            }
            else if (state == PlayMode.PlayState.Paused)
            {
                if (IconManager.IconButton("play", "Resume"))
                {
                    PlayMode.TogglePause();
                }
            }
        }
        else
        {
            ImGui.BeginDisabled();
            IconManager.IconButton("pause", "Available in Play Mode");
            ImGui.EndDisabled();
        }
        
        ImGui.SameLine();
        
        // Step button (only available when paused)
        if (state == PlayMode.PlayState.Paused)
        {
            if (IconManager.IconButton("step", "Step One Frame"))
            {
                PlayMode.Step();
            }
        }
        else
        {
            ImGui.BeginDisabled();
            IconManager.IconButton("step", "Available when Paused");
            ImGui.EndDisabled();
        }
        
        // State indicator text
        ImGui.SameLine();
        ImGui.Spacing();
        ImGui.SameLine();
        var stateColor = state switch
        {
            PlayMode.PlayState.Edit => new Vector4(0.7f, 0.7f, 0.7f, 1.0f),      // Gray
            PlayMode.PlayState.Playing => new Vector4(0.2f, 0.8f, 0.2f, 1.0f),  // Green
            PlayMode.PlayState.Paused => new Vector4(1.0f, 0.8f, 0.2f, 1.0f),   // Orange
            _ => Vector4.One
        };
        
        var stateText = state switch
        {
            PlayMode.PlayState.Edit => "EDIT",
            PlayMode.PlayState.Playing => "PLAYING",
            PlayMode.PlayState.Paused => "PAUSED",
            _ => "UNKNOWN"
        };
        
        ImGui.TextColored(stateColor, stateText);
    }

    // Simple UI preferences persistence
    private static string GetPreferencesPath() =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AstrildApex", "ui_preferences.txt");

    private static bool LoadUIPreference(string key, bool defaultValue)
    {
        try
        {
            var path = GetPreferencesPath();
            if (!System.IO.File.Exists(path)) return defaultValue;

            var lines = System.IO.File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && parts[0].Trim() == key)
                {
                    return bool.Parse(parts[1].Trim());
                }
            }
        }
        catch { }
        return defaultValue;
    }

    private static void SaveUIPreference(string key, bool value)
    {
        try
        {
            var path = GetPreferencesPath();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

            var preferences = new System.Collections.Generic.Dictionary<string, string>();

            // Load existing preferences
            if (System.IO.File.Exists(path))
            {
                var lines = System.IO.File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        preferences[parts[0].Trim()] = parts[1].Trim();
                    }
                }
            }

            // Update preference
            preferences[key] = value.ToString();

            // Save all preferences
            var output = new System.Text.StringBuilder();
            foreach (var kvp in preferences)
            {
                output.AppendLine($"{kvp.Key}={kvp.Value}");
            }

            System.IO.File.WriteAllText(path, output.ToString());
        }
        catch { }
    }

    private static void ImportModel()
    {
        try
        {
            Engine.Utils.DebugLogger.Log("[EditorUI] Opening Import Model dialog...");

            // Open native file dialog
            var result = NativeFileDialogSharp.Dialog.FileOpen("fbx,obj,gltf,glb,dae,3ds,ply,stl,blend");

            if (result.IsOk)
            {
                var sourceFile = result.Path;
                var assetsRoot = Editor.State.ProjectPaths.AssetsDir;
                var fileName = System.IO.Path.GetFileName(sourceFile);

                System.Console.WriteLine($"[Import] Importing model: {fileName}");
                Engine.Utils.DebugLogger.Log($"[EditorUI] Importing model: {sourceFile}");

                // Defer the actual import work so it runs after the current ImGui frame
                // This prevents nested ImGui frames caused by ProgressManager.ForceRender()
                Editor.Utils.DeferredActions.Enqueue(() =>
                {
                    try
                    {
                        // Show progress popup
                        var tracker = new Editor.UI.ProgressManager.StepTracker("Importing 3D Model", 3);

                        tracker.NextStep($"Reading {fileName}...");
                        // Import the model (heavy work)
                        var guid = Engine.Assets.ModelImporter.ImportModel(sourceFile, assetsRoot, "Models");

                        tracker.NextStep("Updating asset database...");
                        // Refresh asset database
                        Engine.Assets.AssetDatabase.Refresh();

                        tracker.Complete($"Model imported successfully!");

                        Engine.Utils.DebugLogger.Log($"[EditorUI] Model imported successfully with GUID: {guid}");
                        System.Console.WriteLine($"✓ Model imported successfully: {fileName}");
                        System.Console.WriteLine($"  GUID: {guid}");
                        System.Console.WriteLine($"  Location: Assets/Models/{fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EditorUI] ImportModel failed: {ex.Message}");
                    }
                });
            }
            else if (result.IsCancelled)
            {
                Engine.Utils.DebugLogger.Log("[EditorUI] Import cancelled by user");
            }
            else
            {
                Engine.Utils.DebugLogger.Log($"[EditorUI] Import dialog error");
                System.Console.WriteLine($"✗ Error opening file dialog");
            }
        }
        catch (Exception ex)
        {
            Editor.UI.ProgressManager.Hide(); // Hide progress on error
            Engine.Utils.DebugLogger.Log($"[EditorUI] Failed to import model: {ex.Message}");
            System.Console.WriteLine($"✗ Failed to import model: {ex.Message}");
            System.Console.WriteLine($"  {ex.StackTrace}");
        }
    }
}