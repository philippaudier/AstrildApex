using System;
using Engine.Core;

namespace Engine.Profiling
{
    /// <summary>
    /// Engine-agnostic profiler updater. Call `ProfilerUpdater.Tick(realDeltaTime)` from
    /// the engine's main update loop (before or after Time.Update) to record frame ms.
    /// </summary>
    public static class ProfilerUpdater
    {
        /// <summary>
        /// Should be called once per frame by the engine with the real (unscaled) delta time.
        /// </summary>
        public static void Tick(float realDeltaTime)
        {
            // Record frame ms (use unscaled real time)
            Profiler.RecordCpu("Frame", realDeltaTime * 1000f);
            Profiler.NewFrame();
        }
    }
}
