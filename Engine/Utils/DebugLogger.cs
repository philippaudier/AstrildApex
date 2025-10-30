using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Engine.Utils
{
    public static class DebugLogger
    {
        private static readonly object _lock = new();
        private static readonly string _path;
        // When false (default) verbose per-frame logs are suppressed. Set to true for debugging.
        public static bool EnableVerbose { get; set; } = false;

        static DebugLogger()
        {
            try
            {
                var cwd = Environment.CurrentDirectory ?? AppContext.BaseDirectory ?? ".";
                _path = Path.Combine(cwd, "astrild_debug.log");
            }
            catch
            {
                _path = "astrild_debug.log";
            }
        }

        public static void Log(string msg)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_path, DateTime.Now.ToString("o") + " " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* best-effort logging */ }
        }
    }
}
