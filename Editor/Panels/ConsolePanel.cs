using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ImGuiNET;
using Editor.Logging;
using System.Numerics;
using Editor.Icons;

namespace Editor.Panels
{
    public static class ConsolePanel
    {
        private static int _selectedIndex = -1;
        private static string _search = string.Empty;
        private static bool _autoScroll = true;
        private static bool _collapse = false;
        private static bool _showTimestamps = true;
        private static bool _errorPause = false;
        private static Vector2 _lastScroll = Vector2.Zero;
        private static LogLevel[] _filterLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToArray();
        private static readonly Dictionary<LogLevel, bool> _levelFilters = new()
        {
            // Hide Verbose by default to reduce noisy logs in the Console panel.
            { LogLevel.Verbose, false },
            { LogLevel.Info, true },
            { LogLevel.Warning, true },
            { LogLevel.Error, true },
            { LogLevel.Fatal, true }
        };

        public static void Draw()
        {
            ImGui.Begin("Console");
            DrawToolbar();
            DrawFilters();
            DrawLogList();
            DrawDetailsPanel();
            ImGui.End();
        }

        private static void DrawToolbar()
        {
            if (IconManager.IconButton("delete", "Clear Console")) LogManager.Clear();
            ImGui.SameLine();
            if (ImGui.Button("Collapse")) _collapse = !_collapse;
            ImGui.SameLine();
            if (ImGui.Button("Error Pause")) _errorPause = !_errorPause;
            ImGui.SameLine();
            ImGui.Checkbox("Auto-scroll", ref _autoScroll);
            ImGui.SameLine();
            ImGui.Checkbox("Timestamps", ref _showTimestamps);
            ImGui.SameLine();
            if (ImGui.Button("Export logs")) ExportLogs();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            ImGui.InputTextWithHint("##search", "Search...", ref _search, 128);
        }

        private static void DrawFilters()
        {
            foreach (var level in _filterLevels)
            {
                ImGui.SameLine();
                var count = LogManager.Entries.Count(e => e.Level == level);
                var label = $"{level} ({count})";
                bool enabled = _levelFilters[level];
                if (ImGuiToggleButtonExtension.ToggleButton(label, ref enabled))
                    _levelFilters[level] = enabled;
            }
        }

        private static void DrawLogList()
        {
            ImGui.BeginChild("##loglist", new Vector2(0, -120), ImGuiChildFlags.Borders);
            var entries = LogManager.Entries
                .Where(e => _levelFilters[e.Level])
                .Where(e => string.IsNullOrWhiteSpace(_search) || e.Message.Contains(_search, StringComparison.OrdinalIgnoreCase))
                .ToList();
            int idx = 0;
            foreach (var entry in entries)
            {
                var color = GetColor(entry.Level);
                ImGui.PushStyleColor(ImGuiCol.Text, color);

                // Ensure each selectable has a unique ID so identical messages don't collide.
                ImGui.PushID(idx);
                string msg = _showTimestamps ? $"[{entry.Timestamp:HH:mm:ss}] {entry.Message}" : entry.Message;
                if (ImGui.Selectable(msg, _selectedIndex == idx))
                    _selectedIndex = idx;
                if (_collapse && entry.Count > 1)
                {
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"x{entry.Count}");
                }
                ImGui.PopID();

                ImGui.PopStyleColor();
                idx++;
            }
            if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 5)
                ImGui.SetScrollHereY(1.0f);
            ImGui.EndChild();
        }

        private static void DrawDetailsPanel()
        {
            ImGui.BeginChild("##logdetails", new Vector2(0, 100), ImGuiChildFlags.Borders);
            var entries = LogManager.Entries
                .Where(e => _levelFilters[e.Level])
                .Where(e => string.IsNullOrWhiteSpace(_search) || e.Message.Contains(_search, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (_selectedIndex >= 0 && _selectedIndex < entries.Count)
            {
                var entry = entries[_selectedIndex];
                ImGui.TextWrapped(entry.Message);
                if (!string.IsNullOrWhiteSpace(entry.StackTrace))
                {
                    ImGui.Separator();
                    ImGui.TextDisabled("Stack Trace (click on a line to open in editor):");
                    ImGui.Spacing();
                    
                    // Parse and display stack trace with clickable file references
                    DrawClickableStackTrace(entry.StackTrace);
                }
                if (!string.IsNullOrWhiteSpace(entry.Source))
                {
                    ImGui.Separator();
                    ImGui.TextDisabled($"Source: {entry.Source}");
                }
                if (ImGui.Button("Copy message")) ImGui.SetClipboardText(entry.Message);
                ImGui.SameLine();
                if (!string.IsNullOrWhiteSpace(entry.StackTrace) && ImGui.Button("Copy stacktrace")) ImGui.SetClipboardText(entry.StackTrace);
            }
            ImGui.EndChild();
        }

        private static void ExportLogs()
        {
            try
            {
                var path = $"console_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                System.IO.File.WriteAllLines(path, LogManager.Entries.Select(e => $"[{e.Timestamp:HH:mm:ss}] [{e.Level}] {e.Message}\n{e.StackTrace}"));
            }
            catch (Exception)
            {
                // Optionally: show notification
            }
        }

        private static uint GetColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Verbose => ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.7f, 1f)),
                LogLevel.Info => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                LogLevel.Warning => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.8f, 0.2f, 1f)),
                LogLevel.Error => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.3f, 0.3f, 1f)),
                LogLevel.Fatal => ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.1f, 0.1f, 1f)),
                _ => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f))
            };
        }
        
        /// <summary>
        /// Draw stack trace with clickable file:line references.
        /// Parses patterns like "at Namespace.Class.Method() in C:\path\to\file.cs:line 42"
        /// </summary>
        private static void DrawClickableStackTrace(string stackTrace)
        {
            // Regex patterns for common stack trace formats:
            // 1. "at ... in C:\path\file.cs:line 123" (C# stack trace)
            // 2. "C:\path\file.cs(123,45)" (compiler error format)
            var lineByLinePattern = @"at .+ in (.+):line (\d+)";
            var compilerPattern = @"(.+\.cs)\((\d+),\d+\)";
            
            var lines = stackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                // Try to match C# stack trace format
                var match = Regex.Match(line, lineByLinePattern);
                if (match.Success)
                {
                    string filePath = match.Groups[1].Value;
                    int lineNumber = int.Parse(match.Groups[2].Value);
                    
                    DrawClickableStackTraceLine(line, filePath, lineNumber);
                    continue;
                }
                
                // Try to match compiler error format
                match = Regex.Match(line, compilerPattern);
                if (match.Success)
                {
                    string filePath = match.Groups[1].Value;
                    int lineNumber = int.Parse(match.Groups[2].Value);
                    
                    DrawClickableStackTraceLine(line, filePath, lineNumber);
                    continue;
                }
                
                // No match - just display as normal text
                ImGui.TextWrapped(line);
            }
        }
        
        /// <summary>
        /// Draw a single stack trace line as a clickable button/text.
        /// </summary>
        private static void DrawClickableStackTraceLine(string displayText, string filePath, int lineNumber)
        {
            // Color the text based on whether the file exists
            bool fileExists = File.Exists(filePath);
            var textColor = fileExists 
                ? new Vector4(0.4f, 0.7f, 1.0f, 1f)  // Light blue for clickable
                : new Vector4(0.7f, 0.7f, 0.7f, 1f); // Gray for non-existent files
            
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            
            // Make it selectable to be clickable
            if (ImGui.Selectable(displayText))
            {
                if (fileExists)
                {
                    State.EditorSettings.OpenScript(filePath, lineNumber);
                }
            }
            
            // Show tooltip
            if (ImGui.IsItemHovered())
            {
                if (fileExists)
                {
                    ImGui.SetTooltip($"Click to open {Path.GetFileName(filePath)} at line {lineNumber}");
                }
                else
                {
                    ImGui.SetTooltip($"File not found: {filePath}");
                }
            }
            
            ImGui.PopStyleColor();
        }
    }

    // Extension pour ToggleButton simple
    public static class ImGuiToggleButtonExtension
    {
        public static bool ToggleButton(string label, ref bool v)
        {
            bool pressed = ImGui.Button(label + (v ? " [x]" : " [ ]"));
            if (pressed) v = !v;
            return pressed;
        }
    }
}
