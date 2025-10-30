using System;
using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    /// <summary>
    /// Professional Unity-style Light Component inspector with presets and validation
    /// </summary>
    public static class LightInspector
    {
        public static void Draw(LightComponent light)
        {
            if (light?.Entity == null) return;
            uint entityId = light.Entity.Id;

            // === LIGHT TYPE & COLOR ===
            if (InspectorWidgets.Section("Light", defaultOpen: true))
            {
                var lightType = light.Type;
                InspectorWidgets.EnumField("Type", ref lightType, entityId, "Type",
                    tooltip: "Directional: sun-like parallel rays. Point: omni-directional. Spot: cone-shaped");
                light.Type = lightType;

                var color = light.Color;
                InspectorWidgets.ColorFieldOTK("Color", ref color, entityId, "Color",
                    tooltip: "Light color (RGB)");
                light.Color = color;

                float intensity = light.Intensity;
                InspectorWidgets.FloatField("Intensity", ref intensity, entityId, "Intensity",
                    speed: 0.01f, min: 0f, max: 100f,
                    tooltip: "Light brightness multiplier",
                    validate: (v) => v >= 0 ? null : "Intensity cannot be negative",
                    helpText: "Typical: 1-2 for indoor, 0.5-1 for outdoor sun, 3-10 for bright spots");
                light.Intensity = intensity;

                // Light Presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Sun", "Bright white directional"),
                    ("Soft", "Warm dim lighting"),
                    ("Studio", "Neutral bright"),
                    ("Fire", "Orange flickering"));
                
                if (preset == 0) // Sun
                {
                    light.Color = new Vector3(1.0f, 0.98f, 0.95f);
                    light.Intensity = 1.0f;
                    light.Type = LightType.Directional;
                }
                else if (preset == 1) // Soft
                {
                    light.Color = new Vector3(1.0f, 0.95f, 0.85f);
                    light.Intensity = 0.6f;
                }
                else if (preset == 2) // Studio
                {
                    light.Color = new Vector3(1.0f, 1.0f, 1.0f);
                    light.Intensity = 1.5f;
                }
                else if (preset == 3) // Fire
                {
                    light.Color = new Vector3(1.0f, 0.6f, 0.2f);
                    light.Intensity = 2.0f;
                    light.Type = LightType.Point;
                }

                InspectorWidgets.EndSection();
            }

            // === RANGE (Point & Spot only) ===
            if (light.Type == LightType.Point || light.Type == LightType.Spot)
            {
                if (InspectorWidgets.Section("Range", defaultOpen: true,
                    tooltip: "How far the light reaches"))
                {
                    float range = light.Range;
                    InspectorWidgets.FloatField("Range", ref range, entityId, "Range",
                        speed: 0.1f, min: 0.1f, max: 1000f,
                        tooltip: "Maximum distance the light affects (world units)",
                        validate: (v) => v > 0 ? null : "Range must be greater than 0",
                        helpText: "Light intensity falls off to zero at this distance");
                    light.Range = range;

                    if (light.Range == 0)
                        InspectorWidgets.WarningBox("Range is 0! Light will not be visible.");

                    InspectorWidgets.EndSection();
                }
            }

            // === SPOT ANGLE (Spot only) ===
            if (light.Type == LightType.Spot)
            {
                if (InspectorWidgets.Section("Spot", defaultOpen: true,
                    tooltip: "Spotlight cone shape"))
                {
                    float spotAngle = light.SpotAngle;
                    InspectorWidgets.SliderAngle("Spot Angle", ref spotAngle, 1f, 179f,
                        entityId, "SpotAngle",
                        tooltip: "Cone angle of the spotlight",
                        helpText: "Narrow (15-30°) for focused beam, wide (60-90°) for broad coverage");
                    light.SpotAngle = spotAngle;

                    InspectorWidgets.EndSection();
                }
            }

            // === SHADOWS ===
            if (InspectorWidgets.Section("Shadows", defaultOpen: true,
                tooltip: "Shadow casting settings"))
            {
                bool castShadows = light.CastShadows;
                InspectorWidgets.Checkbox("Cast Shadows", ref castShadows, entityId, "CastShadows",
                    tooltip: "Enable shadow casting for this light",
                    helpText: "Shadows are expensive! Only use on main lights");
                light.CastShadows = castShadows;

                if (!light.CastShadows)
                    InspectorWidgets.InfoBox("Shadows disabled. This light will not cast shadows.");

                InspectorWidgets.EndSection();
            }
        }
    }
}