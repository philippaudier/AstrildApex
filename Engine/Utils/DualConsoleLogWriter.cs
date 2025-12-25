using System;
using System.IO;
using System.Text;

namespace Engine.Utils
{
    /// <summary>
    /// TextWriter that forwards Console output to both the original console and the engine DebugLogger.
    /// Use this when verbose logging is enabled so DebugLogger entries also appear in the terminal.
    /// </summary>
    public sealed class DualConsoleLogWriter : TextWriter
    {
        private readonly TextWriter _original;
        private readonly StringBuilder _buffer = new();

        public DualConsoleLogWriter(TextWriter original)
        {
            _original = original ?? Console.Out;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _original.Write(value);
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
            if (!string.IsNullOrEmpty(value))
            {
                _original.Write(value);
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
        }

        public override void WriteLine(string? value)
        {
            _original.WriteLine(value);
            if (value == null) value = string.Empty;
            try { DebugLogger.Log(value); } catch { }
            _buffer.Clear();
        }
    }
}
