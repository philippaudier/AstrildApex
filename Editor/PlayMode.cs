using System;
using Engine.Scene;
using Engine.Components;
using Editor.Panels;

namespace Editor
{
    /// <summary>
    /// Gestionnaire statique du Play Mode pour l'éditeur
    /// </summary>
    public static class PlayMode
    {
        private static PlayState _state = PlayState.Edit;
        private static Scene? _originalScene;
        private static Scene? _playScene;
        private static float _fixedTimeAccumulator = 0f;
        private static float _fixedDeltaTime = 0.02f; // 50 FPS fixed update
        
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

        /// <summary>
        /// Démarre le Play Mode - sauvegarde la scène actuelle et lance la simulation
        /// </summary>
        public static void Play()
        {
            if (_state != PlayState.Edit) return;
            var currentScene = EditorUI.MainViewport.Renderer?.Scene;
            if (currentScene == null) return;
            
            // Keep a reference to the original scene so we can restore later if needed
            _originalScene = currentScene;
            _playScene = currentScene.Clone(Program.ScriptHost);

            // IMPORTANT: Do NOT replace the editor viewport's scene with the play scene.
            // Rendering the play scene must be done by the GamePanel's GameRenderer only.
            // This prevents duplicate rendering where both the Viewport and Game panels
            // draw the same runtime scene simultaneously.

            // DO NOT clear material/texture cache when entering Play Mode
            // The global cache should be preserved so GameRenderer can reuse loaded materials
            Console.WriteLine("[PlayMode] Entering Play Mode - preserving material/texture cache");

            // PERFORMANCE: Flush any pending texture uploads immediately when entering Play Mode
            // This ensures all textures are ready before the first frame renders
            try
            {
                System.Threading.Thread.Sleep(50); // Give background threads time to decode images
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int uploaded = Engine.Rendering.TextureCache.FlushPendingUploads(50);
                sw.Stop();
                if (uploaded > 0)
                {
                    Console.WriteLine($"[PlayMode] ⚡ Flushed {uploaded} pending texture(s) in {sw.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayMode] FlushPendingUploads failed: {ex.Message}");
            }

            // Configurer les contextes d'input pour le mode Play
            Engine.Input.InputManager.Instance?.SetPlayModeActive(true);
            
            // Ne pas changer la scène du ViewportPanel en entrant en Play Mode.
            InitializePlayModeComponents();
            
            // Maximize Game Panel if option is enabled (Unity-style)
            if (Panels.GamePanel.Options.MaximizeOnPlay)
            {
                Panels.GamePanel.SetMaximized(true);
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
        /// </summary>
        public static void Stop()
        {
            // Stopping Play Mode
            
            if (_state == PlayState.Edit) return;
            
            // Configurer les contextes d'input pour retourner en mode Edit
            Engine.Input.InputManager.Instance?.SetPlayModeActive(false);
            
            // Force menu state to closed (in case Play Mode stopped with menu open)
            Engine.Input.InputManager.Instance?.SetMenuVisible(false);
            Console.WriteLine("[PlayMode] Stop - Forced menu state to closed");

            // Reset GamePanel cursor state and restore safe cursor state
            Panels.GamePanel.ResetCursorState();
            
            // Force unlock cursor and ensure clean state - CRITICAL ORDER:
            // 1. First unlock via InputManager (sets CursorState.Normal)
            Engine.Input.InputManager.Instance?.UnlockCursor();
            
            // 2. Then force cursor properties (should already be set by UnlockCursor)
            Engine.Input.Cursor.lockState = Engine.Input.CursorLockMode.None;
            Engine.Input.Cursor.visible = true;
            
            Console.WriteLine("[PlayMode] Stop - Cursor unlocked and reset to normal");
            
            // Call OnDestroy() on all components before cleanup
            if (_playScene != null)
            {
                foreach (var entity in _playScene.Entities)
                {
                    foreach (var component in entity.GetAllComponents())
                    {
                        component.OnDestroy();
                    }
                }
            }
            
            // The editor viewport was not replaced when entering Play Mode, so nothing
            // needs to be restored here. Clear play scene references and cleanup.
            _playScene = null;
            _originalScene = null;
            _fixedTimeAccumulator = 0f;
            
            // Force reload terrain shader BEFORE disposing GamePanel to ensure shader is valid
            // This prevents black screen / InvalidOperation errors when returning to Edit mode
            try
            {
                Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayMode] Warning: Failed to reload TerrainForward shader: {ex.Message}");
            }
            
            // Exit maximized mode before disposing (ensures clean state)
            Panels.GamePanel.SetMaximized(false);

            // Diagnostic: capture main viewport stats before disposing the GamePanel
            try
            {
                var main = EditorUI.MainViewport.Renderer;
                if (main != null)
                {
                    Console.WriteLine($"[PlayMode] Before Dispose - MainViewport: instances={Editor.Rendering.ViewportRenderer.InstanceCount}, LastFrameCpuMs={main.LastFrameCpuMs}, DrawCalls={main.DrawCallsThisFrame}, Triangles={main.TrianglesThisFrame}");
                }
                else
                {
                    Console.WriteLine($"[PlayMode] Before Dispose - MainViewport: NULL, instances={Editor.Rendering.ViewportRenderer.InstanceCount}");
                }
            }
            catch { }

            // Avoid full dispose to keep GL resources alive and prevent heavy reallocation on toggles
            Panels.GamePanel.ResetForExit();

            // PERFORMANCE: Flush any remaining pending texture uploads after exiting Play Mode
            // This ensures the editor viewport has all textures loaded
            try
            {
                System.Threading.Thread.Sleep(50); // Give background threads time
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int uploaded = Engine.Rendering.TextureCache.FlushPendingUploads(50);
                sw.Stop();
                if (uploaded > 0)
                {
                    Console.WriteLine($"[PlayMode] ⚡ Exit: Flushed {uploaded} pending texture(s) in {sw.ElapsedMilliseconds}ms");
                    // Clear material cache to force reload with fresh texture handles
                    Engine.Rendering.MaterialRuntime.ClearGlobalCache();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayMode] Exit FlushPendingUploads failed: {ex.Message}");
            }

            // Diagnostic: log ViewportRenderer instance count and main viewport renderer after dispose
            try
            {
                var main2 = EditorUI.MainViewport.Renderer;
                Console.WriteLine($"[PlayMode] After Dispose - ViewportRenderer instances={Editor.Rendering.ViewportRenderer.InstanceCount}, EditorUI.MainViewport.Renderer is {(main2 == null ? "NULL" : "NOT NULL")}");
                if (main2 != null)
                {
                    Console.WriteLine($"[PlayMode] After Dispose - MainViewport: LastFrameCpuMs={main2.LastFrameCpuMs}, DrawCalls={main2.DrawCallsThisFrame}, Triangles={main2.TrianglesThisFrame}");
                }
            }
            catch { }
            
            _state = PlayState.Edit;
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

        private static void InitializePlayModeComponents()
        {
            if (_playScene == null) return;

            foreach (var entity in _playScene.Entities)
            {
                if (!entity.Active) continue;

                foreach (var component in entity.GetAllComponents())
                {
                    if (!component.Enabled) continue;

                    try
                    {
                        component.OnEnable();
                        component.Start();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        private static void UpdateComponents(float deltaTime)
        {
            if (_playScene == null) return;

            foreach (var entity in _playScene.Entities)
            {
                if (!entity.Active) continue;

                foreach (var component in entity.GetAllComponents())
                {
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

            foreach (var entity in _playScene.Entities)
            {
                if (!entity.Active) continue;

                foreach (var component in entity.GetAllComponents())
                {
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

            foreach (var entity in _playScene.Entities)
            {
                if (!entity.Active) continue;

                foreach (var component in entity.GetAllComponents())
                {
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