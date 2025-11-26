using System;
using System.Linq;
using ImGuiNET;
using System.Numerics;

namespace Editor.UIManager.Profiling
{
    /// <summary>
    /// Real-time performance overlay that displays panel profiling data.
    /// Inspired by Unity/Unreal profilers with color-coded warnings.
    /// </summary>
    public static class PerformanceOverlay
    {
        private static bool _showOverlay = false;
        private static bool _showDetailed = false;
        private static OverlayPosition _position = OverlayPosition.TopRight;
        private static float _updateInterval = 0.5f; // Update display every 500ms
        private static float _timeSinceLastUpdate = 0f;

        // Cached display values (updated at intervals to reduce text flickering)
        private static double _cachedTotalMs;
        private static double _cachedFPS;
        private static string _cachedTopPanel = "";
        private static double _cachedTopPanelMs;

        public enum OverlayPosition
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        public static bool Visible
        {
            get => _showOverlay;
            set => _showOverlay = value;
        }

        public static bool ShowDetailed
        {
            get => _showDetailed;
            set => _showDetailed = value;
        }

        public static OverlayPosition Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>
        /// Draw the performance overlay. Call this after all panels have been drawn.
        /// </summary>
        public static void Draw(float deltaTime)
        {
            if (!_showOverlay) return;

            _timeSinceLastUpdate += deltaTime;

            // Update cached values at intervals
            if (_timeSinceLastUpdate >= _updateInterval)
            {
                _timeSinceLastUpdate = 0f;
                UpdateCachedValues();
            }

            // Position the overlay window
            var viewport = ImGui.GetMainViewport();
            var workPos = viewport.WorkPos;
            var workSize = viewport.WorkSize;

            Vector2 windowPos;
            Vector2 windowPosPivot;

            switch (_position)
            {
                case OverlayPosition.TopLeft:
                    windowPos = new Vector2(workPos.X + 10, workPos.Y + 30);
                    windowPosPivot = new Vector2(0, 0);
                    break;
                case OverlayPosition.TopRight:
                    windowPos = new Vector2(workPos.X + workSize.X - 10, workPos.Y + 30);
                    windowPosPivot = new Vector2(1, 0);
                    break;
                case OverlayPosition.BottomLeft:
                    windowPos = new Vector2(workPos.X + 10, workPos.Y + workSize.Y - 10);
                    windowPosPivot = new Vector2(0, 1);
                    break;
                case OverlayPosition.BottomRight:
                default:
                    windowPos = new Vector2(workPos.X + workSize.X - 10, workPos.Y + workSize.Y - 10);
                    windowPosPivot = new Vector2(1, 1);
                    break;
            }

            ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, windowPosPivot);
            ImGui.SetNextWindowBgAlpha(0.75f);

            var flags = ImGuiWindowFlags.NoDecoration
                      | ImGuiWindowFlags.AlwaysAutoResize
                      | ImGuiWindowFlags.NoSavedSettings
                      | ImGuiWindowFlags.NoFocusOnAppearing
                      | ImGuiWindowFlags.NoNav;

            if (ImGui.Begin("##PerformanceOverlay", flags))
            {
                DrawCompactOverlay();

                if (_showDetailed)
                {
                    ImGui.Separator();
                    DrawDetailedStats();
                }
            }
            ImGui.End();
        }

        private static void UpdateCachedValues()
        {
            _cachedTotalMs = PanelProfiler.GetTotalUITimeMs();
            _cachedFPS = _cachedTotalMs > 0 ? 1000.0 / _cachedTotalMs : 0;

            var topPanels = PanelProfiler.GetTopExpensivePanels(1);
            if (topPanels.Count > 0)
            {
                _cachedTopPanel = topPanels[0].Name;
                _cachedTopPanelMs = topPanels[0].AvgMs;
            }
            else
            {
                _cachedTopPanel = "None";
                _cachedTopPanelMs = 0;
            }
        }

        private static void DrawCompactOverlay()
        {
            ImGui.Text("UI Performance");
            ImGui.Separator();

            // Total UI time with color coding
            var color = GetColorForMs(_cachedTotalMs);
            ImGui.TextColored(color, $"Total UI: {_cachedTotalMs:F2} ms");

            // Estimated FPS from UI time alone
            var fpsColor = GetColorForFPS(_cachedFPS);
            ImGui.TextColored(fpsColor, $"UI FPS: {_cachedFPS:F0}");

            // Most expensive panel
            if (_cachedTopPanelMs > 0.1)
            {
                var panelColor = GetColorForMs(_cachedTopPanelMs);
                ImGui.TextColored(panelColor, $"Hotspot: {_cachedTopPanel}");
                ImGui.SameLine();
                ImGui.TextDisabled($"({_cachedTopPanelMs:F2}ms)");
            }

            // Toggle button for detailed view
            if (ImGui.SmallButton(_showDetailed ? "Hide Details" : "Show Details"))
            {
                _showDetailed = !_showDetailed;
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Reset"))
            {
                PanelProfiler.Reset();
            }
        }

        private static void DrawDetailedStats()
        {
            var allStats = PanelProfiler.GetAllStats();

            ImGui.Text("Panel Breakdown:");
            ImGui.Spacing();

            if (allStats.Count == 0)
            {
                ImGui.TextDisabled("No data yet...");
                return;
            }

            // Table with all panel stats
            if (ImGui.BeginTable("##PanelStats", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Panel", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Last", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Avg", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Max", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableHeadersRow();

                foreach (var (name, lastMs, avgMs, minMs, maxMs, calls) in allStats)
                {
                    if (avgMs < 0.01) continue; // Skip negligible panels

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.Text(name);

                    ImGui.TableNextColumn();
                    var lastColor = GetColorForMs(lastMs);
                    ImGui.TextColored(lastColor, $"{lastMs:F2}");

                    ImGui.TableNextColumn();
                    var avgColor = GetColorForMs(avgMs);
                    ImGui.TextColored(avgColor, $"{avgMs:F2}");

                    ImGui.TableNextColumn();
                    var maxColor = GetColorForMs(maxMs);
                    ImGui.TextColored(maxColor, $"{maxMs:F2}");
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.TextDisabled($"Tracking {allStats.Count} panels");
        }

        /// <summary>
        /// Color coding based on frame time budget.
        /// Green: <2ms (good), Yellow: 2-5ms (warning), Orange: 5-10ms (bad), Red: >10ms (critical)
        /// </summary>
        private static Vector4 GetColorForMs(double ms)
        {
            if (ms < 2.0)
                return new Vector4(0.2f, 1.0f, 0.2f, 1.0f); // Green
            else if (ms < 5.0)
                return new Vector4(1.0f, 1.0f, 0.2f, 1.0f); // Yellow
            else if (ms < 10.0)
                return new Vector4(1.0f, 0.6f, 0.0f, 1.0f); // Orange
            else
                return new Vector4(1.0f, 0.2f, 0.2f, 1.0f); // Red
        }

        /// <summary>
        /// Color coding for FPS display.
        /// </summary>
        private static Vector4 GetColorForFPS(double fps)
        {
            if (fps >= 240)
                return new Vector4(0.2f, 1.0f, 0.2f, 1.0f); // Green - excellent
            else if (fps >= 60)
                return new Vector4(0.5f, 1.0f, 0.5f, 1.0f); // Light green - good
            else if (fps >= 30)
                return new Vector4(1.0f, 1.0f, 0.2f, 1.0f); // Yellow - acceptable
            else
                return new Vector4(1.0f, 0.2f, 0.2f, 1.0f); // Red - bad
        }
    }
}
