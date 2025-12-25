using System;
using Serilog;

namespace Editor
{
    /// <summary>
    /// System handler for Audio subsystem
    /// Ensures AudioEngine is ready before components try to use it
    /// </summary>
    public class AudioSystemHandler : IPlayModeSystemHandler
    {
        public string SystemName => "AudioSystem";
        public int InitializationPriority => 10; // Initialize early (lower = earlier)

        public void OnEnterPlayMode(Engine.Scene.Scene playScene)
        {
            try
            {
                if (!Engine.Audio.Core.AudioEngine.Instance.IsInitialized)
                {
                    Log.Warning("[AudioSystemHandler] AudioEngine not initialized - attempting initialization");
                    Engine.Audio.Core.AudioEngine.Instance.Initialize();
                }

                // Ensure listener is at origin by default
                Engine.Audio.Core.AudioEngine.Instance.SetListenerPosition(OpenTK.Mathematics.Vector3.Zero);
                Engine.Audio.Core.AudioEngine.Instance.SetListenerOrientation(
                    new OpenTK.Mathematics.Vector3(0, 0, -1), // Forward
                    new OpenTK.Mathematics.Vector3(0, 1, 0)   // Up
                );

                Log.Information("[AudioSystemHandler] Audio system ready for Play Mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioSystemHandler] Failed to prepare audio system");
                throw; // Re-throw to trigger rollback
            }
        }

        public void OnExitPlayMode(Engine.Scene.Scene editScene, Engine.Scene.Scene playScene)
        {
            try
            {
                // CRITICAL: Stop all audio sources (both Play Mode and Edit Mode scenes)
                // This ensures streaming clips are properly stopped and reset before
                // returning to Edit Mode, preventing the "looping first 2 seconds" bug
                Engine.Audio.Core.AudioEngine.Instance.StopAll();

                // CRITICAL FIX: Also stop all AudioSources in the Edit Mode scene that
                // may be sharing StreamingAudioClips with the Play Mode scene. Without
                // this, Edit Mode sources keep references to corrupted streaming state.
                if (editScene != null)
                {
                    foreach (var entity in editScene.Entities)
                    {
                        var audioSource = entity.GetComponent<Engine.Audio.Components.AudioSource>();
                        if (audioSource != null)
                        {
                            try
                            {
                                audioSource.Stop();
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, $"[AudioSystemHandler] Failed to stop AudioSource on entity: {entity.Name}");
                            }
                        }
                    }
                }

                // Reset master volume to normal if it was modified
                // (Don't dispose AudioEngine - it's a singleton that persists across transitions)

                Log.Information("[AudioSystemHandler] Audio system cleaned up");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSystemHandler] Error during audio cleanup");
            }
        }
    }

    /// <summary>
    /// System handler for Physics/Collision subsystem
    /// Ensures collision system is properly initialized and cleared
    /// </summary>
    public class PhysicsSystemHandler : IPlayModeSystemHandler
    {
        public string SystemName => "PhysicsSystem";
        public int InitializationPriority => 20; // Initialize after audio

        public void OnEnterPlayMode(Engine.Scene.Scene playScene)
        {
            try
            {
                // IMPORTANT: Do NOT clear collision system here!
                // Colliders are registered during OnAttached() which happens AFTER this phase.
                // Clearing here would remove colliders that were registered during scene cloning.

                // The collision system will be properly populated when components initialize.
                // We only need to ensure it's in a clean state when EXITING Play Mode.

                Log.Information("[PhysicsSystemHandler] Physics system ready for Play Mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PhysicsSystemHandler] Failed to prepare physics system");
                throw;
            }
        }

        public void OnExitPlayMode(Engine.Scene.Scene editScene, Engine.Scene.Scene playScene)
        {
            try
            {
                // Physics system removed - cleanup disabled
                // Engine.Physics.CollisionSystem.ClearAll();

                Log.Information("[PhysicsSystemHandler] Physics system cleaned up (disabled)");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PhysicsSystemHandler] Error during physics cleanup");
            }
        }
    }

    /// <summary>
    /// System handler for Input subsystem
    /// Manages input context switching between Edit and Play modes
    /// </summary>
    public class InputSystemHandler : IPlayModeSystemHandler
    {
        public string SystemName => "InputSystem";
        public int InitializationPriority => 30; // Initialize after physics

        public void OnEnterPlayMode(Engine.Scene.Scene playScene)
        {
            try
            {
                if (Engine.Input.InputManager.Instance == null)
                {
                    Log.Warning("[InputSystemHandler] InputManager not initialized");
                    return;
                }

                // Switch to Play Mode input context
                Engine.Input.InputManager.Instance.SetPlayModeActive(true);

                // Ensure menu is closed
                Engine.Input.InputManager.Instance.SetMenuVisible(false);

                // Reset cursor to default state
                Engine.Input.InputManager.Instance.UnlockCursor();

                Log.Information("[InputSystemHandler] Input system switched to Play Mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[InputSystemHandler] Failed to prepare input system");
                throw;
            }
        }

        public void OnExitPlayMode(Engine.Scene.Scene editScene, Engine.Scene.Scene playScene)
        {
            try
            {
                if (Engine.Input.InputManager.Instance == null) return;

                // Switch back to Edit Mode input context
                Engine.Input.InputManager.Instance.SetPlayModeActive(false);

                // Force menu state to closed
                Engine.Input.InputManager.Instance.SetMenuVisible(false);

                // Reset cursor state - CRITICAL ORDER:
                // 1. Unlock via InputManager
                Engine.Input.InputManager.Instance.UnlockCursor();

                // 2. Force cursor properties
                Engine.Input.Cursor.lockState = Engine.Input.CursorLockMode.None;
                Engine.Input.Cursor.visible = true;

                Log.Information("[InputSystemHandler] Input system switched to Edit Mode");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[InputSystemHandler] Error during input cleanup");
            }
        }
    }

    /// <summary>
    /// System handler for Rendering subsystem
    /// Manages material caches and shader reloading
    /// </summary>
    public class RenderingSystemHandler : IPlayModeSystemHandler
    {
        public string SystemName => "RenderingSystem";
        public int InitializationPriority => 40; // Initialize after input

        public void OnEnterPlayMode(Engine.Scene.Scene playScene)
        {
            try
            {
                // CENTRALIZED: Clear ALL material caches using the new unified method
                // This ensures both AssetDatabase and MaterialRuntime caches are cleared together
                Engine.Assets.AssetDatabase.ClearAllMaterialCaches();

                Log.Information("[RenderingSystemHandler] All material caches cleared for Play Mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[RenderingSystemHandler] Failed to prepare rendering system");
                throw;
            }
        }

        public void OnExitPlayMode(Engine.Scene.Scene editScene, Engine.Scene.Scene playScene)
        {
            try
            {
                // Force reload terrain shader to prevent black screen
                try
                {
                    Engine.Rendering.ShaderLibrary.ReloadShader("TerrainForward");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[RenderingSystemHandler] Failed to reload TerrainForward shader");
                }

                Log.Information("[RenderingSystemHandler] Rendering system cleaned up");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[RenderingSystemHandler] Error during rendering cleanup");
            }
        }
    }
}
