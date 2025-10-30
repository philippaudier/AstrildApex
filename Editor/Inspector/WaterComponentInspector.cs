using System;
using ImGuiNET;
using Engine.Components;
using Engine.Scene;
using Engine.Assets;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector simplifié pour WaterComponent
    /// </summary>
    public static class WaterComponentInspector
    {
        public static void Draw(Entity entity, WaterComponent water)
        {
            ImGui.PushID(water.GetHashCode());

            ImGui.Text("🌊 Water Component");
            ImGui.Separator();

            // === WATER DIMENSIONS ===
            ImGui.Text("Dimensions");

            float width = water.WaterWidth;
            if (ImGui.DragFloat("Width", ref width, 1f, 1f, 10000f))
            {
                water.WaterWidth = width;
            }

            float length = water.WaterLength;
            if (ImGui.DragFloat("Length", ref length, 1f, 1f, 10000f))
            {
                water.WaterLength = length;
            }

            ImGui.Separator();

            // === MESH RESOLUTION ===
            ImGui.Text("Resolution");
            ImGui.TextDisabled("Higher = more detail, lower = better performance");

            int resolution = water.Resolution;
            if (ImGui.SliderInt("##Resolution", ref resolution, 4, 128))
            {
                water.Resolution = resolution;
            }

            ImGui.Separator();

            // === WATER MATERIAL ===
            ImGui.Text("Material");

            if (water.WaterMaterialGuid.HasValue && water.WaterMaterialGuid.Value != Guid.Empty)
            {
                if (AssetDatabase.TryGet(water.WaterMaterialGuid.Value, out var record))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f),
                        $"✓ {System.IO.Path.GetFileNameWithoutExtension(record.Path)}");

                    if (ImGui.Button("Change Material"))
                    {
                        water.WaterMaterialGuid = null;
                    }
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f),
                        "Material not found!");

                    if (ImGui.Button("Reset to Default"))
                    {
                        water.WaterMaterialGuid = Engine.Assets.AssetDatabase.EnsureDefaultWaterMaterial();
                    }
                }
            }
            else
            {
                if (ImGui.Button("Use Default Material"))
                {
                    water.WaterMaterialGuid = Engine.Assets.AssetDatabase.EnsureDefaultWaterMaterial();
                }
            }

            ImGui.Separator();

            // === STATS ===
            if (water.IsMeshGenerated)
            {
                ImGui.TextDisabled($"Triangles: {water.PatchCount * 2:N0}");
                ImGui.TextDisabled($"Vertices: {water.VertexCount:N0}");
            }

            // === ACTIONS ===
            if (ImGui.Button("Regenerate Mesh", new System.Numerics.Vector2(-1, 0)))
            {
                water.UpdateMesh();
            }

            ImGui.PopID();
        }
    }
}
