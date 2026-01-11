using System;

namespace Engine.Profiling
{
    /// <summary>
    /// GPU profiler using OpenGL query objects for precise GPU timing.
    /// Automatically integrates with the main Profiler system.
    /// </summary>
    public static class GPUProfiler
    {
        /// <summary>
        /// Begin a GPU timing scope.
        /// Must be called from the render thread.
        /// </summary>
        public static void BeginGPUScope(string name)
        {
            if (!Profiler.Enabled) return;
            GPUTimer.BeginScope(name);
        }

        /// <summary>
        /// End the current GPU timing scope.
        /// Must be called from the render thread.
        /// </summary>
        public static void EndGPUScope()
        {
            if (!Profiler.Enabled) return;
            GPUTimer.EndScope();
        }

        /// <summary>
        /// Collect GPU timing results and integrate with Profiler.
        /// Call this once per frame after all rendering is complete.
        /// </summary>
        public static void CollectGPUResults()
        {
            if (!Profiler.Enabled) return;

            // Collect GPU query results
            GPUTimer.CollectResults();

            // Integrate with main Profiler system
            var scopeNames = GPUTimer.GetScopeNames();
            foreach (var name in scopeNames)
            {
                float latest = GPUTimer.GetLatest(name);
                if (latest > 0)
                {
                    Profiler.RecordGpu(name, latest);
                }
            }
        }

        /// <summary>
        /// Manual recording of GPU time (for external timing sources).
        /// </summary>
        public static void Record(string name, float ms)
        {
            if (!Profiler.Enabled) return;
            Profiler.RecordGpu(name, ms);
        }

        /// <summary>
        /// Check if GPU timing is supported on this platform
        /// </summary>
        public static bool IsSupported => GPUTimer.IsSupported;

        /// <summary>
        /// Shutdown and cleanup all GPU resources
        /// </summary>
        public static void Shutdown()
        {
            GPUTimer.Shutdown();
        }
    }
}
