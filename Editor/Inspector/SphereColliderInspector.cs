using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Sphere Collider inspector
    /// </summary>
    public static class SphereColliderInspector
    {
        public static void Draw(SphereCollider sc)
        {
            if (sc?.Entity == null) return;
            uint entityId = sc.Entity.Id;

            // === COLLIDER SETTINGS ===
            if (InspectorWidgets.Section("Sphere Collider", defaultOpen: true))
            {
                bool enabled = sc.Enabled;
                InspectorWidgets.Checkbox("Enabled", ref enabled, entityId, "Enabled",
                    tooltip: "Enable or disable this collider");
                sc.Enabled = enabled;

                bool isTrigger = sc.IsTrigger;
                InspectorWidgets.Checkbox("Is Trigger", ref isTrigger, entityId, "IsTrigger",
                    tooltip: "Trigger colliders detect overlaps but don't create physics contacts",
                    helpText: "Use for zones, pickups, sensors. Non-trigger = solid collision");
                sc.IsTrigger = isTrigger;

                int layer = sc.Layer;
                InspectorWidgets.IntField("Layer", ref layer, min: 0, max: 31, entityId: entityId, fieldPath: "Layer",
                    tooltip: "Collision layer (0-31)",
                    helpText: "Objects on the same layer can be filtered together");
                sc.Layer = layer;

                InspectorWidgets.EndSection();
            }

            // === SHAPE ===
            if (InspectorWidgets.Section("Shape", defaultOpen: true,
                tooltip: "Sphere collider dimensions"))
            {
                var center = sc.Center;
                InspectorWidgets.Vector3FieldOTK("Center", ref center, 0.01f, entityId, "Center",
                    tooltip: "Center point of the sphere in local space",
                    helpText: "Offset from the GameObject's pivot");
                sc.Center = center;

                float radius = sc.Radius;
                InspectorWidgets.FloatField("Radius", ref radius, entityId, "Radius",
                    speed: 0.01f, min: 0.001f, max: 1000f,
                    tooltip: "Radius of the sphere",
                    validate: (r) => r > 0 ? null : "Radius must be positive",
                    helpText: "Distance from center to surface. Typical values: 0.5 for characters, 0.25 for small objects");
                sc.Radius = radius;

                if (sc.Radius <= 0)
                    InspectorWidgets.WarningBox("Collider radius is zero or negative!");

                // Radius presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Character", "0.5 radius"),
                    ("Small", "0.25 radius"),
                    ("Large", "2.0 radius"));
                
                if (preset == 0)
                    sc.Radius = 0.5f;
                else if (preset == 1)
                    sc.Radius = 0.25f;
                else if (preset == 2)
                    sc.Radius = 2.0f;

                InspectorWidgets.EndSection();
            }
        }
    }
}
