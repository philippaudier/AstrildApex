using System;
using System.Numerics;
using ImGuiNET;
using Engine.Core;

namespace Editor.Panels
{
    /// <summary>
    /// Optional profiler panel for visualizing engine system performance
    /// Shows execution times, phase distribution, and system health
    /// </summary>
    public static class SystemsProfilerPanel
    {
        private static bool _isOpen = false;
        private static bool _profilingEnabled = false;

        public static bool IsOpen
        {
            get => _isOpen;
            set => _isOpen = value;
        }

        public static void Draw()
        {
            if (!_isOpen) return;

            ImGui.SetNextWindowSize(new Vector2(600, 400), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Systems Profiler", ref _isOpen, ImGuiWindowFlags.None))
            {
                var pipeline = EngineUpdatePipeline.Instance;

                // Header controls
                ImGui.Text("Engine Update Pipeline Profiler");
                ImGui.Separator();

                // Profiling toggle
                bool enabled = pipeline.ProfilingEnabled;
                if (ImGui.Checkbox("Enable Profiling", ref enabled))
                {
                    pipeline.ProfilingEnabled = enabled;
                    _profilingEnabled = enabled;
                }

                ImGui.SameLine();
                if (ImGui.Button("Clear Metrics"))
                {
                    pipeline.ClearMetrics();
                }

                ImGui.SameLine();
                if (ImGui.Button("Reset Pipeline"))
                {
                    pipeline.Reset();
                }

                if (!_profilingEnabled)
                {
                    ImGui.TextDisabled("Enable profiling to see system performance metrics");
                    ImGui.End();
                    return;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // System list grouped by phase
                if (ImGui.BeginTabBar("ProfilerTabs"))
                {
                    if (ImGui.BeginTabItem("By Phase"))
                    {
                        DrawSystemsByPhase(pipeline);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("All Systems"))
                    {
                        DrawAllSystems(pipeline);
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Performance"))
                    {
                        DrawPerformanceOverview(pipeline);
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
            ImGui.End();
        }

        private static void DrawSystemsByPhase(EngineUpdatePipeline pipeline)
        {
            foreach (UpdatePhase phase in Enum.GetValues(typeof(UpdatePhase)))
            {
                var systems = pipeline.GetSystemsInPhase(phase);
                if (systems.Count == 0) continue;

                if (ImGui.CollapsingHeader($"{phase} ({systems.Count} systems)", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Indent();

                    if (ImGui.BeginTable($"Table_{phase}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
                    {
                        ImGui.TableSetupColumn("System", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Enabled");
                        ImGui.TableSetupColumn("Avg (ms)");
                        ImGui.TableSetupColumn("Max (ms)");
                        ImGui.TableSetupColumn("Calls");
                        ImGui.TableHeadersRow();

                        foreach (var system in systems)
                        {
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            if (system.Enabled)
                            {
                                ImGui.Text(system.Name);
                            }
                            else
                            {
                                ImGui.TextDisabled(system.Name);
                            }

                            ImGui.TableNextColumn();
                            ImGui.Text(system.Enabled ? "✓" : "✗");

                            var (avgMs, maxMs, minMs, executions) = pipeline.GetSystemMetrics(system.Name);

                            ImGui.TableNextColumn();
                            if (executions > 0)
                            {
                                var color = avgMs > 16.0 ? new Vector4(1, 0.3f, 0.3f, 1) :
                                           avgMs > 8.0 ? new Vector4(1, 1, 0.3f, 1) :
                                           new Vector4(0.3f, 1, 0.3f, 1);
                                ImGui.TextColored(color, $"{avgMs:F2}");
                            }
                            else
                            {
                                ImGui.TextDisabled("N/A");
                            }

                            ImGui.TableNextColumn();
                            if (executions > 0)
                            {
                                ImGui.Text($"{maxMs:F2}");
                            }
                            else
                            {
                                ImGui.TextDisabled("N/A");
                            }

                            ImGui.TableNextColumn();
                            if (executions > 0)
                            {
                                ImGui.Text(executions.ToString());
                            }
                            else
                            {
                                ImGui.TextDisabled("0");
                            }
                        }

                        ImGui.EndTable();
                    }

                    ImGui.Unindent();
                    ImGui.Spacing();
                }
            }
        }

        private static void DrawAllSystems(EngineUpdatePipeline pipeline)
        {
            var allSystems = pipeline.GetAllSystems();

            if (ImGui.BeginTable("AllSystemsTable", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("System", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Phase");
                ImGui.TableSetupColumn("Priority");
                ImGui.TableSetupColumn("Enabled");
                ImGui.TableSetupColumn("Avg (ms)");
                ImGui.TableSetupColumn("Max (ms)");
                ImGui.TableHeadersRow();

                foreach (var system in allSystems)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    if (system.Enabled)
                    {
                        ImGui.Text(system.Name);
                    }
                    else
                    {
                        ImGui.TextDisabled(system.Name);
                    }

                    ImGui.TableNextColumn();
                    ImGui.Text(system.Phase.ToString());

                    ImGui.TableNextColumn();
                    ImGui.Text(system.Priority.ToString());

                    ImGui.TableNextColumn();
                    ImGui.Text(system.Enabled ? "✓" : "✗");

                    var (avgMs, maxMs, minMs, executions) = pipeline.GetSystemMetrics(system.Name);

                    ImGui.TableNextColumn();
                    if (executions > 0)
                    {
                        var color = avgMs > 16.0 ? new Vector4(1, 0.3f, 0.3f, 1) :
                                   avgMs > 8.0 ? new Vector4(1, 1, 0.3f, 1) :
                                   new Vector4(0.3f, 1, 0.3f, 1);
                        ImGui.TextColored(color, $"{avgMs:F3}");
                    }
                    else
                    {
                        ImGui.TextDisabled("N/A");
                    }

                    ImGui.TableNextColumn();
                    if (executions > 0)
                    {
                        ImGui.Text($"{maxMs:F3}");
                    }
                    else
                    {
                        ImGui.TextDisabled("N/A");
                    }
                }

                ImGui.EndTable();
            }
        }

        private static void DrawPerformanceOverview(EngineUpdatePipeline pipeline)
        {
            var metrics = pipeline.GetAllMetrics();

            ImGui.Text("Performance Summary");
            ImGui.Separator();

            double totalAvg = 0;
            double maxPeak = 0;
            int totalCalls = 0;

            foreach (var (name, (avgMs, maxMs, count)) in metrics)
            {
                totalAvg += avgMs;
                if (maxMs > maxPeak) maxPeak = maxMs;
                totalCalls += count;
            }

            ImGui.Text($"Total Systems: {metrics.Count}");
            ImGui.Text($"Total Avg Time: {totalAvg:F2} ms/frame");
            ImGui.Text($"Peak Time: {maxPeak:F2} ms");
            ImGui.Text($"Total Update Calls: {totalCalls}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Budget Analysis (60 FPS = 16.67 ms/frame)");
            ImGui.Separator();

            float budget = 16.67f;
            float usage = (float)totalAvg;
            float percent = (usage / budget) * 100f;

            var budgetColor = percent > 100f ? new Vector4(1, 0.2f, 0.2f, 1) :
                             percent > 80f ? new Vector4(1, 1, 0.2f, 1) :
                             new Vector4(0.2f, 1, 0.2f, 1);

            ImGui.ProgressBar(Math.Min(usage / budget, 1.0f), new Vector2(-1, 0), $"{usage:F2} / {budget:F2} ms ({percent:F1}%)");
            ImGui.TextColored(budgetColor, $"Frame Budget Usage: {percent:F1}%");

            if (percent > 100f)
            {
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(1, 0.2f, 0.2f, 1), "WARNING: Exceeding 60 FPS budget!");
                ImGui.Text("Consider optimizing systems or reducing workload.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Top 5 Most Expensive Systems:");
            if (ImGui.BeginTable("TopExpensiveTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("System");
                ImGui.TableSetupColumn("Avg (ms)");
                ImGui.TableSetupColumn("% of Budget");
                ImGui.TableHeadersRow();

                var sorted = new System.Collections.Generic.List<(string name, double avgMs, double maxMs, int count)>();
                foreach (var kvp in metrics)
                {
                    sorted.Add((kvp.Key, kvp.Value.avgMs, kvp.Value.maxMs, kvp.Value.count));
                }
                sorted.Sort((a, b) => b.avgMs.CompareTo(a.avgMs));

                int shown = 0;
                foreach (var (name, avgMs, _, _) in sorted)
                {
                    if (shown >= 5) break;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(name);

                    ImGui.TableNextColumn();
                    var color = avgMs > 5.0 ? new Vector4(1, 0.3f, 0.3f, 1) :
                               avgMs > 2.0 ? new Vector4(1, 1, 0.3f, 1) :
                               new Vector4(1, 1, 1, 1);
                    ImGui.TextColored(color, $"{avgMs:F2}");

                    ImGui.TableNextColumn();
                    float pct = (float)(avgMs / budget * 100f);
                    ImGui.Text($"{pct:F1}%");

                    shown++;
                }

                ImGui.EndTable();
            }
        }
    }
}
