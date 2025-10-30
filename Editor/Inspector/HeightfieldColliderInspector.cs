using ImGuiNET;
using Engine.Components;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Heightfield Collider inspector
    /// </summary>
    public static class HeightfieldColliderInspector
    {
        public static void Draw(HeightfieldCollider hc)
        {
            if (hc?.Entity == null) return;
            uint entityId = hc.Entity.Id;

            // === COLLIDER SETTINGS ===
            if (InspectorWidgets.Section("Heightfield Collider", defaultOpen: true))
            {
                bool enabled = hc.Enabled;
                InspectorWidgets.Checkbox("Enabled", ref enabled, entityId, "Enabled",
                    tooltip: "Enable or disable this collider");
                hc.Enabled = enabled;

                bool isTrigger = hc.IsTrigger;
                InspectorWidgets.Checkbox("Is Trigger", ref isTrigger, entityId, "IsTrigger",
                    tooltip: "Trigger colliders detect overlaps but don't create physics contacts",
                    helpText: "Use for zones that detect when objects enter terrain areas");
                hc.IsTrigger = isTrigger;

                int layer = hc.Layer;
                InspectorWidgets.IntField("Layer", ref layer, min: 0, max: 31, entityId: entityId, fieldPath: "Layer",
                    tooltip: "Collision layer (0-31)",
                    helpText: "Objects on the same layer can be filtered together");
                hc.Layer = layer;

                InspectorWidgets.EndSection();
            }

            // === TERRAIN REFERENCE ===
            if (InspectorWidgets.Section("Terrain Reference", defaultOpen: true,
                tooltip: "The terrain component this collider represents"))
            {
                if (hc.TerrainRef != null)
                {
                    string terrainName = hc.TerrainRef.Entity?.Name ?? "Unknown";
                    InspectorWidgets.InfoBox($"Connected to Terrain: {terrainName}");
                    
                    ImGui.TextDisabled("Entity:");
                    ImGui.SameLine();
                    ImGui.Text(terrainName);
                }
                else
                {
                    InspectorWidgets.InfoBox("Auto-detected from same entity\n\nThe heightfield collider will automatically use the Terrain component on this entity for collision detection.");
                }

                InspectorWidgets.EndSection();
            }

            // === INFO ===
            if (InspectorWidgets.Section("Heightfield Info", defaultOpen: false,
                tooltip: "Information about heightfield collision"))
            {
                InspectorWidgets.InfoBox(
                    "Heightfield Collider uses terrain height data for efficient large-scale terrain collision.\n\n" +
                    "Features:\n" +
                    "• Optimized for large terrains\n" +
                    "• Uses terrain heightmap directly\n" +
                    "• Supports raycasting and physics\n" +
                    "• Automatically syncs with terrain changes");

                InspectorWidgets.EndSection();
            }
        }
    }
}
