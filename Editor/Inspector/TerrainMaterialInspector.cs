using System;
using ImGuiNET;
using Engine.Assets;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector for TerrainForward materials - shows ONLY global terrain settings.
    /// Layers are now managed in the Terrain component inspector, not here.
    /// </summary>
    public static class TerrainMaterialInspector
    {
        public static void Draw(MaterialAsset mat)
        {
            if (mat == null) return;

            ImGui.PushID("TerrainMaterial");

            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.8f, 1f, 1f), "Terrain Material (Global Settings)");
            ImGui.Separator();
            ImGui.TextWrapped("This material uses the TerrainForward shader. Layers are configured in the Terrain component inspector, not here.");
            ImGui.Spacing();

            // Show a helpful message
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.8f, 0.2f, 1f), "ℹ How to use:");
            ImGui.Bullet(); ImGui.Text("Assign this material to a Terrain component");
            ImGui.Bullet(); ImGui.Text("Configure layers in the Terrain component inspector");
            ImGui.Bullet(); ImGui.Text("Each layer can reference a ForwardBase material");

            ImGui.Spacing();
            ImGui.Separator();

            // TODO: Add global terrain material parameters here
            // Examples:
            // - Global triplanar sharpness
            // - Global quality settings
            // - Debug visualization options

            ImGui.TextDisabled("(No global parameters yet)");

            ImGui.PopID();
        }
    }
}
