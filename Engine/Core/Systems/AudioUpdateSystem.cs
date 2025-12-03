using Engine.Audio.Core;
using Serilog;

namespace Engine.Core.Systems
{
    /// <summary>
    /// System wrapper for AudioEngine - handles 3D audio updates, listener position, streaming
    /// </summary>
    public sealed class AudioUpdateSystem : IUpdateSystem
    {
        public string Name => "Audio";
        public bool Enabled { get; set; } = true;
        public UpdatePhase Phase => UpdatePhase.Update;
        public int Priority => 100; // After gameplay logic, before rendering

        public void Update(float deltaTime)
        {
            try
            {
                AudioEngine.Instance?.Update(deltaTime);
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "[AudioUpdateSystem] Failed to update audio");

                // Try to recover by reinitializing on next frame
                // (The system will attempt to continue functioning)
            }
        }
    }
}
