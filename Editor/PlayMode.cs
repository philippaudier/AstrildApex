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
        private static float _fixedTimeAccumulator = 0f;
        private static float _fixedDeltaTime = 0.02f; // 50 FPS fixed update

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

            _state = PlayState.Playing;
            // Play Mode started
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

            // Reset GamePanel cursor state before transition
            try
            {
                Panels.GamePanel.ResetCursorState();
            }
            catch (Exception ex)
            {
                Engine.Utils.DebugLogger.Log($"[PlayMode] Warning: Failed to reset cursor state: {ex.Message}");
            }

            // Use the robust transition manager for exiting Play Mode
            var result = _transitionManager.ExitPlayMode();

            if (!result.Success)
            {
                LogManager.LogWarning($"Play Mode exit had errors: {result.ErrorMessage}", "PlayMode");
                // Even if there were errors, we still transition back to Edit mode
                // The TransitionManager guarantees cleanup even on failure
            }

            // Clear local state
            _playScene = null;
            _originalScene = null;
            _fixedTimeAccumulator = 0f;
            _cachedComponentsByEntity.Clear();

            _state = PlayState.Edit;
            Engine.Utils.DebugLogger.Log("[PlayMode] Returned to Edit Mode");
        }

        /// <summary>
        /// Met à jour la simulation (appelé depuis la boucle principale)
        /// </summary>
        public static void UpdateSimulation(float deltaTime)
        {
            if (_state != PlayState.Playing || _playScene == null) return;

            // Mettre à jour les composants
            UpdateComponents(deltaTime);
            
            // Fixed update pour la physique
            _fixedTimeAccumulator += deltaTime;
            while (_fixedTimeAccumulator >= _fixedDeltaTime)
            {
                // Step simple collision world first
                Engine.Physics.CollisionSystem.Step(_fixedDeltaTime);
                FixedUpdateComponents(_fixedDeltaTime);
                _fixedTimeAccumulator -= _fixedDeltaTime;
            }
            
            // Late update
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