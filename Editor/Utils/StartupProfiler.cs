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
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║          STARTUP PROFILER - Measuring Load Times                   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
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

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    STARTUP PROFILING REPORT                        ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");

            // Sort entries by time (descending) to show bottlenecks first
            var sortedByTime = _entries.OrderByDescending(e => e.ElapsedMs).ToList();

            Console.WriteLine("║ TOP BOTTLENECKS (by time):                                         ║");
            Console.WriteLine("╟────────────────────────────────────────────────────────────────────╢");

            for (int i = 0; i < Math.Min(10, sortedByTime.Count); i++)
            {
                var entry = sortedByTime[i];
                var percentage = (_totalTimer.ElapsedMilliseconds > 0)
                    ? (entry.ElapsedMs * 100.0 / _totalTimer.ElapsedMilliseconds)
                    : 0;

                var bar = GenerateBar((int)percentage);
                var indent = new string(' ', entry.Depth * 2);

                Console.WriteLine($"║ {i+1,2}. {indent}{entry.Name,-40} {entry.ElapsedMs,6}ms {percentage,5:F1}% {bar}");
            }

            Console.WriteLine("╟────────────────────────────────────────────────────────────────────╢");
            Console.WriteLine("║ COMPLETE TIMELINE:                                                 ║");
            Console.WriteLine("╟────────────────────────────────────────────────────────────────────╢");

            long cumulative = 0;
            foreach (var entry in _entries)
            {
                cumulative += entry.ElapsedMs;
                var percentage = (_totalTimer.ElapsedMilliseconds > 0)
                    ? (entry.ElapsedMs * 100.0 / _totalTimer.ElapsedMilliseconds)
                    : 0;

                var indent = new string(' ', entry.Depth * 2);
                var arrow = entry.Depth > 0 ? "└─ " : "▶ ";

                Console.WriteLine($"║ {indent}{arrow}{entry.Name,-45} {entry.ElapsedMs,6}ms {percentage,5:F1}%");
            }

            Console.WriteLine("╠════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ TOTAL STARTUP TIME: {_totalTimer.ElapsedMilliseconds,6} ms ({_totalTimer.Elapsed.TotalSeconds:F2}s)");

            // Identify slow sections (> 500ms)
            var slowSections = _entries.Where(e => e.ElapsedMs > 500).ToList();
            if (slowSections.Any())
            {
                Console.WriteLine("╟────────────────────────────────────────────────────────────────────╢");
                Console.WriteLine("║ ⚠️  SLOW SECTIONS (>500ms):                                        ║");
                foreach (var entry in slowSections)
                {
                    Console.WriteLine($"║    • {entry.Name,-50} {entry.ElapsedMs,6}ms");
                }
            }

            Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝\n");
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
