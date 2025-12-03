using Engine.Physics;
using Serilog;

namespace Engine.Core.Systems
{
    /// <summary>
    /// System wrapper for CollisionSystem - handles physics simulation at fixed timestep
    /// </summary>
    public sealed class PhysicsUpdateSystem : IUpdateSystem
    {
        public string Name => "Physics";
        public bool Enabled { get; set; } = true;
        public UpdatePhase Phase => UpdatePhase.FixedUpdate; // Physics runs at fixed timestep
        public int Priority => 0;

        public void Update(float deltaTime)
        {
            try
            {
                CollisionSystem.Step(deltaTime);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "[PhysicsUpdateSystem] Failed to step physics simulation");

                // Physics crash is critical but isolated - other systems continue
                // Consider disabling physics on repeated failures
            }
        }
    }
}
