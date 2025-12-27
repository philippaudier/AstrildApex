using System;
using Engine.Scene;
using Engine.Components;
using Editor.Logging;
using Editor.Panels;

namespace Editor
{
    /// <summary>
    /// Gestionnaire statique du Play Mode pour l'éditeur
    /// Now uses PlayModeTransitionManager for robust, error-safe transitions
    /// </summary>
    public static class PlayMode
    {
        private static PlayState _state = PlayState.Edit;
        private static Scene? _originalScene;
        private static Scene? _playScene;

        // REMOVED: _fixedTimeAccumulator and _fixedDeltaTime - now handled by PhysicsManager

        // PERFORMANCE: Cache component lists to avoid repeated GetAllComponents() allocations
        private static readonly Dictionary<uint, List<Engine.Components.Component>> _cachedComponentsByEntity
            = new Dictionary<uint, List<Engine.Components.Component>>();

        // NEW: Robust transition manager with validation, rollback, and lifecycle management
        private static readonly PlayModeTransitionManager _transitionManager = new PlayModeTransitionManager();
        private static bool _transitionManagerInitialized = false;

        public enum PlayState
        {
            Edit,    // Mode édition normale
            Playing, // Simulation en cours
            Paused   // Simulation en pause
        }

        public static PlayState State => _state;
        public static bool IsPlaying => _state == PlayState.Playing;
        public static bool IsPaused => _state == PlayState.Paused;
        public static bool IsInPlayMode => _state != PlayState.Edit;
        public static Scene? PlayScene => _playScene;
        public static PlayModeTransitionManager.TransitionPhase CurrentTransitionPhase => _transitionManager.CurrentPhase;

        /// <summary>
        /// Initialize transition manager with system handlers (called once)
        /// </summary>
        private static void EnsureTransitionManagerInitialized()
        {
            if (_transitionManagerInitialized) return;

            // Register system handlers in order of initialization priority
            _transitionManager.RegisterSystemHandler(new AudioSystemHandler());
            _transitionManager.RegisterSystemHandler(new PhysicsSystemHandler());
            _transitionManager.RegisterSystemHandler(new InputSystemHandler());
            _transitionManager.RegisterSystemHandler(new RenderingSystemHandler());

            _transitionManagerInitialized = true;
            Engine.Utils.DebugLogger.Log("[PlayMode] Transition manager initialized with system handlers");
        }

        /// <summary>
        /// Démarre le Play Mode - sauvegarde la scène actuelle et lance la simulation
        /// Now uses PlayModeTransitionManager for robust error-safe transitions
        /// </summary>
        public static void Play()
        {
            if (_state != PlayState.Edit) return;
            var currentScene = EditorUI.MainViewport.Renderer?.Scene;
            if (currentScene == null)
            {
                LogManager.LogError("Cannot enter Play Mode: No scene loaded", "PlayMode");
                return;
            }

            // Initialize transition manager on first use
            EnsureTransitionManagerInitialized();

            // Use the robust transition manager for entering Play Mode
            var result = _transitionManager.EnterPlayMode(currentScene);

            if (!result.Success)
            {
                LogManager.LogError($"Failed to enter Play Mode: {result.ErrorMessage}", "PlayMode");
                if (result.Exception != null)
                {
                    LogManager.LogError($"Exception: {result.Exception}", "PlayMode");
                }
                return;
            }

            // Transition successful - update local state
            _originalScene = currentScene;
            _playScene = _transitionManager.PlayScene;

            // CRITICAL: Switch viewport renderer to use PLAY SCENE instead of original scene
            // This prevents modifications during play mode from affecting the original scene
            if (_playScene != null && EditorUI.MainViewport.Renderer != null)
            {
                EditorUI.MainViewport.Renderer.SetScene(_playScene);
                Engine.Utils.DebugLogger.Log($"[PlayMode] Switched viewport to play scene with {_playScene.Entities.Count} entities");
            }

            // NEW: Switch ViewportPanel to Play Mode
            try
            {
                EditorUI.MainViewport.SetPlayMode(true);
                Engine.Utils.DebugLogger.Log("[PlayMode] ViewportPanel switched to Play Mode");
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"Failed to switch ViewportPanel to Play Mode: {ex.Message}", "PlayMode");
            }

            // Clear component cache and rebuild for Play Mode
            _cachedComponentsByEntity.Clear();
            if (_playScene != null)
            {
                var entitiesSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_playScene.Entities);
                for (int i = 0; i < entitiesSpan.Length; i++)
                {
                    var entity = entitiesSpan[i];
                    if (!entity.Active) continue;
                    var comps = entity.GetAllComponents();
                    _cachedComponentsByEntity[entity.Id] = new List<Engine.Components.Component>(comps);
                }
            }

            // Physics system removed
            // Engine.Physics.PhysicsManager.Instance.SetActiveScene(_playScene);

            _state = PlayState.Playing;
            LogManager.LogInfo("Play Mode started", "PlayMode");
        }

        /// <summary>
        /// Met en pause ou reprend la simulation
        /// </summary>
        public static void TogglePause()
        {
            if (_state == PlayState.Playing)
                _state = PlayState.Paused;
            else if (_state == PlayState.Paused)
                _state = PlayState.Playing;
        }

        /// <summary>
        /// Avance d'une frame en mode pause
        /// </summary>
        public static void Step()
        {
            if (_state != PlayState.Paused) return;
            
            // Exécuter une frame de simulation
            UpdateSimulation(0.016f); // 60 FPS frame
        }

        /// <summary>
        /// Arrête le Play Mode et restaure la scène originale
        /// Now uses PlayModeTransitionManager for robust cleanup
        /// </summary>
        public static void Stop()
        {
            if (_state == PlayState.Edit) return;

            Engine.Utils.DebugLogger.Log("[PlayMode] Stopping Play Mode...");

            // Use the robust transition manager for exiting Play Mode
            var result = _transitionManager.ExitPlayMode();

            if (!result.Success)
            {
                LogManager.LogWarning($"Play Mode exit had errors: {result.ErrorMessage}", "PlayMode");
                // Even if there were errors, we still transition back to Edit mode
                // The TransitionManager guarantees cleanup even on failure
            }

            // CRITICAL: Restore original scene BEFORE clearing references
            // This ensures the editor returns to the pre-Play Mode scene state
            if (_originalScene != null && EditorUI.MainViewport.Renderer != null)
            {
                EditorUI.MainViewport.Renderer.SetScene(_originalScene);
                Engine.Utils.DebugLogger.Log($"[PlayMode] Restored original scene with {_originalScene.Entities.Count} entities");
            }
            else
            {
                LogManager.LogWarning("Original scene was null or viewport renderer not available - cannot restore!", "PlayMode");
            }

            // NEW: Switch ViewportPanel back to Edit Mode
            try
            {
                EditorUI.MainViewport.SetPlayMode(false);
                Engine.Utils.DebugLogger.Log("[PlayMode] ViewportPanel switched to Edit Mode");
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"Failed to switch ViewportPanel to Edit Mode: {ex.Message}", "PlayMode");
            }

            // Clear local state
            _playScene = null;
            _originalScene = null;
            _cachedComponentsByEntity.Clear();

            // Physics system removed
            // Engine.Physics.PhysicsManager.Instance.Reset();
            // Engine.Physics.PhysicsManager.Instance.SetActiveScene(null);

            _state = PlayState.Edit;
            Engine.Utils.DebugLogger.Log("[PlayMode] Returned to Edit Mode");
        }

        // Weather system (global)
        private static readonly Engine.Systems.WeatherSystem _weatherSystem = new Engine.Systems.WeatherSystem();

        /// <summary>
        /// Met à jour la simulation (appelé depuis la boucle principale)
        /// Now uses unified PhysicsManager - no more double accumulator!
        /// </summary>
        public static void UpdateSimulation(float deltaTime)
        {
            if (_state != PlayState.Playing || _playScene == null) return;

            // === STEP 0: Update weather system (global environment) ===
            try
            {
                _weatherSystem.Update(_playScene, deltaTime);
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[PlayMode] Weather system error: {ex.Message}");
            }

            // === STEP 1: Update components (variable timestep) ===
            UpdateComponents(deltaTime);

            // Physics system removed
            // Engine.Physics.PhysicsManager.Instance.Update(deltaTime);

            // === STEP 3: FixedUpdate for other components (CameraComponent, etc.) ===
            FixedUpdateComponents(1.0f / 60.0f);

            // === STEP 4: Late update ===
            LateUpdateComponents(deltaTime);
        }

        // NOTE: InitializePlayModeComponents() removed - now handled by PlayModeTransitionManager

        private static void UpdateComponents(float deltaTime)
        {
            if (_playScene == null) return;

            var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_playScene.Entities);
            for (int i = 0; i < span.Length; i++)
            {
                var entity = span[i];
                if (!entity.Active) continue;

                // Use cached component list (PERFORMANCE - zero allocation!)
                if (!_cachedComponentsByEntity.TryGetValue(entity.Id, out var comps))
                    continue;

                for (int c = 0; c < comps.Count; c++)
                {
                    var component = comps[c];
                    if (!component.Enabled) continue;

                    try
                    {
                        component.Update(deltaTime);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static void FixedUpdateComponents(float fixedDeltaTime)
        {
            if (_playScene == null) return;

            var spanFixed = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_playScene.Entities);
            for (int i = 0; i < spanFixed.Length; i++)
            {
                var entity = spanFixed[i];
                if (!entity.Active) continue;

                // Use cached component list (PERFORMANCE - zero allocation!)
                if (!_cachedComponentsByEntity.TryGetValue(entity.Id, out var comps))
                    continue;

                for (int c = 0; c < comps.Count; c++)
                {
                    var component = comps[c];
                    if (!component.Enabled) continue;

                    // Physics system removed
                    // if (component is Engine.Components.CharacterController)
                    //     continue;

                    try
                    {
                        component.FixedUpdate(fixedDeltaTime);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static void LateUpdateComponents(float deltaTime)
        {
            if (_playScene == null) return;

            var spanLate = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_playScene.Entities);
            for (int i = 0; i < spanLate.Length; i++)
            {
                var entity = spanLate[i];
                if (!entity.Active) continue;

                // Use cached component list (PERFORMANCE - zero allocation!)
                if (!_cachedComponentsByEntity.TryGetValue(entity.Id, out var comps))
                    continue;

                for (int c = 0; c < comps.Count; c++)
                {
                    var component = comps[c];
                    if (!component.Enabled) continue;

                    try
                    {
                        component.LateUpdate(deltaTime);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }
    }
}