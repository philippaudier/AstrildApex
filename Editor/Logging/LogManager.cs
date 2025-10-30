using System;
using System.Collections.Generic;
using System.Linq;

namespace Editor.Logging
{
    public static class LogManager
    {
        private static readonly List<LogEntry> _entries = new();
        private static readonly object _lock = new();
        public static event Action? OnLogChanged;

        public static IReadOnlyList<LogEntry> Entries => _entries;

        public static void Add(LogEntry entry)
        {
            lock (_lock)
            {
                // Collapse mode: group identical messages
                var last = _entries.LastOrDefault();
                if (last != null && last.Message == entry.Message && last.Level == entry.Level && last.StackTrace == entry.StackTrace)
                {
                    last.Count++;
                }
                else
                {
                    _entries.Add(entry);
                }
            }
            OnLogChanged?.Invoke();
        }

        public static void Clear()
        {
            lock (_lock) _entries.Clear();
            OnLogChanged?.Invoke();
        }

        // Utility methods for common logging scenarios
        public static void LogInfo(string message, string? source = null)
        {
            Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Info,
                Message = message,
                Source = source
            });
        }

        public static void LogWarning(string message, string? source = null)
        {
            Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Warning,
                Message = message,
                Source = source
            });
        }

        public static void LogError(string message, string? source = null)
        {
            Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Error,
                Message = message,
                Source = source
            });
        }

        public static void LogVerbose(string message, string? source = null)
        {
            Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = LogLevel.Verbose,
                Message = message,
                Source = source
            });
        }
    }
}
