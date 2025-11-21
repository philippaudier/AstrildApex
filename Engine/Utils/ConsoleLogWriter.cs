using System;
using System.IO;
using System.Text;

namespace Engine.Utils
{
    /// <summary>
    /// TextWriter that forwards Console output to the engine DebugLogger (file-based).
    /// We intentionally keep this minimal: lines are forwarded to DebugLogger.Log and
    /// nothing else is written to the terminal to reduce terminal noise.
    /// </summary>
    public sealed class ConsoleLogWriter : TextWriter
    {
        private readonly StringBuilder _buffer = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            // Buffer characters until newline so DebugLogger.Log receives complete lines
            if (value == '\n')
            {
                var line = _buffer.ToString().TrimEnd('\r');
                if (!string.IsNullOrEmpty(line))
                {
                    try { DebugLogger.Log(line); } catch { }
                }
                _buffer.Clear();
            }
            else
            {
                _buffer.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            // Preserve existing behavior: split by newlines and forward each line
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    _buffer.Append(value.AsSpan(start, i - start));
                    var line = _buffer.ToString().TrimEnd('\r');
                    if (!string.IsNullOrEmpty(line))
                    {
                        try { DebugLogger.Log(line); } catch { }
                    }
                    _buffer.Clear();
                    start = i + 1;
                }
            }
            if (start < value.Length)
                _buffer.Append(value.AsSpan(start));
        }

        public override void WriteLine(string? value)
        {
            if (value == null) value = string.Empty;
            try { DebugLogger.Log(value); } catch { }
            _buffer.Clear();
        }
    }
}
