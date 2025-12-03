using Serilog;
using Engine.Core;

namespace Editor.Systems
{
    /// <summary>
    /// System wrapper for Play Mode simulation - handles component updates during Play Mode
    /// This system is Editor-specific and runs in the editor's update pipeline
    /// </summary>
    public sealed class PlayModeSimulationSystem : IUpdateSystem
    {
        public string Name => "PlayModeSimulation";
        public bool Enabled { get; set; } = true;
        public UpdatePhase Phase => UpdatePhase.Update;
        public int Priority => 50; // After input, before audio

        public void Update(float deltaTime)
        {
            try
            {
                // Only update if in Play Mode
                if (PlayMode.IsPlaying)
                {
                    PlayMode.UpdateSimulation(deltaTime);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "[PlayModeSimulationSystem] Failed to update Play Mode simulation");

                // Play Mode crash is critical but isolated
                // Consider pausing Play Mode on crash
            }
        }
    }
}
