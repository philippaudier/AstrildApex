using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Capsule Collider inspector
    /// </summary>
    public static class CapsuleColliderInspector
    {
        public static void Draw(CapsuleCollider cc)
        {
            if (cc?.Entity == null) return;
            uint entityId = cc.Entity.Id;

            // === COLLIDER SETTINGS ===
            if (InspectorWidgets.Section("Capsule Collider", defaultOpen: true))
            {
                bool enabled = cc.Enabled;
                InspectorWidgets.Checkbox("Enabled", ref enabled, entityId, "Enabled",
                    tooltip: "Enable or disable this collider");
                cc.Enabled = enabled;

                bool isTrigger = cc.IsTrigger;
                InspectorWidgets.Checkbox("Is Trigger", ref isTrigger, entityId, "IsTrigger",
                    tooltip: "Trigger colliders detect overlaps but don't create physics contacts",
                    helpText: "Use for zones, pickups, sensors. Non-trigger = solid collision");
                cc.IsTrigger = isTrigger;

                int layer = cc.Layer;
                InspectorWidgets.IntField("Layer", ref layer, min: 0, max: 31, entityId: entityId, fieldPath: "Layer",
                    tooltip: "Collision layer (0-31)",
                    helpText: "Objects on the same layer can be filtered together");
                cc.Layer = layer;

                InspectorWidgets.EndSection();
            }

            // === SHAPE ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true,
                tooltip: "Capsule collider dimensions"))
            {
                var center = cc.Center;
                InspectorWidgets.Vector3FieldOTK("Center", ref center, 0.01f, entityId, "Center",
                    tooltip: "Center point of the capsule in local space",
                    helpText: "Offset from the GameObject's pivot");
                cc.Center = center;

                float radius = cc.Radius;
                InspectorWidgets.FloatField("Radius", ref radius, entityId, "Radius",
                    speed: 0.01f, min: 0.001f, max: 1000f,
                    tooltip: "Radius of the capsule's hemisphere caps",
                    validate: (r) => r > 0 ? null : "Radius must be positive",
                    helpText: "Width of the capsule. For characters: typically 0.3-0.5");
                cc.Radius = radius;

                float height = cc.Height;
                InspectorWidgets.FloatField("Height", ref height, entityId, "Height",
                    speed: 0.01f, min: 0.001f, max: 1000f,
                    tooltip: "Total height of the capsule including both hemisphere caps",
                    validate: (h) => h > cc.Radius * 2 ? null : "Height must be greater than radius × 2",
                    helpText: "For standing characters: typically 1.6-2.0");
                cc.Height = height;

                if (cc.Radius <= 0 || cc.Height <= 0)
                    InspectorWidgets.WarningBox("Collider dimensions have zero or negative values!");
                
                if (cc.Height < cc.Radius * 2)
                    InspectorWidgets.WarningBox("Height is less than diameter! Capsule may appear squashed.");

                // Direction
                InspectorWidgets.DisabledLabel("Direction:");
                string[] directionNames = { "X-Axis (Horizontal Right)", "Y-Axis (Vertical Up)", "Z-Axis (Horizontal Forward)" };
                int direction = cc.Direction;
                
                ImGui.SetNextItemWidth(200);
                if (ImGui.Combo("##Direction", ref direction, directionNames, directionNames.Length))
                {
                    cc.Direction = direction;
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Axis along which the capsule is oriented\n\nY-Axis: standing characters\nX/Z-Axis: lying objects, rolling barrels");
                }

                // Shape presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Character", "Standing human"),
                    ("Crouched", "Crouching pose"),
                    ("Lying", "Horizontal"));
                
                if (preset == 0) // Character
                {
                    cc.Radius = 0.4f;
                    cc.Height = 1.8f;
                    cc.Direction = 1; // Y-Axis
                }
                else if (preset == 1) // Crouched
                {
                    cc.Radius = 0.4f;
                    cc.Height = 1.0f;
                    cc.Direction = 1; // Y-Axis
                }
                else if (preset == 2) // Lying
                {
                    cc.Radius = 0.3f;
                    cc.Height = 1.8f;
                    cc.Direction = 0; // X-Axis
                }

                InspectorWidgets.EndSection();
            }
        }
    }
}
