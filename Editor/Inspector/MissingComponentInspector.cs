using System.Numerics;
using ImGuiNET;
using Engine.Components;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector for MissingComponent - displays a warning when a component type is missing
    /// </summary>
    public static class MissingComponentInspector
    {
        public static void Draw(MissingComponent missing)
        {
            if (missing == null || missing.Entity == null) return;

            // Draw warning box with red/yellow colors
            var warningColor = new Vector4(1.0f, 0.8f, 0.2f, 1.0f); // Yellow/orange
            var errorColor = new Vector4(1.0f, 0.3f, 0.3f, 1.0f);   // Red

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.3f, 0.15f, 0.15f, 0.9f));
            ImGui.BeginChild("MissingComponentWarning", new Vector2(0, 160), ImGuiChildFlags.Borders);

            // Warning icon + title
            ImGui.PushStyleColor(ImGuiCol.Text, warningColor);
            ImGui.SetWindowFontScale(1.3f);
            ImGui.Text("⚠ Missing Component");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopStyleColor();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Component info
            ImGui.Text("Type:");
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, errorColor);
            ImGui.Text(missing.MissingTypeName);
            ImGui.PopStyleColor();

            ImGui.Spacing();

            ImGui.TextWrapped($"The component '{missing.MissingTypeName}' is missing. It may have been removed from the engine or the script file was deleted.");

            ImGui.Spacing();
            ImGui.TextDisabled($"Marked missing: {missing.MarkedMissingAt:yyyy-MM-dd HH:mm:ss}");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Action buttons
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));

            if (ImGui.Button("Remove Missing Component", new Vector2(-1, 0)))
            {
                // Avoid using `missing.Entity` after removal since RemoveComponent may clear
                // the component's back-reference. Capture values first, then remove.
                var ent = missing.Entity;
                var typeName = missing.MissingTypeName ?? "(unknown)";
                if (ent != null)
                {
                    ent.RemoveComponent<MissingComponent>();
                    Editor.Panels.InspectorPanel.InvalidateComponentCache();
                    Console.WriteLine($"[MissingComponentInspector] Removed missing component '{typeName}' from entity '{ent.Name}'");
                }
                else
                {
                    Console.WriteLine($"[MissingComponentInspector] Removed missing component '{typeName}' (entity was null)");
                }
            }

            ImGui.PopStyleColor(3);

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Remove this placeholder component.\nThe original data will be lost.");
            }

            ImGui.EndChild();
            ImGui.PopStyleColor(); // ChildBg

            // Optional: Show preserved data for debugging
            if (missing.SerializedData != null && missing.SerializedData.Count > 0)
            {
                ImGui.Spacing();
                if (ImGui.TreeNode("Preserved Data (Debug)"))
                {
                    ImGui.TextDisabled("Original component data (read-only):");
                    ImGui.Separator();

                    foreach (var kvp in missing.SerializedData)
                    {
                        ImGui.BulletText($"{kvp.Key}: {kvp.Value}");
                    }

                    ImGui.TreePop();
                }
            }
        }
    }
}
