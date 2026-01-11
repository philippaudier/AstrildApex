using System;
using System.Collections.Generic;

namespace Engine.Profiling
{
    /// <summary>
    /// Comprehensive rendering statistics for a single frame.
    /// Tracks draw calls, geometry, instancing, memory, and timing.
    /// </summary>
    public class RenderStats
    {
        // === DRAW CALLS ===

        /// <summary>Total number of draw calls in this frame</summary>
        public int DrawCalls { get; set; }

        /// <summary>Number of instanced draw calls (DrawArraysInstanced, DrawElementsInstanced)</summary>
        public int InstancedDrawCalls { get; set; }

        /// <summary>Total instances rendered across all instanced draw calls</summary>
        public int TotalInstances { get; set; }

        /// <summary>Number of instances that passed frustum culling</summary>
        public int VisibleInstances { get; set; }

        /// <summary>Number of instances culled by frustum or distance</summary>
        public int CulledInstances { get; set; }

        // === GEOMETRY ===

        /// <summary>Total triangles rendered this frame</summary>
        public long TotalTriangles { get; set; }

        /// <summary>Total vertices processed this frame</summary>
        public long TotalVertices { get; set; }

        // === MEMORY (in MB) ===

        /// <summary>Texture memory usage in MB</summary>
        public float TextureMemoryMB { get; set; }

        /// <summary>Buffer memory (VBO, EBO, UBO, etc.) usage in MB</summary>
        public float BufferMemoryMB { get; set; }

        /// <summary>Total VRAM usage in MB</summary>
        public float TotalVRAMMB { get; set; }

        // === TIMING ===

        /// <summary>CPU frame time in milliseconds</summary>
        public float CPUTimeMs { get; set; }

        /// <summary>GPU frame time in milliseconds</summary>
        public float GPUTimeMs { get; set; }

        // === DETAILED BREAKDOWN ===

        /// <summary>Per-pass statistics (e.g., "Shadows", "Vegetation", "PostProcess")</summary>
        public Dictionary<string, PassStats> PassStats { get; set; } = new();

        /// <summary>Per-batch instancing statistics</summary>
        public Dictionary<string, BatchStats> BatchStats { get; set; } = new();

        /// <summary>Reset all stats to zero</summary>
        public void Reset()
        {
            DrawCalls = 0;
            InstancedDrawCalls = 0;
            TotalInstances = 0;
            VisibleInstances = 0;
            CulledInstances = 0;
            TotalTriangles = 0;
            TotalVertices = 0;
            TextureMemoryMB = 0;
            BufferMemoryMB = 0;
            TotalVRAMMB = 0;
            CPUTimeMs = 0;
            GPUTimeMs = 0;
            PassStats.Clear();
            BatchStats.Clear();
        }

        /// <summary>Clone these stats</summary>
        public RenderStats Clone()
        {
            var clone = new RenderStats
            {
                DrawCalls = this.DrawCalls,
                InstancedDrawCalls = this.InstancedDrawCalls,
                TotalInstances = this.TotalInstances,
                VisibleInstances = this.VisibleInstances,
                CulledInstances = this.CulledInstances,
                TotalTriangles = this.TotalTriangles,
                TotalVertices = this.TotalVertices,
                TextureMemoryMB = this.TextureMemoryMB,
                BufferMemoryMB = this.BufferMemoryMB,
                TotalVRAMMB = this.TotalVRAMMB,
                CPUTimeMs = this.CPUTimeMs,
                GPUTimeMs = this.GPUTimeMs
            };

            foreach (var kvp in PassStats)
            {
                clone.PassStats[kvp.Key] = kvp.Value.Clone();
            }

            foreach (var kvp in BatchStats)
            {
                clone.BatchStats[kvp.Key] = kvp.Value.Clone();
            }

            return clone;
        }
    }

    /// <summary>
    /// Statistics for a single render pass (e.g., Shadow, Forward, PostProcess)
    /// </summary>
    public class PassStats
    {
        public string Name { get; set; } = "";
        public int DrawCalls { get; set; }
        public long Triangles { get; set; }
        public float CPUTimeMs { get; set; }
        public float GPUTimeMs { get; set; }

        public PassStats Clone()
        {
            return new PassStats
            {
                Name = this.Name,
                DrawCalls = this.DrawCalls,
                Triangles = this.Triangles,
                CPUTimeMs = this.CPUTimeMs,
                GPUTimeMs = this.GPUTimeMs
            };
        }
    }

    /// <summary>
    /// Statistics for a single instancing batch
    /// </summary>
    public class BatchStats
    {
        public string Name { get; set; } = "";

        /// <summary>Total instances in this batch (before culling)</summary>
        public int TotalInstances { get; set; }

        /// <summary>Instances that passed culling</summary>
        public int VisibleInstances { get; set; }

        /// <summary>Instances culled</summary>
        public int CulledInstances { get; set; }

        /// <summary>Triangles per instance</summary>
        public int TrianglesPerInstance { get; set; }

        /// <summary>Total triangles rendered (VisibleInstances * TrianglesPerInstance)</summary>
        public long TotalTriangles => (long)VisibleInstances * TrianglesPerInstance;

        /// <summary>Culling efficiency (0-1, higher is better)</summary>
        public float CullingEfficiency => TotalInstances > 0 ? (float)CulledInstances / TotalInstances : 0f;

        public BatchStats Clone()
        {
            return new BatchStats
            {
                Name = this.Name,
                TotalInstances = this.TotalInstances,
                VisibleInstances = this.VisibleInstances,
                CulledInstances = this.CulledInstances,
                TrianglesPerInstance = this.TrianglesPerInstance
            };
        }
    }

    /// <summary>
    /// Accumulated statistics over multiple frames for analysis
    /// </summary>
    public class RenderStatsHistory
    {
        private readonly Queue<RenderStats> _history = new();
        private readonly int _maxFrames;

        public RenderStatsHistory(int maxFrames = 600)
        {
            _maxFrames = maxFrames;
        }

        public void AddFrame(RenderStats stats)
        {
            _history.Enqueue(stats.Clone());
            while (_history.Count > _maxFrames)
            {
                _history.Dequeue();
            }
        }

        public int FrameCount => _history.Count;

        /// <summary>Get statistics for the most recent frame</summary>
        public RenderStats? GetLatest()
        {
            if (_history.Count == 0) return null;
            return _history.ToArray()[_history.Count - 1];
        }

        /// <summary>Get average stats across all frames in history</summary>
        public RenderStats GetAverage()
        {
            if (_history.Count == 0) return new RenderStats();

            var avg = new RenderStats();
            int count = 0;

            foreach (var frame in _history)
            {
                avg.DrawCalls += frame.DrawCalls;
                avg.InstancedDrawCalls += frame.InstancedDrawCalls;
                avg.TotalInstances += frame.TotalInstances;
                avg.VisibleInstances += frame.VisibleInstances;
                avg.CulledInstances += frame.CulledInstances;
                avg.TotalTriangles += frame.TotalTriangles;
                avg.TotalVertices += frame.TotalVertices;
                avg.TextureMemoryMB += frame.TextureMemoryMB;
                avg.BufferMemoryMB += frame.BufferMemoryMB;
                avg.TotalVRAMMB += frame.TotalVRAMMB;
                avg.CPUTimeMs += frame.CPUTimeMs;
                avg.GPUTimeMs += frame.GPUTimeMs;
                count++;
            }

            if (count > 0)
            {
                avg.DrawCalls /= count;
                avg.InstancedDrawCalls /= count;
                avg.TotalInstances /= count;
                avg.VisibleInstances /= count;
                avg.CulledInstances /= count;
                avg.TotalTriangles /= count;
                avg.TotalVertices /= count;
                avg.TextureMemoryMB /= count;
                avg.BufferMemoryMB /= count;
                avg.TotalVRAMMB /= count;
                avg.CPUTimeMs /= count;
                avg.GPUTimeMs /= count;
            }

            return avg;
        }

        /// <summary>Get maximum stats across all frames</summary>
        public RenderStats GetMax()
        {
            if (_history.Count == 0) return new RenderStats();

            var max = new RenderStats();

            foreach (var frame in _history)
            {
                if (frame.DrawCalls > max.DrawCalls) max.DrawCalls = frame.DrawCalls;
                if (frame.InstancedDrawCalls > max.InstancedDrawCalls) max.InstancedDrawCalls = frame.InstancedDrawCalls;
                if (frame.TotalInstances > max.TotalInstances) max.TotalInstances = frame.TotalInstances;
                if (frame.VisibleInstances > max.VisibleInstances) max.VisibleInstances = frame.VisibleInstances;
                if (frame.CulledInstances > max.CulledInstances) max.CulledInstances = frame.CulledInstances;
                if (frame.TotalTriangles > max.TotalTriangles) max.TotalTriangles = frame.TotalTriangles;
                if (frame.TotalVertices > max.TotalVertices) max.TotalVertices = frame.TotalVertices;
                if (frame.TextureMemoryMB > max.TextureMemoryMB) max.TextureMemoryMB = frame.TextureMemoryMB;
                if (frame.BufferMemoryMB > max.BufferMemoryMB) max.BufferMemoryMB = frame.BufferMemoryMB;
                if (frame.TotalVRAMMB > max.TotalVRAMMB) max.TotalVRAMMB = frame.TotalVRAMMB;
                if (frame.CPUTimeMs > max.CPUTimeMs) max.CPUTimeMs = frame.CPUTimeMs;
                if (frame.GPUTimeMs > max.GPUTimeMs) max.GPUTimeMs = frame.GPUTimeMs;
            }

            return max;
        }

        public void Clear()
        {
            _history.Clear();
        }
    }
}
