using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Box Collider inspector
    /// </summary>
    public static class BoxColliderInspector
    {
        public static void Draw(BoxCollider bc)
        {
            if (bc?.Entity == null) return;
            uint entityId = bc.Entity.Id;

            // === COLLIDER SETTINGS ===
            if (InspectorWidgets.Section("Box Collider", defaultOpen: true))
            {
                bool enabled = bc.Enabled;
                InspectorWidgets.Checkbox("Enabled", ref enabled, entityId, "Enabled",
                    tooltip: "Enable or disable this collider");
                bc.Enabled = enabled;

                bool isTrigger = bc.IsTrigger;
                InspectorWidgets.Checkbox("Is Trigger", ref isTrigger, entityId, "IsTrigger",
                    tooltip: "Trigger colliders detect overlaps but don't create physics contacts",
                    helpText: "Use for zones, pickups, sensors. Non-trigger = solid collision");
                bc.IsTrigger = isTrigger;

                int layer = bc.Layer;
                InspectorWidgets.IntField("Layer", ref layer, min: 0, max: 31, entityId: entityId, fieldPath: "Layer",
                    tooltip: "Collision layer (0-31)",
                    helpText: "Objects on the same layer can be filtered together");
                bc.Layer = layer;

                InspectorWidgets.EndSection();
            }

            // === SHAPE ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true,
                tooltip: "Box collider dimensions"))
            {
                var center = bc.Center;
                InspectorWidgets.Vector3FieldOTK("Center", ref center, 0.01f, entityId, "Center",
                    tooltip: "Center point of the box in local space",
                    helpText: "Offset from the GameObject's pivot");
                bc.Center = center;

                var size = bc.Size;
                InspectorWidgets.Vector3FieldOTK("Size", ref size, 0.01f, entityId, "Size",
                    tooltip: "Size of the box in each axis",
                    validate: (s) => (s.X > 0 && s.Y > 0 && s.Z > 0) ? null : "Size components must be positive",
                    helpText: "Width (X), Height (Y), Depth (Z) in local units");
                bc.Size = size;

                if (bc.Size.X <= 0 || bc.Size.Y <= 0 || bc.Size.Z <= 0)
                    InspectorWidgets.WarningBox("Collider size has zero or negative dimensions!");

                // Size presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Unit Cube", "1x1x1 cube"),
                    ("Character", "1x2x1 character"),
                    ("Wall", "0.1x3x5 wall"));
                
                if (preset == 0)
                    bc.Size = new Vector3(1f, 1f, 1f);
                else if (preset == 1)
                    bc.Size = new Vector3(1f, 2f, 1f);
                else if (preset == 2)
                    bc.Size = new Vector3(0.1f, 3f, 5f);

                InspectorWidgets.EndSection();
            }
        }
    }
}
