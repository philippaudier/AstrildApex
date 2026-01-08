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

            DrawSkyboxSection(env);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawAmbientSection(env);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawCelestialBodySection(env);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawProceduralSkyboxPreview(env);
        }

        private static void DrawSkyboxSection(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("🌌 Skybox", defaultOpen: true))
            {
                uint entityId = env.Entity?.Id ?? 0;

                // Skybox Material Path
                string skyboxPath = env.SkyboxMaterialPath ?? "";
                ImGui.Text("Material Path");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##SkyboxPath", ref skyboxPath, 512))
                {
                    env.SkyboxMaterialPath = skyboxPath;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Path to the procedural skybox material asset");

                ImGui.Spacing();

                // Auto-update toggle
                bool autoUpdate = env.AutoUpdateSkybox;
                if (ImGui.Checkbox("Auto-Update from Time", ref autoUpdate))
                {
                    env.AutoUpdateSkybox = autoUpdate;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Automatically update skybox material based on time of day and season");

                if (autoUpdate && !string.IsNullOrEmpty(skyboxPath))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1.0f, 0.4f, 1.0f), "✓ Skybox will update automatically");

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();

                    // Show currently applied values (read-only)
                    InspectorWidgets.DisabledLabel("Currently Applied Values:");

                    var skyTint = env.SkyboxTint;
                    var skyTintVec3 = new System.Numerics.Vector3(skyTint.X, skyTint.Y, skyTint.Z);
                    ImGui.BeginDisabled();
                    ImGui.ColorEdit3("Sky Tint (Applied)", ref skyTintVec3);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Current sky tint calculated from time of day");

                    float exposure = env.SkyboxExposure;
                    ImGui.BeginDisabled();
                    ImGui.DragFloat("Exposure (Applied)", ref exposure, 0.01f, 0.0f, 8.0f);
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Current exposure calculated from time of day");
                }
                else if (autoUpdate && string.IsNullOrEmpty(skyboxPath))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.6f, 0.0f, 1.0f), "⚠ Set Material Path to enable auto-update");
                }

                ImGui.Spacing();
                InspectorWidgets.EndSection();
            }
        }

        private static void DrawAmbientSection(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("💡 Ambient Lighting", defaultOpen: true))
            {
                uint entityId = env.Entity?.Id ?? 0;

                // Ambient Mode
                int ambientMode = (int)env.AmbientMode;
                string[] ambientModes = { "Skybox", "Color" };
                ImGui.Text("Source");
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo("##AmbientMode", ref ambientMode, ambientModes, ambientModes.Length))
                {
                    env.AmbientMode = (Engine.Components.AmbientMode)ambientMode;
                }

                ImGui.Spacing();

                // Ambient Color
                var ambientColor = env.AmbientColor;
                InspectorWidgets.ColorFieldOTK("Ambient Color", ref ambientColor, entityId, "AmbientColor",
                    tooltip: "Base ambient light color");
                env.AmbientColor = ambientColor;

                float ambientIntensity = env.AmbientIntensity;
                InspectorWidgets.FloatField("Intensity", ref ambientIntensity, entityId, "AmbientIntensity",
                    speed: 0.01f, min: 0f, max: 2f,
                    tooltip: "Ambient light intensity multiplier");
                env.AmbientIntensity = ambientIntensity;

                ImGui.Spacing();
                InspectorWidgets.EndSection();
            }
        }

        private static void DrawCelestialBodySection(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("☀️ Celestial Body (Sun/Moon)", defaultOpen: true))
            {
                uint entityId = env.Entity?.Id ?? 0;

                InspectorWidgets.InfoBox("ONE directional light blends between sun (day) and moon (night). Optimizes performance and avoids shadow conflicts.");

                ImGui.Spacing();

                // Main directional light reference (read-only display)
                InspectorWidgets.DisabledLabel("Main Directional Light:");

                var mainLight = env.MainDirectionalLight;
                string lightName = mainLight != null ? mainLight.Name : "None (Auto-detect)";
                ImGui.BulletText(lightName);

                if (mainLight == null)
                {
                    InspectorWidgets.InfoBox("The system will auto-detect the first DirectionalLight in the scene.");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Sun settings
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.9f, 0.6f, 1.0f), "☀ Sun (Day)");

                var sunColor = env.SunColor;
                InspectorWidgets.ColorFieldOTK("Sun Color", ref sunColor, entityId, "SunColor",
                    tooltip: "Daytime sun color (warm yellow/white)");
                env.SunColor = sunColor;

                float sunIntensity = env.SunIntensity;
                InspectorWidgets.FloatField("Sun Intensity", ref sunIntensity, entityId, "SunIntensity",
                    speed: 0.01f, min: 0f, max: 10f,
                    tooltip: "Daytime sun brightness");
                env.SunIntensity = sunIntensity;

                float sunSize = env.SunSize;
                InspectorWidgets.FloatField("Sun Size", ref sunSize, entityId, "SunSize",
                    speed: 0.01f, min: 0.1f, max: 5f,
                    tooltip: "Visual size of the sun");
                env.SunSize = sunSize;

                bool sunCastShadows = env.SunCastShadows;
                if (ImGui.Checkbox("Sun Cast Shadows", ref sunCastShadows))
                {
                    env.SunCastShadows = sunCastShadows;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Enable shadow casting during daytime");

                ImGui.Spacing();

                // Moon settings
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.8f, 1.0f, 1.0f), "🌙 Moon (Night)");

                var moonColor = env.MoonColor;
                InspectorWidgets.ColorFieldOTK("Moon Color", ref moonColor, entityId, "MoonColor",
                    tooltip: "Nighttime moon color (cool blue/white)");
                env.MoonColor = moonColor;

                float moonIntensity = env.MoonIntensity;
                InspectorWidgets.FloatField("Moon Intensity", ref moonIntensity, entityId, "MoonIntensity",
                    speed: 0.01f, min: 0f, max: 10f,
                    tooltip: "Nighttime moon brightness");
                env.MoonIntensity = moonIntensity;

                float moonSize = env.MoonSize;
                InspectorWidgets.FloatField("Moon Size", ref moonSize, entityId, "MoonSize",
                    speed: 0.01f, min: 0.1f, max: 5f,
                    tooltip: "Visual size of the moon");
                env.MoonSize = moonSize;

                bool moonCastShadows = env.MoonCastShadows;
                if (ImGui.Checkbox("Moon Cast Shadows", ref moonCastShadows))
                {
                    env.MoonCastShadows = moonCastShadows;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Enable shadow casting during nighttime");

                ImGui.Spacing();

                // Golden hour
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.7f, 0.3f, 1.0f), "🌅 Golden Hour (Sunrise/Sunset)");

                var sunriseColor = env.SunriseColor;
                InspectorWidgets.ColorFieldOTK("Sunrise Color", ref sunriseColor, entityId, "SunriseColor",
                    tooltip: "Warm color tint during sunrise");
                env.SunriseColor = sunriseColor;

                var sunsetColor = env.SunsetColor;
                InspectorWidgets.ColorFieldOTK("Sunset Color", ref sunsetColor, entityId, "SunsetColor",
                    tooltip: "Warm color tint during sunset");
                env.SunsetColor = sunsetColor;

                float goldenHourIntensity = env.GoldenHourIntensity;
                InspectorWidgets.FloatField("Golden Hour Intensity", ref goldenHourIntensity, entityId, "GoldenHourIntensity",
                    speed: 0.01f, min: 0f, max: 2f,
                    tooltip: "How much golden hour affects the light color");
                env.GoldenHourIntensity = goldenHourIntensity;

                ImGui.Spacing();

                // Transition settings
                ImGui.TextColored(new System.Numerics.Vector4(0.8f, 0.8f, 1.0f, 1.0f), "⏱ Transition Settings");

                float transitionDuration = env.TransitionDuration;
                InspectorWidgets.FloatField("Transition Duration (hours)", ref transitionDuration, entityId, "TransitionDuration",
                    speed: 0.1f, min: 0.1f, max: 3f,
                    tooltip: "How long the sun/moon transition takes");
                env.TransitionDuration = transitionDuration;

                float transitionCurve = env.TransitionCurve;
                InspectorWidgets.FloatField("Transition Curve", ref transitionCurve, entityId, "TransitionCurve",
                    speed: 0.1f, min: 1f, max: 3f,
                    tooltip: "Smoothness (1=linear, 2=smooth, 3=very smooth)");
                env.TransitionCurve = transitionCurve;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Advanced Celestial Settings (collapsible sub-section)
                bool advancedOpen = ThemedImGui.CollapsingHeader("⚙️ Advanced Settings", ImGuiTreeNodeFlags.None);
                if (advancedOpen)
                {
                    ImGui.Indent(InspectorLayout.IndentWidth);
                    ImGui.Spacing();

                    // Temperature settings
                    InspectorWidgets.DisabledLabel("Color Temperature:");

                    float sunTemp = env.SunTemperature;
                    InspectorWidgets.FloatField("Sun Temperature (K)", ref sunTemp, entityId, "SunTemperature",
                        speed: 100f, min: 2000f, max: 10000f,
                        tooltip: "Sun color temperature in Kelvin (5500K = daylight, higher = bluer, lower = redder)",
                        helpText: "2000K-3000K: warm/orange, 5500K: neutral daylight, 6500K-10000K: cool/blue");
                    env.SunTemperature = sunTemp;

                    float moonTemp = env.MoonTemperature;
                    InspectorWidgets.FloatField("Moon Temperature (K)", ref moonTemp, entityId, "MoonTemperature",
                        speed: 100f, min: 2000f, max: 10000f,
                        tooltip: "Moon color temperature in Kelvin (4000K = cool moonlight)",
                        helpText: "Lower than sun for cooler, blue-ish moonlight");
                    env.MoonTemperature = moonTemp;

                    ImGui.Spacing();

                    // Orbital mechanics
                    InspectorWidgets.DisabledLabel("Orbital Mechanics:");

                    float sunOrbitTilt = env.SunOrbitTilt;
                    InspectorWidgets.FloatField("Sun Orbit Tilt (degrees)", ref sunOrbitTilt, entityId, "SunOrbitTilt",
                        speed: 0.5f, min: 0f, max: 90f,
                        tooltip: "Earth's axial tilt (23.5° = realistic, affects seasonal day length)",
                        helpText: "23.5° = Earth's real tilt, 0° = no seasons, 90° = extreme seasons");
                    env.SunOrbitTilt = sunOrbitTilt;

                    var sunriseDir = env.SunriseDirection;
                    InspectorWidgets.Vector3FieldOTK("Sunrise Direction", ref sunriseDir, 0.1f, entityId, "SunriseDirection",
                        tooltip: "Direction vector for sunrise (default: East = +X)");
                    env.SunriseDirection = sunriseDir;

                    ImGui.Spacing();
                    InspectorWidgets.InfoBox("Temperature values affect light color. Orbital parameters affect celestial trajectory and seasonal variation.");

                    ImGui.Spacing();
                    ImGui.Unindent(InspectorLayout.IndentWidth);
                }

                ImGui.Spacing();

                // Display current state
                float sunMoonBlend = env.GetSunMoonBlend();
                string blendLabel = sunMoonBlend > 0.7f ? "Sun" : sunMoonBlend < 0.3f ? "Moon" : "Transition";
                ImGui.ProgressBar(sunMoonBlend, new System.Numerics.Vector2(-1, 0), $"{blendLabel}: {sunMoonBlend:P0}");

                ImGui.Spacing();

                // Season info
                InspectorWidgets.DisabledLabel("Current Season Info:");
                ImGui.BulletText($"Season: {env.GetSeasonName()}");
                ImGui.BulletText($"Day of Year: {env.DayOfYear}/365");
                ImGui.BulletText($"Solar Declination: {env.GetSolarDeclination():F1}°");
                ImGui.BulletText($"Time of Day: {env.TimeOfDay:F1}h");
                ImGui.BulletText($"Sunrise: {env.GetSunriseHour():00}:{(int)((env.GetSunriseHour() % 1) * 60):00}");
                ImGui.BulletText($"Sunset: {env.GetSunsetHour():00}:{(int)((env.GetSunsetHour() % 1) * 60):00}");
                
                float dayLength = env.GetSunsetHour() - env.GetSunriseHour();
                ImGui.BulletText($"Day Length: {(int)dayLength}h {(int)((dayLength % 1) * 60)}min");

                ImGui.Spacing();

                // Latitude control
                InspectorWidgets.DisabledLabel("Geographic Position:");
                float latitude = env.Latitude;
                InspectorWidgets.FloatField("Latitude (degrees)", ref latitude, entityId, "Latitude",
                    speed: 1.0f, min: -90f, max: 90f,
                    tooltip: "Geographic latitude (-90° South Pole, 0° Equator, +90° North Pole)");
                env.Latitude = latitude;

                ImGui.Spacing();
                InspectorWidgets.EndSection();
            }
        }

        private static void DrawProceduralSkyboxPreview(EnvironmentSettings env)
        {
            if (InspectorWidgets.Section("🌈 Procedural Skybox Preview", defaultOpen: false))
            {
                uint entityId = env.Entity?.Id ?? 0;

                InspectorWidgets.InfoBox("These values are calculated in real-time from TimeOfDay, Season, and Celestial settings. They show what will be applied to the procedural skybox material.");

                ImGui.Spacing();

                // Get current calculated parameters
                var skyboxParams = env.GetProceduralSkyboxParameters();
                float dayNightBlend = env.GetDayNightBlend(env.TimeOfDay);
                float goldenHourBlend = env.GetGoldenHourBlend(env.TimeOfDay);

                // Display blend factors
                InspectorWidgets.DisabledLabel("Current Blends:");
                ImGui.Text($"Day/Night: {dayNightBlend:P1}");
                ImGui.ProgressBar(dayNightBlend, new System.Numerics.Vector2(-1, 0), $"");
                ImGui.Text($"Golden Hour: {goldenHourBlend:P1}");
                ImGui.ProgressBar(goldenHourBlend, new System.Numerics.Vector2(-1, 0), $"");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Sky parameters (read-only display)
                InspectorWidgets.DisabledLabel("Sky Parameters:");

                var skyTintVec3 = new System.Numerics.Vector3(skyboxParams.SkyTint.X, skyboxParams.SkyTint.Y, skyboxParams.SkyTint.Z);
                ImGui.BeginDisabled();
                ImGui.ColorEdit3("Sky Tint", ref skyTintVec3);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Calculated sky gradient color (blended from day/night/golden hour)");

                var groundColorVec3 = new System.Numerics.Vector3(skyboxParams.GroundColor.X, skyboxParams.GroundColor.Y, skyboxParams.GroundColor.Z);
                ImGui.BeginDisabled();
                ImGui.ColorEdit3("Ground Color", ref groundColorVec3);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Calculated ground/horizon color");

                float exposure = skyboxParams.Exposure;
                ImGui.BeginDisabled();
                ImGui.DragFloat("Exposure", ref exposure, 0.01f, 0.0f, 8.0f, "%.2f");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Calculated skybox exposure (brightness)");

                float atmThickness = skyboxParams.AtmosphereThickness;
                ImGui.BeginDisabled();
                ImGui.DragFloat("Atmosphere Thickness", ref atmThickness, 0.01f, 0.0f, 5.0f, "%.2f");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Calculated atmosphere scattering thickness");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Celestial body parameters (read-only display)
                InspectorWidgets.DisabledLabel("Celestial Body (Sun/Moon):");

                var sunTintVec3 = new System.Numerics.Vector3(skyboxParams.SunTint.X, skyboxParams.SunTint.Y, skyboxParams.SunTint.Z);
                ImGui.BeginDisabled();
                ImGui.ColorEdit3("Sun/Moon Tint", ref sunTintVec3);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Blended sun/moon color for skybox rendering");

                float sunSize = skyboxParams.SunSize;
                ImGui.BeginDisabled();
                ImGui.DragFloat("Sun/Moon Size", ref sunSize, 0.001f, 0.0f, 1.0f, "%.3f");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Calculated celestial body disk size (larger during golden hour)");

                float sunSizeConvergence = skyboxParams.SunSizeConvergence;
                ImGui.BeginDisabled();
                ImGui.DragFloat("Sun Glow", ref sunSizeConvergence, 0.1f, 1.0f, 20.0f, "%.1f");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Calculated sun glow/bloom effect (stronger during golden hour)");

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Applied status
                if (env.AutoUpdateSkybox && !string.IsNullOrEmpty(env.SkyboxMaterialPath))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1.0f, 0.4f, 1.0f), "✓ These values are being applied to the skybox material");
                }
                else if (env.AutoUpdateSkybox && string.IsNullOrEmpty(env.SkyboxMaterialPath))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.6f, 0.0f, 1.0f), "⚠ Set Skybox Material Path to apply these values");
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1.0f), "○ Auto-Update is disabled - values are calculated but not applied");
                }

                ImGui.Spacing();
                InspectorWidgets.EndSection();
            }
        }
    }
}
