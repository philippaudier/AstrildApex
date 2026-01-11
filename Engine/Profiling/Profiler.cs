using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Engine.Profiling
{
    /// <summary>
    /// Advanced CPU/GPU profiling system with hierarchical scopes, detailed statistics, and custom counters.
    /// Use Profiler.Profile("name") for simple CPU profiling, or PushScope/PopScope for hierarchical profiling.
    /// </summary>
    public static class Profiler
    {
        const int HistoryLength = 600; // keep ~10s at 60fps

        class History
        {
            readonly float[] values = new float[HistoryLength];
            int index = 0;
            public void Push(float v)
            {
                values[index] = v;
                index = (index + 1) % values.Length;
            }
            public float[] Snapshot()
            {
                var arr = new float[values.Length];
                Array.Copy(values, arr, values.Length);
                return arr;
            }
            public float Max()
            {
                float m = 0f;
                for (int i = 0; i < values.Length; ++i) if (values[i] > m) m = values[i];
                return m;
            }
            public float Min()
            {
                float m = float.MaxValue;
                for (int i = 0; i < values.Length; ++i)
                    if (values[i] > 0 && values[i] < m) m = values[i];
                return m == float.MaxValue ? 0f : m;
            }
            public float Average()
            {
                float sum = 0f;
                int count = 0;
                for (int i = 0; i < values.Length; ++i)
                {
                    if (values[i] > 0)
                    {
                        sum += values[i];
                        count++;
                    }
                }
                return count > 0 ? sum / count : 0f;
            }
            public float Percentile(float p)
            {
                var sorted = values.Where(v => v > 0).OrderBy(v => v).ToArray();
                if (sorted.Length == 0) return 0f;
                int index = (int)(sorted.Length * p);
                if (index >= sorted.Length) index = sorted.Length - 1;
                return sorted[index];
            }
        }

        static readonly object locker = new object();
        static readonly Dictionary<string, History> cpuHist = new Dictionary<string, History>();
        static readonly Dictionary<string, History> gpuHist = new Dictionary<string, History>();
        static readonly Dictionary<string, long> counters = new Dictionary<string, long>();

        // Hierarchical profiling support
        [ThreadStatic]
        static ProfileScopeStack? scopeStack;

        [ThreadStatic]
        static Stack<SampleInfo>? sampleStack;

        struct SampleInfo { public string name; public long startTicks; }

        // Frame history for hierarchical scopes
        static readonly Queue<ProfileScope> frameHistory = new Queue<ProfileScope>();
        static ProfileScope? currentFrameRoot = null;

        /// <summary>Enable/disable profiling globally (for performance testing)</summary>
        public static bool Enabled { get; set; } = true;

        #region Simple Profiling API (backward compatible)

        public static IDisposable Profile(string name)
        {
            if (!Enabled) return new NullDisposable();
            BeginSample(name);
            return new SampleDisposable(name);
        }

        sealed class NullDisposable : IDisposable
        {
            public void Dispose() { }
        }

        sealed class SampleDisposable : IDisposable
        {
            readonly string name;
            bool disposed;
            public SampleDisposable(string name) { this.name = name; }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                EndSample(name);
            }
        }

        public static void BeginSample(string name)
        {
            if (!Enabled) return;
            if (sampleStack == null) sampleStack = new Stack<SampleInfo>();
            sampleStack.Push(new SampleInfo { name = name, startTicks = Stopwatch.GetTimestamp() });
        }

        public static void EndSample(string? expectedName = null)
        {
            if (!Enabled) return;
            if (sampleStack == null || sampleStack.Count == 0) return;
            var info = sampleStack.Pop();
            long end = Stopwatch.GetTimestamp();
            var ms = (float)((end - info.startTicks) / (double)Stopwatch.Frequency * 1000.0);
            lock (locker)
            {
                if (!cpuHist.TryGetValue(info.name, out var h)) { h = new History(); cpuHist[info.name] = h; }
                h.Push(ms);
            }
        }

        #endregion

        #region Hierarchical Profiling API

        /// <summary>Push a new hierarchical scope</summary>
        public static void PushScope(string name)
        {
            if (!Enabled) return;
            if (scopeStack == null) scopeStack = new ProfileScopeStack();
            scopeStack.Push(name);
        }

        /// <summary>Pop the current hierarchical scope</summary>
        public static void PopScope()
        {
            if (!Enabled) return;
            if (scopeStack == null) return;
            scopeStack.Pop();
        }

        /// <summary>Get the current frame's root scope (for UI display)</summary>
        public static ProfileScope? GetCurrentFrameRoot()
        {
            return currentFrameRoot;
        }

        /// <summary>Get historical frame roots</summary>
        public static ProfileScope[] GetFrameHistory()
        {
            lock (locker)
            {
                return frameHistory.ToArray();
            }
        }

        #endregion

        #region Counter API

        /// <summary>Set a custom counter value</summary>
        public static void SetCounter(string name, long value)
        {
            if (!Enabled) return;
            lock (locker)
            {
                counters[name] = value;
            }

            // Also set counter on current scope if available
            if (scopeStack?.Current != null)
            {
                scopeStack.Current.SetCounter(name, value);
            }
        }

        /// <summary>Increment a custom counter</summary>
        public static void IncrementCounter(string name, long delta = 1)
        {
            if (!Enabled) return;
            lock (locker)
            {
                if (counters.TryGetValue(name, out long current))
                    counters[name] = current + delta;
                else
                    counters[name] = delta;
            }

            // Also increment on current scope
            if (scopeStack?.Current != null)
            {
                scopeStack.Current.IncrementCounter(name, delta);
            }
        }

        /// <summary>Get a counter value</summary>
        public static long GetCounter(string name)
        {
            lock (locker)
            {
                return counters.TryGetValue(name, out long value) ? value : 0;
            }
        }

        #endregion

        #region Frame Management

        public static void NewFrame()
        {
            if (!Enabled) return;

            // Save previous frame's scope tree
            if (scopeStack != null)
            {
                var root = scopeStack.TakeRoot();
                if (root != null)
                {
                    lock (locker)
                    {
                        frameHistory.Enqueue(root);
                        while (frameHistory.Count > 60) // Keep last 60 frames
                        {
                            frameHistory.Dequeue();
                        }
                        currentFrameRoot = root;
                    }
                }
            }

            // Reset per-frame counters
            lock (locker)
            {
                counters.Clear();
            }
        }

        #endregion

        #region Statistics API

        /// <summary>Get detailed statistics for a named scope</summary>
        public static ScopeStats GetScopeStats(string name)
        {
            lock (locker)
            {
                var stats = new ScopeStats { Name = name };

                if (cpuHist.TryGetValue(name, out var cpu))
                {
                    stats.CPUMin = cpu.Min();
                    stats.CPUMax = cpu.Max();
                    stats.CPUAvg = cpu.Average();
                    stats.CPU95th = cpu.Percentile(0.95f);
                    stats.CPU99th = cpu.Percentile(0.99f);
                }

                if (gpuHist.TryGetValue(name, out var gpu))
                {
                    stats.GPUMin = gpu.Min();
                    stats.GPUMax = gpu.Max();
                    stats.GPUAvg = gpu.Average();
                    stats.GPU95th = gpu.Percentile(0.95f);
                    stats.GPU99th = gpu.Percentile(0.99f);
                }

                return stats;
            }
        }

        public struct ScopeStats
        {
            public string Name;
            public float CPUMin, CPUMax, CPUAvg, CPU95th, CPU99th;
            public float GPUMin, GPUMax, GPUAvg, GPU95th, GPU99th;
        }

        #endregion

        #region Legacy API (backward compatible)

        // Record an arbitrary CPU value (ms) for a named counter.
        public static void RecordCpu(string name, float ms)
        {
            if (!Enabled) return;
            lock (locker)
            {
                if (!cpuHist.TryGetValue(name, out var h)) { h = new History(); cpuHist[name] = h; }
                h.Push(ms);
            }
        }

        public static string[] GetCounters()
        {
            lock (locker)
            {
                var keys = new string[cpuHist.Count];
                cpuHist.Keys.CopyTo(keys, 0);
                return keys;
            }
        }

        public static float[] GetCpuHistory(string name)
        {
            lock (locker)
            {
                if (!cpuHist.TryGetValue(name, out var h)) return new float[HistoryLength];
                return h.Snapshot();
            }
        }

        public static float CpuMax(string name)
        {
            lock (locker)
            {
                if (!cpuHist.TryGetValue(name, out var h)) return 0f;
                return h.Max();
            }
        }

        public static float CpuMin(string name)
        {
            lock (locker)
            {
                if (!cpuHist.TryGetValue(name, out var h)) return 0f;
                return h.Min();
            }
        }

        public static float CpuAvg(string name)
        {
            lock (locker)
            {
                if (!cpuHist.TryGetValue(name, out var h)) return 0f;
                return h.Average();
            }
        }

        public static void RecordGpu(string name, float ms)
        {
            if (!Enabled) return;
            lock (locker)
            {
                if (!gpuHist.TryGetValue(name, out var h)) { h = new History(); gpuHist[name] = h; }
                h.Push(ms);
            }
        }

        public static float[] GetGpuHistory(string name)
        {
            lock (locker)
            {
                if (!gpuHist.TryGetValue(name, out var h)) return new float[HistoryLength];
                return h.Snapshot();
            }
        }

        public static float GpuMax(string name)
        {
            lock (locker)
            {
                if (!gpuHist.TryGetValue(name, out var h)) return 0f;
                return h.Max();
            }
        }

        public static float GpuMin(string name)
        {
            lock (locker)
            {
                if (!gpuHist.TryGetValue(name, out var h)) return 0f;
                return h.Min();
            }
        }

        public static float GpuAvg(string name)
        {
            lock (locker)
            {
                if (!gpuHist.TryGetValue(name, out var h)) return 0f;
                return h.Average();
            }
        }

        #endregion
    }
}
