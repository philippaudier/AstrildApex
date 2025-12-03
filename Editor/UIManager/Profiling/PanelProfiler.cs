using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Editor.UIManager.Profiling
{
    /// <summary>
    /// Lightweight profiler that measures CPU time for each panel's Draw() call.
    /// Tracks min/max/avg over a rolling window for accurate performance analysis.
    /// </summary>
    public static class PanelProfiler
    {
        private class PanelStats
        {
            public string Name;
            public Stopwatch Timer = new Stopwatch();
            public List<double> Samples = new List<double>(120); // 2 seconds at 60 FPS
            public double LastFrameMs;
            public double MinMs = double.MaxValue;
            public double MaxMs;
            public double AvgMs;
            public long TotalCalls;
            public bool IsCurrentlyDrawing;

            public PanelStats(string name)
            {
                Name = name;
            }

            public void RecordSample(double ms)
            {
                LastFrameMs = ms;
                TotalCalls++;

                // Update min/max
                if (ms < MinMs) MinMs = ms;
                if (ms > MaxMs) MaxMs = ms;

                // Rolling window
                Samples.Add(ms);
                if (Samples.Count > 120)
                    Samples.RemoveAt(0);

                // Recalculate average
                AvgMs = Samples.Average();
            }

            public void Reset()
            {
                Samples.Clear();
                MinMs = double.MaxValue;
                MaxMs = 0;
                AvgMs = 0;
                TotalCalls = 0;
            }
        }

        private static readonly Dictionary<string, PanelStats> _stats = new Dictionary<string, PanelStats>();
        private static readonly object _lock = new object();
        private static bool _enabled = false; // DISABLED FOR PERFORMANCE TESTING

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// Begin timing a panel's Draw() call.
        /// </summary>
        public static void BeginPanel(string panelName)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                if (!_stats.TryGetValue(panelName, out var stats))
                {
                    stats = new PanelStats(panelName);
                    _stats[panelName] = stats;
                }

                stats.IsCurrentlyDrawing = true;
                stats.Timer.Restart();
            }
        }

        /// <summary>
        /// End timing a panel's Draw() call and record the result.
        /// </summary>
        public static void EndPanel(string panelName)
        {
            if (!_enabled) return;

            lock (_lock)
            {
                if (_stats.TryGetValue(panelName, out var stats))
                {
                    stats.Timer.Stop();
                    stats.IsCurrentlyDrawing = false;
                    stats.RecordSample(stats.Timer.Elapsed.TotalMilliseconds);
                }
            }
        }

        /// <summary>
        /// Get the last frame time for a specific panel.
        /// </summary>
        public static double GetLastFrameMs(string panelName)
        {
            lock (_lock)
            {
                return _stats.TryGetValue(panelName, out var stats) ? stats.LastFrameMs : 0;
            }
        }

        /// <summary>
        /// Get average frame time for a specific panel.
        /// </summary>
        public static double GetAverageMs(string panelName)
        {
            lock (_lock)
            {
                return _stats.TryGetValue(panelName, out var stats) ? stats.AvgMs : 0;
            }
        }

        /// <summary>
        /// Get all panel statistics sorted by average time (most expensive first).
        /// </summary>
        public static List<(string Name, double LastMs, double AvgMs, double MinMs, double MaxMs, long Calls)> GetAllStats()
        {
            lock (_lock)
            {
                return _stats.Values
                    .OrderByDescending(s => s.AvgMs)
                    .Select(s => (s.Name, s.LastFrameMs, s.AvgMs, s.MinMs, s.MaxMs, s.TotalCalls))
                    .ToList();
            }
        }

        /// <summary>
        /// Get total UI time for the last frame (sum of all panels).
        /// </summary>
        public static double GetTotalUITimeMs()
        {
            lock (_lock)
            {
                return _stats.Values.Sum(s => s.LastFrameMs);
            }
        }

        /// <summary>
        /// Reset all statistics.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                foreach (var stats in _stats.Values)
                {
                    stats.Reset();
                }
            }
        }

        /// <summary>
        /// Clear all tracked panels.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _stats.Clear();
            }
        }

        /// <summary>
        /// Check if any panel is currently being timed (for debugging).
        /// </summary>
        public static bool IsAnyPanelDrawing()
        {
            lock (_lock)
            {
                return _stats.Values.Any(s => s.IsCurrentlyDrawing);
            }
        }

        /// <summary>
        /// Get the top N most expensive panels by average time.
        /// </summary>
        public static List<(string Name, double AvgMs)> GetTopExpensivePanels(int count = 3)
        {
            lock (_lock)
            {
                return _stats.Values
                    .OrderByDescending(s => s.AvgMs)
                    .Take(count)
                    .Select(s => (s.Name, s.AvgMs))
                    .ToList();
            }
        }
    }
}
