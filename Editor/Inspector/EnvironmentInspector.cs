using System;
using System.Linq;
using ImGuiNET;
using Engine.Components;
using OpenTK.Mathematics;
using Editor.UI;
using Editor.Themes;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector UI for EnvironmentSettings - provides controls for skybox, ambient lighting,
    /// and the new unified celestial body system (sun/moon blend).
    /// </summary>
    public static class EnvironmentInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static void Draw(EnvironmentSettings env)
        {
            if (env == null) return;

            DrawAmbientSection(env);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawCelestialBodySection(env);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawCelestialParametersSection(env);
        }

        // Skybox is managed separately - removed from this inspector

        private static void DrawAmbientSection(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("Ambient Light", defaultOpen: true))
            {
                uint entityId = env.Entity?.Id ?? 0;

                var ambientColor = env.AmbientColor;
                InspectorWidgets.ColorFieldOTK("Color", ref ambientColor, entityId, "AmbientColor",
                    tooltip: "Base ambient light color (affects all objects)");
                env.AmbientColor = ambientColor;

                float ambientIntensity = env.AmbientIntensity;
                InspectorWidgets.FloatField("Intensity", ref ambientIntensity, entityId, "AmbientIntensity",
                    speed: 0.01f, min: 0f, max: 10f,
                    tooltip: "Ambient light brightness multiplier");
                env.AmbientIntensity = ambientIntensity;

                ImGui.Spacing();
                InspectorWidgets.InfoBox("Ambient light provides base illumination to all objects, preventing pure black shadows.");
            }
        }

        private static void DrawCelestialBodySection(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("Celestial Body (Sun/Moon)", defaultOpen: true))
            {
                uint entityId = env.Entity?.Id ?? 0;

                InspectorWidgets.InfoBox("New unified system: ONE directional light smoothly blends between sun (day) and moon (night). This optimizes performance and avoids shadow conflicts.");

                ImGui.Spacing();

                // Main directional light reference (read-only display)
                InspectorWidgets.DisabledLabel("Main Directional Light:");

                var mainLight = env.MainDirectionalLight;
                string lightName = mainLight != null ? mainLight.Name : "None (Auto-detect)";
                ImGui.BulletText(lightName);

                if (mainLight == null)
                {
                    InspectorWidgets.InfoBox("The system will automatically find the first DirectionalLight in the scene. You can also set this via code or drag-drop a DirectionalLight entity.");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Sun colors
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.9f, 0.6f, 1.0f), "Sun (Day)");

                var sunColor = env.SunColor;
                InspectorWidgets.ColorFieldOTK("Sun Color", ref sunColor, entityId, "SunColor",
                    tooltip: "Daytime sun color (warm yellow/white)");
                env.SunColor = sunColor;

                float sunIntensity = env.SunIntensity;
                InspectorWidgets.FloatField("Sun Intensity", ref sunIntensity, entityId, "SunIntensity",
                    speed: 0.01f, min: 0f, max: 10f,
                    tooltip: "Daytime sun brightness");
                env.SunIntensity = sunIntensity;

                ImGui.Spacing();

                // Moon colors
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.8f, 1.0f, 1.0f), "Moon (Night)");

                var moonColor = env.MoonColor;
                InspectorWidgets.ColorFieldOTK("Moon Color", ref moonColor, entityId, "MoonColor",
                    tooltip: "Nighttime moon color (cool blue/white)");
                env.MoonColor = moonColor;

                float moonIntensity = env.MoonIntensity;
                InspectorWidgets.FloatField("Moon Intensity", ref moonIntensity, entityId, "MoonIntensity",
                    speed: 0.01f, min: 0f, max: 10f,
                    tooltip: "Nighttime moon brightness");
                env.MoonIntensity = moonIntensity;

                ImGui.Spacing();

                // Golden hour (Sunrise/Sunset colors)
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.7f, 0.3f, 1.0f), "Golden Hour (Sunrise/Sunset)");

                var sunriseColor = env.SunriseColor;
                InspectorWidgets.ColorFieldOTK("Sunrise Color", ref sunriseColor, entityId, "SunriseColor",
                    tooltip: "Warm color tint during sunrise");
                env.SunriseColor = sunriseColor;

                var sunsetColor = env.SunsetColor;
                InspectorWidgets.ColorFieldOTK("Sunset Color", ref sunsetColor, entityId, "SunsetColor",
                    tooltip: "Warm color tint during sunset");
                env.SunsetColor = sunsetColor;

                float goldenHourIntensity = env.GoldenHourIntensity;
                InspectorWidgets.FloatField("Golden Hour Boost", ref goldenHourIntensity, entityId, "GoldenHourIntensity",
                    speed: 0.01f, min: 0f, max: 2f,
                    tooltip: "Additional brightness during sunrise/sunset");
                env.GoldenHourIntensity = goldenHourIntensity;

                ImGui.Spacing();

                // Display current blend
                float sunMoonBlend = env.GetSunMoonBlend();
                string blendLabel = sunMoonBlend > 0.7f ? "Sun" : sunMoonBlend < 0.3f ? "Moon" : "Transition";
                ImGui.ProgressBar(sunMoonBlend, new System.Numerics.Vector2(-1, 0), $"{blendLabel}: {sunMoonBlend:P0}");
            }
        }

        private static void DrawCelestialParametersSection(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("Celestial Mechanics", defaultOpen: false))
            {
                uint entityId = env.Entity?.Id ?? 0;

                InspectorWidgets.InfoBox("Advanced parameters for sun/moon orbit simulation. These affect the position and rotation of the celestial body throughout the day and year.");

                ImGui.Spacing();

                // Sunrise direction
                var sunriseDir = env.SunriseDirection;
                InspectorWidgets.Vector3FieldOTK("Sunrise Direction", ref sunriseDir, 0.01f, entityId, "SunriseDirection",
                    tooltip: "Direction where the sun rises (typically East, +X or +Z depending on coordinate system)");
                env.SunriseDirection = sunriseDir;

                // Sun orbit tilt (seasons)
                float sunOrbitTilt = env.SunOrbitTilt;
                InspectorWidgets.FloatField("Orbit Tilt (degrees)", ref sunOrbitTilt, entityId, "SunOrbitTilt",
                    speed: 0.5f, min: -90f, max: 90f,
                    tooltip: "Axial tilt of sun orbit (simulates seasons). Earth = 23.5°");
                env.SunOrbitTilt = sunOrbitTilt;

                ImGui.Spacing();

                // Quick presets
                InspectorWidgets.DisabledLabel("Presets:");
                int preset = InspectorWidgets.PresetButtonRow(
                    ("Earth", "Earth-like (23.5° tilt)"),
                    ("Flat", "No tilt (perpetual equinox)"),
                    ("Extreme", "45° tilt (dramatic seasons)"));

                if (preset == 0) // Earth
                {
                    env.SunOrbitTilt = 23.5f;
                }
                else if (preset == 1) // Flat
                {
                    env.SunOrbitTilt = 0.0f;
                }
                else if (preset == 2) // Extreme
                {
                    env.SunOrbitTilt = 45.0f;
                }
            }
        }
    }
}
