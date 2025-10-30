using System;

namespace Editor.Logging
{
    public enum LogLevel
    {
        Verbose,  // Gris clair
        Info,     // Blanc
        Warning,  // Jaune/Orange
        Error,    // Rouge
        Fatal     // Rouge foncé
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? Source { get; set; }
        public int Count { get; set; } = 1;
    }
}
