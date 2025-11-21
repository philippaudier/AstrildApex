using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Editor.Utils
{
    /// <summary>
    /// Simple profiler to measure startup time and identify bottlenecks
    /// </summary>
    public static class StartupProfiler
    {
        private class ProfileEntry
        {
            public string Name;
            public long ElapsedMs;
            public int Depth;

            public ProfileEntry(string name, long elapsedMs, int depth)
            {
                Name = name;
                ElapsedMs = elapsedMs;
                Depth = depth;
            }
        }

        private static readonly List<ProfileEntry> _entries = new();
        private static readonly Stack<(string Name, Stopwatch Watch, int Depth)> _stack = new();
        private static readonly Stopwatch _totalTimer = new();
        private static int _currentDepth = 0;
        private static bool _enabled = true;

        public static void Start()
        {
            _entries.Clear();
            _stack.Clear();
            _currentDepth = 0;
            _totalTimer.Restart();
            try { Engine.Utils.DebugLogger.Log("╔════════════════════════════════════════════════════════════════════╗"); } catch { }
            try { Engine.Utils.DebugLogger.Log("║          STARTUP PROFILER - Measuring Load Times                   ║"); } catch { }
            try { Engine.Utils.DebugLogger.Log("╚════════════════════════════════════════════════════════════════════╝"); } catch { }
        }

        public static void BeginSection(string name)
        {
            if (!_enabled) return;

            var sw = Stopwatch.StartNew();
            _stack.Push((name, sw, _currentDepth));
            _currentDepth++;
        }

        public static void EndSection()
        {
            if (!_enabled || _stack.Count == 0) return;

            var (name, watch, depth) = _stack.Pop();
            watch.Stop();
            _currentDepth = depth;

            _entries.Add(new ProfileEntry(name, watch.ElapsedMilliseconds, depth));
        }

        public static void PrintReport()
        {
            if (!_enabled) return;

            _totalTimer.Stop();

            try { Engine.Utils.DebugLogger.Log("\n╔════════════════════════════════════════════════════════════════════╗"); } catch { }
            try { Engine.Utils.DebugLogger.Log("║                    STARTUP PROFILING REPORT                        ║"); } catch { }
            try { Engine.Utils.DebugLogger.Log("╠════════════════════════════════════════════════════════════════════╣"); } catch { }

            // Sort entries by time (descending) to show bottlenecks first
            var sortedByTime = _entries.OrderByDescending(e => e.ElapsedMs).ToList();

            try { Engine.Utils.DebugLogger.Log("║ TOP BOTTLENECKS (by time):                                         ║"); } catch { }
            try { Engine.Utils.DebugLogger.Log("╟────────────────────────────────────────────────────────────────────╢"); } catch { }

            for (int i = 0; i < Math.Min(10, sortedByTime.Count); i++)
            {
                var entry = sortedByTime[i];
                var percentage = (_totalTimer.ElapsedMilliseconds > 0)
                    ? (entry.ElapsedMs * 100.0 / _totalTimer.ElapsedMilliseconds)
                    : 0;

                var bar = GenerateBar((int)percentage);
                var indent = new string(' ', entry.Depth * 2);

                try { Engine.Utils.DebugLogger.Log($"║ {i+1,2}. {indent}{entry.Name,-40} {entry.ElapsedMs,6}ms {percentage,5:F1}% {bar}"); } catch { }
            }

            try { Engine.Utils.DebugLogger.Log("╟────────────────────────────────────────────────────────────────────╢"); } catch { }
            try { Engine.Utils.DebugLogger.Log("║ COMPLETE TIMELINE:                                                 ║"); } catch { }
            try { Engine.Utils.DebugLogger.Log("╟────────────────────────────────────────────────────────────────────╢"); } catch { }

            long cumulative = 0;
            foreach (var entry in _entries)
            {
                cumulative += entry.ElapsedMs;
                var percentage = (_totalTimer.ElapsedMilliseconds > 0)
                    ? (entry.ElapsedMs * 100.0 / _totalTimer.ElapsedMilliseconds)
                    : 0;

                var indent = new string(' ', entry.Depth * 2);
                var arrow = entry.Depth > 0 ? "└─ " : "▶ ";

                try { Engine.Utils.DebugLogger.Log($"║ {indent}{arrow}{entry.Name,-45} {entry.ElapsedMs,6}ms {percentage,5:F1}%"); } catch { }
            }

            try { Engine.Utils.DebugLogger.Log("╠════════════════════════════════════════════════════════════════════╣"); } catch { }
            try { Engine.Utils.DebugLogger.Log($"║ TOTAL STARTUP TIME: {_totalTimer.ElapsedMilliseconds,6} ms ({_totalTimer.Elapsed.TotalSeconds:F2}s)"); } catch { }

            // Identify slow sections (> 500ms)
            var slowSections = _entries.Where(e => e.ElapsedMs > 500).ToList();
            if (slowSections.Any())
            {
                try { Engine.Utils.DebugLogger.Log("╟────────────────────────────────────────────────────────────────────╢"); } catch { }
                try { Engine.Utils.DebugLogger.Log("║ ⚠️  SLOW SECTIONS (>500ms):                                        ║"); } catch { }
                foreach (var entry in slowSections)
                {
                    try { Engine.Utils.DebugLogger.Log($"║    • {entry.Name,-50} {entry.ElapsedMs,6}ms"); } catch { }
                }
            }

            try { Engine.Utils.DebugLogger.Log("╚════════════════════════════════════════════════════════════════════╝\n"); } catch { }
        }

        private static string GenerateBar(int percentage)
        {
            const int maxBarLength = 10;
            var barLength = Math.Min(maxBarLength, percentage / 10);
            var bar = new string('█', barLength);
            var empty = new string('░', maxBarLength - barLength);
            return $"[{bar}{empty}]";
        }

        public static void Disable()
        {
            _enabled = false;
        }

        public static void Enable()
        {
            _enabled = true;
        }
    }
}
