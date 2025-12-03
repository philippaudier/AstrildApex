using System;
using System.Collections.Generic;
using System.Linq;
using Engine.Scene;
using Engine.Components;
using Serilog;

namespace Editor
{
    /// <summary>
    /// Robust Play Mode transition manager with validation, rollback, and lifecycle management
    /// Ensures clean transitions between Edit and Play modes without crashes or corrupted state
    /// </summary>
    public class PlayModeTransitionManager
    {
        /// <summary>
        /// Transition phases for granular error handling and rollback
        /// </summary>
        public enum TransitionPhase
        {
            Idle,
            Validating,
            CloningScene,
            PreparingResources,
            InitializingSystems,
            InitializingComponents,
            ActivatingPlayMode,
            Running,
            Stopping,
            CleaningUp,
            Failed
        }

        /// <summary>
        /// Result of a transition operation
        /// </summary>
        public class TransitionResult
        {
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public TransitionPhase FailedAtPhase { get; set; }
            public Exception? Exception { get; set; }

            public static TransitionResult Ok() => new TransitionResult { Success = true };
            public static TransitionResult Fail(string message, TransitionPhase phase, Exception? ex = null)
                => new TransitionResult { Success = false, ErrorMessage = message, FailedAtPhase = phase, Exception = ex };
        }

        private TransitionPhase _currentPhase = TransitionPhase.Idle;
        private Scene? _originalScene;
        private Scene? _playScene;
        private readonly List<IPlayModeSystemHandler> _systemHandlers = new();

        public TransitionPhase CurrentPhase => _currentPhase;
        public Scene? PlayScene => _playScene;

        /// <summary>
        /// Register a system handler for lifecycle management during transitions
        /// </summary>
        public void RegisterSystemHandler(IPlayModeSystemHandler handler)
        {
            if (!_systemHandlers.Contains(handler))
            {
                _systemHandlers.Add(handler);
                Log.Information($"[TransitionManager] Registered system handler: {handler.GetType().Name}");
            }
        }

        /// <summary>
        /// Enter Play Mode with full validation and rollback support
        /// </summary>
        public TransitionResult EnterPlayMode(Scene editScene)
        {
            Log.Information("[TransitionManager] ====== ENTERING PLAY MODE ======");
            _currentPhase = TransitionPhase.Validating;

            try
            {
                // PHASE 1: VALIDATION
                Log.Information("[TransitionManager] Phase 1: Validating scene and systems...");
                var validationResult = ValidateBeforeTransition(editScene);
                if (!validationResult.Success)
                {
                    _currentPhase = TransitionPhase.Failed;
                    return validationResult;
                }

                _originalScene = editScene;

                // CRITICAL: Stop all audio in Edit Mode BEFORE cloning to prevent streaming clips
                // from being in a corrupted state when they are shared between Edit and Play scenes
                try
                {
                    Engine.Audio.Core.AudioEngine.Instance.StopAll();
                    Log.Information("[TransitionManager] Stopped all audio before cloning");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[TransitionManager] Failed to stop audio before cloning");
                }

                // PHASE 2: CLONE SCENE
                Log.Information("[TransitionManager] Phase 2: Cloning scene...");
                _currentPhase = TransitionPhase.CloningScene;
                _playScene = CloneSceneSafely(editScene);
                if (_playScene == null)
                {
                    return Rollback(TransitionResult.Fail(
                        "Failed to clone scene",
                        TransitionPhase.CloningScene
                    ));
                }
                Log.Information($"[TransitionManager] Scene cloned: {_playScene.Entities.Count} entities");

                // PHASE 3: PREPARE RESOURCES
                Log.Information("[TransitionManager] Phase 3: Preparing resources...");
                _currentPhase = TransitionPhase.PreparingResources;
                if (!PrepareResources(_playScene))
                {
                    return Rollback(TransitionResult.Fail(
                        "Failed to prepare resources",
                        TransitionPhase.PreparingResources
                    ));
                }

                // PHASE 4: INITIALIZE SYSTEMS
                Log.Information("[TransitionManager] Phase 4: Initializing global systems...");
                _currentPhase = TransitionPhase.InitializingSystems;
                if (!InitializeGlobalSystems())
                {
                    return Rollback(TransitionResult.Fail(
                        "Failed to initialize global systems",
                        TransitionPhase.InitializingSystems
                    ));
                }

                // PHASE 5: INITIALIZE COMPONENTS
                Log.Information("[TransitionManager] Phase 5: Initializing components...");
                _currentPhase = TransitionPhase.InitializingComponents;
                if (!InitializeComponents(_playScene))
                {
                    return Rollback(TransitionResult.Fail(
                        "Failed to initialize components",
                        TransitionPhase.InitializingComponents
                    ));
                }

                // PHASE 6: ACTIVATE PLAY MODE
                Log.Information("[TransitionManager] Phase 6: Activating Play Mode...");
                _currentPhase = TransitionPhase.ActivatingPlayMode;
                ActivatePlayMode();

                // PHASE 7: RUNNING
                _currentPhase = TransitionPhase.Running;
                Log.Information("[TransitionManager] ====== PLAY MODE ACTIVE ======");
                return TransitionResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[TransitionManager] Critical error during transition at phase: {_currentPhase}");
                return Rollback(TransitionResult.Fail(
                    $"Exception during transition: {ex.Message}",
                    _currentPhase,
                    ex
                ));
            }
        }

        /// <summary>
        /// Exit Play Mode with guaranteed cleanup
        /// </summary>
        public TransitionResult ExitPlayMode()
        {
            Log.Information("[TransitionManager] ====== EXITING PLAY MODE ======");
            _currentPhase = TransitionPhase.Stopping;

            try
            {
                // PHASE 1: STOP SIMULATION
                Log.Information("[TransitionManager] Phase 1: Stopping simulation...");
                StopSimulation();

                // PHASE 2: CLEANUP COMPONENTS
                Log.Information("[TransitionManager] Phase 2: Cleaning up components...");
                _currentPhase = TransitionPhase.CleaningUp;
                CleanupComponents(_playScene);

                // PHASE 3: CLEANUP SYSTEMS
                Log.Information("[TransitionManager] Phase 3: Cleaning up global systems...");
                CleanupGlobalSystems();

                // PHASE 4: DISPOSE RESOURCES
                Log.Information("[TransitionManager] Phase 4: Disposing resources...");
                DisposeResources();

                // PHASE 5: CLEAR REFERENCES
                Log.Information("[TransitionManager] Phase 5: Clearing references...");
                _playScene = null;
                _originalScene = null;

                // PHASE 6: RETURN TO EDIT MODE
                _currentPhase = TransitionPhase.Idle;
                Log.Information("[TransitionManager] ====== EDIT MODE RESTORED ======");
                return TransitionResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Error during exit - attempting forced cleanup");
                ForceCleanup();
                _currentPhase = TransitionPhase.Idle;
                return TransitionResult.Fail($"Error during exit: {ex.Message}", TransitionPhase.CleaningUp, ex);
            }
        }

        // ========== VALIDATION ==========

        private TransitionResult ValidateBeforeTransition(Scene scene)
        {
            if (scene == null)
                return TransitionResult.Fail("Scene is null", TransitionPhase.Validating);

            if (scene.Entities == null || scene.Entities.Count == 0)
            {
                Log.Warning("[TransitionManager] Scene has no entities - continuing anyway");
            }

            // Validate that required systems are initialized
            if (!Engine.Audio.Core.AudioEngine.Instance.IsInitialized)
            {
                Log.Warning("[TransitionManager] AudioEngine not initialized - audio will not work in Play Mode");
            }

            // Check for critical missing dependencies
            if (scene.Entities != null)
            {
                foreach (var entity in scene.Entities)
                {
                    foreach (var component in entity.GetAllComponents())
                    {
                        if (component is Engine.Audio.Components.AudioSource audioSource)
                        {
                            if (audioSource.PlayOnAwake && audioSource.ClipGuid == null)
                            {
                                Log.Warning($"[TransitionManager] AudioSource on '{entity.Name ?? "Unknown"}' has PlayOnAwake but no clip");
                            }
                        }
                    }
                }
            }

            Log.Information("[TransitionManager] Validation passed");
            return TransitionResult.Ok();
        }

        // ========== CLONING ==========

        private Scene? CloneSceneSafely(Scene scene)
        {
            try
            {
                var cloned = scene.Clone(Program.ScriptHost);
                Log.Information($"[TransitionManager] Scene cloned successfully: {cloned.Entities.Count} entities");
                return cloned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Failed to clone scene");
                return null;
            }
        }

        // ========== RESOURCE PREPARATION ==========

        private bool PrepareResources(Scene playScene)
        {
            try
            {
                // Preload materials and textures
                int preloadCount = 0;
                Func<Guid, string?> resolver = guid => Engine.Assets.AssetDatabase.TryGet(guid, out var rec) ? rec.Path : null;

                foreach (var entity in playScene.Entities)
                {
                    try
                    {
                        var mr = entity.GetComponent<Engine.Components.MeshRendererComponent>();
                        if (mr != null && mr.MaterialGuid.HasValue && mr.MaterialGuid.Value != Guid.Empty)
                        {
                            var mat = Engine.Assets.AssetDatabase.LoadMaterial(mr.MaterialGuid.Value);
                            Engine.Rendering.MaterialRuntime.FromAsset(mat, resolver);
                            preloadCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"[TransitionManager] Failed to preload material for entity: {entity.Name ?? "Unknown"}");
                    }
                }

                Log.Information($"[TransitionManager] Preloaded {preloadCount} material(s)");

                // Clear material caches to force reload
                Engine.Rendering.MaterialRuntime.ClearGlobalCache();

                // Flush pending texture uploads
                System.Threading.Thread.Sleep(10);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int totalUploaded = 0;
                int batchCount = 0;
                int uploaded;
                const int maxBatches = 20;

                do
                {
                    if (batchCount > 0)
                        System.Threading.Thread.Sleep(5);

                    uploaded = Engine.Rendering.TextureCache.FlushPendingUploads(100);
                    totalUploaded += uploaded;
                    batchCount++;
                }
                while (uploaded > 0 && batchCount < maxBatches);

                sw.Stop();

                if (totalUploaded > 0)
                {
                    Log.Information($"[TransitionManager] Flushed {totalUploaded} texture(s) in {sw.ElapsedMilliseconds}ms");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Failed to prepare resources");
                return false;
            }
        }

        // ========== SYSTEM INITIALIZATION ==========

        private bool InitializeGlobalSystems()
        {
            try
            {
                // Set Play Mode flag BEFORE initializing systems
                Engine.Core.RuntimeEnvironment.IsPlayMode = true;

                // Initialize registered system handlers
                foreach (var handler in _systemHandlers.OrderBy(h => h.InitializationPriority))
                {
                    try
                    {
                        if (_playScene == null)
                        {
                            Log.Error("[TransitionManager] Play scene is null during system initialization");
                            return false;
                        }

                        Log.Information($"[TransitionManager] Initializing system: {handler.SystemName}");
                        handler.OnEnterPlayMode(_playScene);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"[TransitionManager] Failed to initialize system: {handler.SystemName}");
                        return false;
                    }
                }

                // Input system
                Engine.Input.InputManager.Instance?.SetPlayModeActive(true);

                // NOTE: Do NOT clear collision system here!
                // Colliders are registered during OnAttached() which happened during scene cloning.
                // This is now handled by PhysicsSystemHandler.

                Log.Information("[TransitionManager] All systems initialized");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Failed to initialize global systems");
                return false;
            }
        }

        // ========== COMPONENT INITIALIZATION ==========

        private bool InitializeComponents(Scene playScene)
        {
            try
            {
                var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(playScene.Entities);
                int totalComponents = 0;
                int successfulInits = 0;

                for (int i = 0; i < entitiesSpan.Length; i++)
                {
                    var entity = entitiesSpan[i];
                    if (!entity.Active) continue;

                    var components = entity.GetAllComponents();
                    totalComponents += components.Count();

                    foreach (var component in components)
                    {
                        if (!component.Enabled) continue;

                        try
                        {
                            component.OnEnable();
                            component.Start();
                            successfulInits++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"[TransitionManager] Failed to initialize component {component.GetType().Name} on entity {entity.Name ?? "Unknown"}");
                        }
                    }
                }

                Log.Information($"[TransitionManager] Initialized {successfulInits}/{totalComponents} components");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Failed to initialize components");
                return false;
            }
        }

        // ========== ACTIVATION ==========

        private void ActivatePlayMode()
        {
            // Maximize/focus game panel if options enabled
            if (Panels.GamePanel.Options.MaximizeOnPlay)
            {
                Panels.GamePanel.SetMaximized(true);
            }

            if (Panels.GamePanel.Options.FocusOnPlay)
            {
                Panels.GamePanel.FocusWindow();
            }
        }

        // ========== CLEANUP ==========

        private void StopSimulation()
        {
            // Stop all audio sources
            try
            {
                Engine.Audio.Core.AudioEngine.Instance.StopAll();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TransitionManager] Failed to stop audio");
            }

            // Reset input state
            Engine.Input.InputManager.Instance?.SetPlayModeActive(false);
            Engine.Input.InputManager.Instance?.SetMenuVisible(false);
            Engine.Input.InputManager.Instance?.UnlockCursor();
            Engine.Input.Cursor.lockState = Engine.Input.CursorLockMode.None;
            Engine.Input.Cursor.visible = true;
        }

        private void CleanupComponents(Scene? playScene)
        {
            if (playScene == null) return;

            try
            {
                foreach (var entity in playScene.Entities)
                {
                    foreach (var component in entity.GetAllComponents())
                    {
                        try
                        {
                            component.OnDestroy();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"[TransitionManager] Error in OnDestroy for {component.GetType().Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Failed to cleanup components");
            }
        }

        private void CleanupGlobalSystems()
        {
            try
            {
                // Cleanup registered system handlers (in reverse order)
                foreach (var handler in _systemHandlers.OrderByDescending(h => h.InitializationPriority))
                {
                    try
                    {
                        if (_originalScene == null || _playScene == null)
                        {
                            Log.Warning("[TransitionManager] Scene is null during cleanup - skipping handler");
                            continue;
                        }

                        Log.Information($"[TransitionManager] Cleaning up system: {handler.SystemName}");
                        handler.OnExitPlayMode(_originalScene, _playScene);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"[TransitionManager] Failed to cleanup system: {handler.SystemName}");
                    }
                }

                // NOTE: Collision system cleanup is now handled by PhysicsSystemHandler
                // which was already called in the loop above

                // Reset play mode flag
                Engine.Core.RuntimeEnvironment.IsPlayMode = false;

                // Exit maximized mode
                Panels.GamePanel.SetMaximized(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Failed to cleanup global systems");
            }
        }

        private void DisposeResources()
        {
            try
            {
                // Reload terrain shader
                Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");

                // Reset game panel
                Panels.GamePanel.ResetForExit();

                // Flush pending texture uploads
                System.Threading.Thread.Sleep(10);
                int uploaded = Engine.Rendering.TextureCache.FlushPendingUploads(100);
                if (uploaded > 0)
                {
                    Log.Information($"[TransitionManager] Flushed {uploaded} textures on exit");
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TransitionManager] Failed to dispose some resources");
            }
        }

        // ========== ROLLBACK ==========

        private TransitionResult Rollback(TransitionResult failureResult)
        {
            Log.Warning($"[TransitionManager] ROLLBACK triggered at phase: {failureResult.FailedAtPhase}");
            Log.Warning($"[TransitionManager] Reason: {failureResult.ErrorMessage}");

            try
            {
                // Force cleanup of any partially initialized state
                ForceCleanup();

                _currentPhase = TransitionPhase.Idle;
                Log.Information("[TransitionManager] Rollback complete - returned to Edit Mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Error during rollback - state may be corrupted");
            }

            return failureResult;
        }

        private void ForceCleanup()
        {
            try
            {
                if (_playScene != null)
                {
                    CleanupComponents(_playScene);
                    _playScene = null;
                }

                CleanupGlobalSystems();
                DisposeResources();

                _originalScene = null;
                _currentPhase = TransitionPhase.Idle;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TransitionManager] Error during force cleanup");
            }
        }
    }

    /// <summary>
    /// Interface for systems that need lifecycle hooks during Play Mode transitions
    /// </summary>
    public interface IPlayModeSystemHandler
    {
        string SystemName { get; }
        int InitializationPriority { get; } // Lower = earlier initialization
        void OnEnterPlayMode(Scene playScene);
        void OnExitPlayMode(Scene editScene, Scene playScene);
    }
}
