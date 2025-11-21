using System;
using System.IO;
using System.Text.Json;
using Engine.Rendering;
using Editor.Rendering;
using OpenTK.Mathematics;
using Editor.Logging;

namespace Editor.State
{
    /// <summary>
    /// Persistent editor settings like last opened scene
    /// </summary>
    public static class EditorSettings
    {
        private static readonly string _settingsPath = Path.Combine(ProjectPaths.ProjectRoot, "ProjectSettings", "EditorSettings.json");
        
        private class Settings
        {
            public string LastOpenedScene { get; set; } = "";
            public DateTime LastOpenedTime { get; set; } = DateTime.MinValue;
            // Persist whether the horizontal grid is visible in the viewport HUD
            public bool ShowGrid { get; set; } = true;

            // Camera clipping planes for the editor viewport
            public float CameraNear { get; set; } = 0.1f;
            public float CameraFar { get; set; } = 5000f;

            // Persisted camera projection mode & ortho/FOV
            public int CameraProjectionMode { get; set; } = 0; // 0=Perspective,1=Ortho,2=2D
            public float CameraOrthoSize { get; set; } = 10f;
            public float CameraFov { get; set; } = 60f;

            // VSync (vertical synchronization) - persisted editor preference
            public bool VSync { get; set; } = true;

            public CameraStateData ViewportCamera { get; set; } = CameraStateData.CreateDefault();

            // NOTE: SSAO Rendering Settings removed - SSAO is now configured via GlobalEffects component
            // public SSAOSettingsData SSAO { get; set; } = new SSAOSettingsData();

            // Shadows settings
            public ShadowsSettingsData Shadows { get; set; } = new ShadowsSettingsData();

            // TAA (Temporal Anti-Aliasing) settings
            public TAASettingsData TAA { get; set; } = new TAASettingsData();

            // Anti-Aliasing Mode (None, MSAA, TAA)
            public int AntiAliasingMode { get; set; } = 0; // 0 = None (default)

            // Selection Outline Settings
            public OutlineSettingsData Outline { get; set; } = new OutlineSettingsData();

            // Theme name
            public string ThemeName { get; set; } = "Dark Unity";
            
            // External Tools (Unity-style)
            public ExternalToolsData ExternalTools { get; set; } = new ExternalToolsData();
            
            // Font settings
            public FontSettingsData Font { get; set; } = new FontSettingsData();
            
            // UI positions (persist overlay HUD positions)
            public UIPositionsData UI { get; set; } = new UIPositionsData();
        }

        public class UIPositionsData
        {
            // Use -1 to indicate 'not set' so callers can fallback to defaults
            public float GameHudPosX { get; set; } = -1f;
            public float GameHudPosY { get; set; } = -1f;
            public float ViewportHudPosX { get; set; } = -1f;
            public float ViewportHudPosY { get; set; } = -1f;
        }
        
        public class FontSettingsData
        {
            public string FontName { get; set; } = "Default (Proggy Clean)";
            public string FontPath { get; set; } = ""; // Full path to .ttf/.otf file
            public float FontSize { get; set; } = 14f;
        }
        
        public class ExternalToolsData
        {
            public string ScriptEditor { get; set; } = ""; // Path to external editor (VS Code, Rider, etc.)
            public string ScriptEditorArgs { get; set; } = "\"$(File)\" -g \"$(File):$(Line)\""; // Arguments with placeholders
            public bool AutoDetectEditor { get; set; } = true; // Auto-detect VS Code on first run
            // Path to Filament cmgen executable (optional)
            public string CmgenPath { get; set; } = "";
            // If true, automatically run PMREM generation when dropping HDR/EXR into Assets
            public bool AutoGeneratePMREMOnImport { get; set; } = false;
        }

        public class CameraStateData
        {
            public float Yaw { get; set; }
            public float Pitch { get; set; }
            public float Distance { get; set; }
            public float TargetX { get; set; }
            public float TargetY { get; set; }
            public float TargetZ { get; set; }

            public static CameraStateData CreateDefault()
            {
                return new CameraStateData
                {
                    Yaw = MathHelper.DegreesToRadians(-30f),
                    Pitch = MathHelper.DegreesToRadians(-15f),
                    Distance = 3.0f,
                    TargetX = 0f,
                    TargetY = 0f,
                    TargetZ = 0f
                };
            }

            public static CameraStateData FromOrbitState(OrbitCameraState state)
            {
                return new CameraStateData
                {
                    Yaw = state.Yaw,
                    Pitch = state.Pitch,
                    Distance = MathF.Max(0.01f, state.Distance),
                    TargetX = state.Target.X,
                    TargetY = state.Target.Y,
                    TargetZ = state.Target.Z
                };
            }

            public OrbitCameraState ToOrbitState()
            {
                return new OrbitCameraState
                {
                    Yaw = Yaw,
                    Pitch = Pitch,
                    Distance = Distance <= 0f ? 3.0f : Distance,
                    Target = new Vector3(TargetX, TargetY, TargetZ)
                };
            }
        }

        // Separate class for JSON serialization of selection outline settings
        public class OutlineSettingsData
        {
            public bool Enabled { get; set; } = true;
            public float Thickness { get; set; } = 2.0f;  // Outline thickness in pixels
            public float ColorR { get; set; } = 1.0f;     // Orange color
            public float ColorG { get; set; } = 0.5f;
            public float ColorB { get; set; } = 0.0f;
            public float ColorA { get; set; } = 1.0f;
            
            // Pulse/blink settings
            public bool EnablePulse { get; set; } = true;
            public float PulseSpeed { get; set; } = 2.0f;      // Cycles per second
            public float PulseMinAlpha { get; set; } = 0.3f;   // Minimum alpha during pulse
            public float PulseMaxAlpha { get; set; } = 1.0f;   // Maximum alpha during pulse
        }

        // NOTE: SSAO settings data removed - SSAO is now configured via GlobalEffects component
        /*
        // Separate class for JSON serialization of SSAO settings
        public class SSAOSettingsData
        {
            public bool Enabled { get; set; } = true;
            public float Radius { get; set; } = 1.0f;     // View-space sampling radius
            public float Bias { get; set; } = 0.05f;      // Self-occlusion bias
            public float Intensity { get; set; } = 1.5f;  // Power curve for intensity
            public int SampleCount { get; set; } = 16;    // Number of samples (8, 16, 32, 64)
            public int BlurSize { get; set; } = 3;        // Blur kernel radius in pixels

            // Convert to Engine SSAO settings
            public SSAORenderer.SSAOSettings ToEngineSettings()
            {
                return new SSAORenderer.SSAOSettings
                {
                    Enabled = this.Enabled,
                    Radius = this.Radius,
                    Bias = this.Bias,
                    Intensity = this.Intensity,
                    SampleCount = this.SampleCount,
                    BlurSize = this.BlurSize
                };
            }

            // Convert from Engine SSAO settings
            public static SSAOSettingsData FromEngineSettings(SSAORenderer.SSAOSettings settings)
            {
                return new SSAOSettingsData
                {
                    Enabled = settings.Enabled,
                    Radius = settings.Radius,
                    Bias = settings.Bias,
                    Intensity = settings.Intensity,
                    SampleCount = settings.SampleCount,
                    BlurSize = settings.BlurSize
                };
            }
        }
        */

        // Separate class for JSON serialization of TAA settings
        public class TAASettingsData
        {
            public bool Enabled { get; set; } = false;         // Disabled by default
            public float FeedbackMin { get; set; } = 0.8f;     // Minimum history weight
            public float FeedbackMax { get; set; } = 0.95f;    // Maximum history weight
            public bool UseYCoCg { get; set; } = false;        // Disable by default (can cause artifacts)
            public int JitterPattern { get; set; } = 0;        // 0=Halton, 1=R2, 2=None
            public float JitterScale { get; set; } = 1.0f;     // Scale applied to jitter amplitude

            // Convert to Engine TAA settings
            public Engine.Rendering.PostProcess.TAARenderer.TAASettings ToEngineSettings()
            {
                return new Engine.Rendering.PostProcess.TAARenderer.TAASettings
                {
                    Enabled = this.Enabled,
                    FeedbackMin = this.FeedbackMin,
                    FeedbackMax = this.FeedbackMax,
                    UseYCoCg = this.UseYCoCg,
                    JitterPattern = this.JitterPattern,
                    JitterScale = this.JitterScale
                };
            }

            // Convert from Engine TAA settings
            public static TAASettingsData FromEngineSettings(Engine.Rendering.PostProcess.TAARenderer.TAASettings settings)
            {
                return new TAASettingsData
                {
                    Enabled = settings.Enabled,
                    FeedbackMin = settings.FeedbackMin,
                    FeedbackMax = settings.FeedbackMax,
                    UseYCoCg = settings.UseYCoCg,
                    JitterPattern = settings.JitterPattern,
                    JitterScale = settings.JitterScale
                };
            }
        }

        public class ShadowsSettingsData
        {
            // === Core Settings ===
            public bool Enabled { get; set; } = true;
            public int ShadowMapSize { get; set; } = 2048; // 1024, 2048, 4096, 8192
            public float ShadowStrength { get; set; } = 0.7f; // Shadow darkness (0.0 = no shadows, 1.0 = full black)

            // === Bias Settings (prevent shadow acne) ===
            public float ShadowBias { get; set; } = 0.1f; // Depth bias to prevent shadow acne

            // === Scene Settings ===
            public float ShadowDistance { get; set; } = 100f; // Scene radius for shadow coverage

            // === Debug ===
            public bool DebugShowShadowMap { get; set; } = false;

            // === Legacy/Deprecated (kept for compatibility) ===
            [Obsolete("No longer used - simplified shadow system")]
            public int ShadowQuality { get; set; } = 0;

            [Obsolete("No longer used - simplified shadow system")]
            public float ShadowNormalBias { get; set; } = 0.01f;

            [Obsolete("No longer used - simplified shadow system")]
            public int PCFSamples { get; set; } = 2;

            [Obsolete("No longer used - simplified shadow system")]
            public float LightSize { get; set; } = 0.05f;

            // === Legacy/Deprecated (kept for compatibility) ===
            [Obsolete("Use ShadowBias instead")]
            public float Bias
            {
                get => ShadowBias;
                set => ShadowBias = value;
            }

            [Obsolete("Use ShadowBias instead")]
            public float BiasConst
            {
                get => ShadowBias;
                set => ShadowBias = value;
            }

            [Obsolete("Use ShadowNormalBias instead")]
            public float SlopeScale
            {
                get => ShadowNormalBias;
                set => ShadowNormalBias = value;
            }

            [Obsolete("Not used in new shadow system")]
            public float PolygonOffsetFactor { get; set; } = 2.5f;

            [Obsolete("Not used in new shadow system")]
            public float PolygonOffsetUnits { get; set; } = 4.0f;

            [Obsolete("Not used in new shadow system")]
            public float PCFRadius { get; set; } = 1.5f;

            [Obsolete("CSM not implemented yet")]
            public bool UseCascadedShadows { get; set; } = false;

            [Obsolete("CSM not implemented yet")]
            public int CascadeCountCSM { get; set; } = 4;

            [Obsolete("CSM not implemented yet")]
            public float CascadeLambda { get; set; } = 0.5f;

            [Obsolete("Use ShadowMapSize instead")]
            public int AtlasSize
            {
                get => ShadowMapSize;
                set => ShadowMapSize = value;
            }

            [Obsolete("Not used in new shadow system")]
            public int CascadeCount { get; set; } = 1;

            [Obsolete("Use DebugShowShadowMap instead")]
            public bool DebugShowAtlas
            {
                get => DebugShowShadowMap;
                set => DebugShowShadowMap = value;
            }
        }
        
        private static Settings? _currentSettings;

        public static string LastOpenedScene
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.LastOpenedScene ?? "";
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.LastOpenedScene = value;
                    _currentSettings.LastOpenedTime = DateTime.UtcNow;
                    SaveSettings();
                }
            }
        }

        public static bool ShowGrid
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.ShowGrid ?? true;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.ShowGrid = value;
                    SaveSettings();
                }
            }
        }

        public static float CameraNear
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.CameraNear ?? 0.1f;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.CameraNear = value;
                    SaveSettings();
                }
            }
        }

        public static int CameraProjectionMode
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.CameraProjectionMode ?? 0;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.CameraProjectionMode = value;
                    SaveSettings();
                }
            }
        }

        public static float CameraOrthoSize
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.CameraOrthoSize ?? 10f;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.CameraOrthoSize = value;
                    SaveSettings();
                }
            }
        }

        public static float CameraFov
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.CameraFov ?? 60f;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.CameraFov = value;
                    SaveSettings();
                }
            }
        }

        public static float CameraFar
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.CameraFar ?? 5000f;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.CameraFar = value;
                    SaveSettings();
                }
            }
        }

        /// <summary>
        /// VSync preference persisted in editor settings. When changed it should be
        /// applied to the rendering window (GameWindow.VSync).
        /// </summary>
        public static bool VSync
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.VSync ?? true;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.VSync = value;
                    SaveSettings();
                }
            }
        }

        public static OrbitCameraState ViewportCameraState
        {
            get
            {
                LoadSettingsIfNeeded();
                var data = _currentSettings?.ViewportCamera ?? CameraStateData.CreateDefault();
                return data.ToOrbitState();
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.ViewportCamera = CameraStateData.FromOrbitState(value);
                    SaveSettings();
                }
            }
        }

        public static OutlineSettingsData Outline
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.Outline ?? new OutlineSettingsData();
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.Outline = value;
                    SaveSettings();
                }
            }
        }

        // NOTE: SSAOSettings property removed - SSAO is now configured via GlobalEffects component
        /*
        public static SSAORenderer.SSAOSettings SSAOSettings
        {
            get
            {
                LoadSettingsIfNeeded();
                var settings = _currentSettings?.SSAO?.ToEngineSettings() ?? SSAORenderer.SSAOSettings.Default;
                return settings;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.SSAO = SSAOSettingsData.FromEngineSettings(value);
                    SaveSettings();
                }
            }
        }
        */

        public static ShadowsSettingsData ShadowsSettings
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.Shadows ?? new ShadowsSettingsData();
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.Shadows = value;
                    SaveSettings();
                }
            }
        }

        public static Engine.Rendering.PostProcess.TAARenderer.TAASettings TAASettings
        {
            get
            {
                LoadSettingsIfNeeded();
                var settings = _currentSettings?.TAA?.ToEngineSettings() ?? Engine.Rendering.PostProcess.TAARenderer.TAASettings.Default;
                return settings;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.TAA = TAASettingsData.FromEngineSettings(value);
                    SaveSettings();
                }
            }
        }

        // Anti-Aliasing Mode (None, MSAA, TAA)
        public static Engine.Rendering.AntiAliasingMode AntiAliasingMode
        {
            get
            {
                LoadSettingsIfNeeded();
                return (Engine.Rendering.AntiAliasingMode)(_currentSettings?.AntiAliasingMode ?? 0);
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.AntiAliasingMode = (int)value;
                    SaveSettings();
                }
            }
        }

        public static string ThemeName
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.ThemeName ?? "Dark Unity";
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    _currentSettings.ThemeName = value;
                    SaveSettings();
                }
            }
        }
        
        // External Tools settings (Unity-style)
        public static string ScriptEditor
        {
            get
            {
                LoadSettingsIfNeeded();
                var path = _currentSettings?.ExternalTools?.ScriptEditor ?? "";
                
                // Auto-detect VS Code on first access if path is empty and auto-detect is enabled
                if (string.IsNullOrEmpty(path) && (_currentSettings?.ExternalTools?.AutoDetectEditor ?? true))
                {
                    path = DetectVSCode();
                    if (!string.IsNullOrEmpty(path) && _currentSettings != null)
                    {
                        _currentSettings.ExternalTools.ScriptEditor = path;
                        SaveSettings();
                    }
                }
                
                return path;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.ExternalTools == null)
                        _currentSettings.ExternalTools = new ExternalToolsData();
                    _currentSettings.ExternalTools.ScriptEditor = value;
                    SaveSettings();
                }
            }
        }
        
        public static string ScriptEditorArgs
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.ExternalTools?.ScriptEditorArgs ?? "\"$(File)\" -g \"$(File):$(Line)\"";
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.ExternalTools == null)
                        _currentSettings.ExternalTools = new ExternalToolsData();
                    _currentSettings.ExternalTools.ScriptEditorArgs = value;
                    SaveSettings();
                }
            }
        }

        // Filament cmgen executable path
        public static string CmgenPath
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.ExternalTools?.CmgenPath ?? "";
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.ExternalTools == null)
                        _currentSettings.ExternalTools = new ExternalToolsData();
                    _currentSettings.ExternalTools.CmgenPath = value;
                    SaveSettings();
                }
            }
        }

        // Toggle: automatically generate PMREM when importing HDR/EXR
        public static bool AutoGeneratePMREMOnImport
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.ExternalTools?.AutoGeneratePMREMOnImport ?? false;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.ExternalTools == null)
                        _currentSettings.ExternalTools = new ExternalToolsData();
                    _currentSettings.ExternalTools.AutoGeneratePMREMOnImport = value;
                    SaveSettings();
                }
            }
        }
        
        // Font settings
        public static string InterfaceFont
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.Font?.FontName ?? "Default (Proggy Clean)";
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.Font == null)
                        _currentSettings.Font = new FontSettingsData();
                    _currentSettings.Font.FontName = value;
                    SaveSettings();
                }
            }
        }
        
        public static string InterfaceFontPath
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.Font?.FontPath ?? "";
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.Font == null)
                        _currentSettings.Font = new FontSettingsData();
                    _currentSettings.Font.FontPath = value;
                    SaveSettings();
                }
            }
        }

        public static float InterfaceFontSize
        {
            get
            {
                LoadSettingsIfNeeded();
                return _currentSettings?.Font?.FontSize ?? 14f;
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.Font == null)
                        _currentSettings.Font = new FontSettingsData();
                    _currentSettings.Font.FontSize = value;
                    SaveSettings();
                }
            }
        }

        // Persisted HUD positions
        public static (float X, float Y) GameHudPosition
        {
            get
            {
                LoadSettingsIfNeeded();
                var ui = _currentSettings?.UI ?? new UIPositionsData();
                return (ui.GameHudPosX, ui.GameHudPosY);
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.UI == null) _currentSettings.UI = new UIPositionsData();
                    _currentSettings.UI.GameHudPosX = value.X;
                    _currentSettings.UI.GameHudPosY = value.Y;
                    SaveSettings();
                }
            }
        }

        public static (float X, float Y) ViewportHudPosition
        {
            get
            {
                LoadSettingsIfNeeded();
                var ui = _currentSettings?.UI ?? new UIPositionsData();
                return (ui.ViewportHudPosX, ui.ViewportHudPosY);
            }
            set
            {
                LoadSettingsIfNeeded();
                if (_currentSettings != null)
                {
                    if (_currentSettings.UI == null) _currentSettings.UI = new UIPositionsData();
                    _currentSettings.UI.ViewportHudPosX = value.X;
                    _currentSettings.UI.ViewportHudPosY = value.Y;
                    SaveSettings();
                }
            }
        }
        
        /// <summary>
        /// Auto-detect VS Code installation on Windows
        /// </summary>
        private static string DetectVSCode()
        {
            // Try common VS Code installation paths on Windows
            string[] possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe"),
                "C:\\Program Files\\Microsoft VS Code\\Code.exe",
                "C:\\Program Files (x86)\\Microsoft VS Code\\Code.exe"
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    LogManager.LogInfo($"Auto-detected VS Code at: {path}", "EditorSettings");
                    return path;
                }
            }
            
            LogManager.LogInfo("VS Code not found in standard locations", "EditorSettings");
            return "";
        }
        
        /// <summary>
        /// Open a file in the configured external script editor
        /// </summary>
        public static void OpenScript(string filePath, int line = 1)
        {
            var editorPath = ScriptEditor;
            
            if (string.IsNullOrEmpty(editorPath))
            {
                LogManager.LogInfo("No script editor configured. Please set one in Preferences > External Tools", "EditorSettings");
                return;
            }
            
            if (!File.Exists(editorPath))
            {
                LogManager.LogInfo($"Script editor not found at: {editorPath}", "EditorSettings");
                return;
            }
            
            // Replace placeholders in arguments
            var args = ScriptEditorArgs
                .Replace("$(File)", filePath)
                .Replace("$(Line)", line.ToString())
                .Replace("$(Column)", "1");
            
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = editorPath,
                    Arguments = args,
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                
                System.Diagnostics.Process.Start(processInfo);
                LogManager.LogInfo($"Opened {filePath} in external editor", "EditorSettings");
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"Failed to open external editor: {ex.Message}", "EditorSettings");
            }
        }
        
        private static void LoadSettingsIfNeeded()
        {
            if (_currentSettings != null) return;
            
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _currentSettings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                }
                else
                {
                    _currentSettings = new Settings();
                }
            }
            catch
            {
                _currentSettings = new Settings();
            }
        }
        
        private static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath) ?? "");
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Silent fail for settings save
            }
        }
        
        public static bool HasValidLastScene()
        {
            var lastScene = LastOpenedScene;
            return !string.IsNullOrEmpty(lastScene) && 
                   File.Exists(Path.IsPathRooted(lastScene) ? lastScene : Path.Combine(ProjectPaths.ProjectRoot, lastScene));
        }
    }
}