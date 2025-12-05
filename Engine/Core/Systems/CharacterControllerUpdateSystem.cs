using Engine.Components;
using Serilog;

namespace Engine.Core.Systems
{
    /// <summary>
    /// System wrapper for CharacterController updates - runs in FixedUpdate AFTER physics
    /// This ensures CharacterController queries are synchronized with CollisionSystem state
    /// </summary>
    public sealed class CharacterControllerUpdateSystem : IUpdateSystem
    {
        public string Name => "CharacterController";
        public bool Enabled { get; set; } = true;
        public UpdatePhase Phase => UpdatePhase.FixedUpdate; // Run in physics phase
        public int Priority => 10; // AFTER PhysicsUpdateSystem (priority 0)

        private Engine.Scene.Scene? _activeScene;

        public void SetActiveScene(Engine.Scene.Scene? scene)
        {
            _activeScene = scene;
        }

        public void Update(float deltaTime)
        {
            if (_activeScene == null) return;

            try
            {
                // Update all CharacterControllers in the scene
                var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_activeScene.Entities);
                for (int i = 0; i < span.Length; i++)
                {
                    var entity = span[i];
                    if (!entity.Active) continue;

                    // Find CharacterController component
                    var cc = entity.GetComponent<CharacterController>();
                    if (cc != null && cc.Enabled)
                    {
                        try
                        {
                            cc.FixedUpdate(deltaTime);
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error(ex, $"[CharacterControllerUpdateSystem] Failed to update CharacterController on entity {entity.Id}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "[CharacterControllerUpdateSystem] Failed to update CharacterControllers");
            }
        }
    }
}
