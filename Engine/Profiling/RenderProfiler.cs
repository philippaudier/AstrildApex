using System;
using System.Collections.Generic;

namespace Engine.Profiling
{
    /// <summary>
    /// Specialized profiler for rendering with automatic tracking of draw calls, instances, and geometry.
    /// Integrates with the main Profiler and GPUProfiler systems.
    /// </summary>
    public static class RenderProfiler
    {
        private static RenderStats _currentFrameStats = new RenderStats();
        private static RenderStatsHistory _statsHistory = new RenderStatsHistory(600);
        private static readonly object _lock = new object();

        private static readonly Stack<string> _passStack = new Stack<string>();
        private static string? _currentPass = null;

        /// <summary>
        /// Begin a new render pass (e.g., "Shadows", "Forward", "PostProcess")
        /// </summary>
        public static void BeginRenderPass(string passName)
        {
            if (!Profiler.Enabled) return;

            lock (_lock)
            {
                _passStack.Push(passName);
                _currentPass = passName;

                // Create pass stats if not exists
                if (!_currentFrameStats.PassStats.ContainsKey(passName))
                {
                    _currentFrameStats.PassStats[passName] = new PassStats { Name = passName };
                }
            }

            // Also push a profiler scope
            Profiler.PushScope(passName);
        }

        /// <summary>
        /// End the current render pass
        /// </summary>
        public static void EndRenderPass()
        {
            if (!Profiler.Enabled) return;

            lock (_lock)
            {
                if (_passStack.Count > 0)
                {
                    _passStack.Pop();
                    _currentPass = _passStack.Count > 0 ? _passStack.Peek() : null;
                }
            }

            Profiler.PopScope();
        }

        /// <summary>
        /// Record a draw call
        /// </summary>
        public static void RecordDrawCall(int instances, int triangles)
        {
            if (!Profiler.Enabled) return;

            lock (_lock)
            {
                _currentFrameStats.DrawCalls++;

                if (instances > 1)
                {
                    _currentFrameStats.InstancedDrawCalls++;
                    _currentFrameStats.TotalInstances += instances;
                }

                _currentFrameStats.TotalTriangles += triangles;

                // Also record in current pass
                if (_currentPass != null && _currentFrameStats.PassStats.TryGetValue(_currentPass, out var passStats))
                {
                    passStats.DrawCalls++;
                    passStats.Triangles += triangles;
                }

                // Set counter in profiler
                Profiler.IncrementCounter("DrawCalls", 1);
                Profiler.IncrementCounter("Triangles", triangles);
            }
        }

        /// <summary>
        /// Record an instancing batch with culling information
        /// </summary>
        public static void RecordInstancingBatch(string batchName, int visibleInstances, int triangles, int culledInstances)
        {
            if (!Profiler.Enabled) return;

            lock (_lock)
            {
                int totalInstances = visibleInstances + culledInstances;

                _currentFrameStats.VisibleInstances += visibleInstances;
                _currentFrameStats.CulledInstances += culledInstances;

                // Update or create batch stats
                if (!_currentFrameStats.BatchStats.TryGetValue(batchName, out var batchStats))
                {
                    batchStats = new BatchStats { Name = batchName };
                    _currentFrameStats.BatchStats[batchName] = batchStats;
                }

                batchStats.VisibleInstances = visibleInstances;
                batchStats.CulledInstances = culledInstances;
                batchStats.TotalInstances = totalInstances;
                batchStats.TrianglesPerInstance = visibleInstances > 0 ? triangles / visibleInstances : 0;

                // Set counters
                Profiler.SetCounter($"Batch.{batchName}.Visible", visibleInstances);
                Profiler.SetCounter($"Batch.{batchName}.Culled", culledInstances);
            }
        }

        /// <summary>
        /// Begin a new frame - resets all stats
        /// </summary>
        public static void BeginFrame()
        {
            if (!Profiler.Enabled) return;

            lock (_lock)
            {
                // Save previous frame to history
                if (_currentFrameStats.DrawCalls > 0 || _currentFrameStats.TotalInstances > 0)
                {
                    _statsHistory.AddFrame(_currentFrameStats);
                }

                // Reset for new frame
                _currentFrameStats.Reset();
                _passStack.Clear();
                _currentPass = null;
            }
        }

        /// <summary>
        /// Get the current frame's statistics
        /// </summary>
        public static RenderStats GetFrameStats()
        {
            lock (_lock)
            {
                return _currentFrameStats.Clone();
            }
        }

        /// <summary>
        /// Get the latest frame stats from history
        /// </summary>
        public static RenderStats? GetLatestStats()
        {
            return _statsHistory.GetLatest();
        }

        /// <summary>
        /// Get average stats over all frames
        /// </summary>
        public static RenderStats GetAverageStats()
        {
            return _statsHistory.GetAverage();
        }

        /// <summary>
        /// Get max stats over all frames
        /// </summary>
        public static RenderStats GetMaxStats()
        {
            return _statsHistory.GetMax();
        }

        /// <summary>
        /// Get the full stats history
        /// </summary>
        public static RenderStatsHistory GetHistory()
        {
            return _statsHistory;
        }

        /// <summary>
        /// Clear all history
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _currentFrameStats.Reset();
                _statsHistory.Clear();
                _passStack.Clear();
                _currentPass = null;
            }
        }

        /// <summary>
        /// Get a summary string for debugging
        /// </summary>
        public static string GetSummary()
        {
            var stats = GetFrameStats();
            return $"DrawCalls: {stats.DrawCalls}, Instances: {stats.TotalInstances} ({stats.VisibleInstances} visible, {stats.CulledInstances} culled), Triangles: {stats.TotalTriangles:N0}";
        }
    }
}
