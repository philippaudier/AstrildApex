using Serilog.Core;
using Serilog.Events;
using System;

namespace Editor.Logging
{
    public class ConsolePanelSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent)
        {
            var entry = new LogEntry
            {
                Timestamp = logEvent.Timestamp.DateTime,
                Level = logEvent.Level switch
                {
                    LogEventLevel.Verbose => LogLevel.Verbose,
                    LogEventLevel.Debug => LogLevel.Verbose,
                    LogEventLevel.Information => LogLevel.Info,
                    LogEventLevel.Warning => LogLevel.Warning,
                    LogEventLevel.Error => LogLevel.Error,
                    LogEventLevel.Fatal => LogLevel.Fatal,
                    _ => LogLevel.Info
                },
                Message = logEvent.RenderMessage(),
                StackTrace = logEvent.Exception?.ToString(),
                Source = logEvent.Properties.TryGetValue("SourceContext", out var src) ? src.ToString() : null
            };
            LogManager.Add(entry);
        }
    }
}
