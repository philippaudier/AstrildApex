using System;
using System.Numerics;
using ImGuiNET;
using Editor.UI;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector UI for WeatherComponent - provides intuitive controls for weather parameters,
    /// presets, and smooth transitions.
    /// </summary>
    public static class WeatherInspector
    {
        private static int _selectedPresetIndex = -1;

        /// <summary>
        /// Helper to update WeatherManager after any weather parameter change for immediate live preview
        /// </summary>
        private static void UpdateWeatherManager(Engine.Components.WeatherComponent weather)
        {
            try
            {
                Engine.Systems.WeatherManager.UpdateFromComponent(weather);
            }
            catch { }
        }

        private static int _debugFrameCounter = 0;

        public static void Draw(Engine.Components.WeatherComponent weather)
        {
            if (weather == null) return;

            // DEBUG: Print accumulation value every 60 frames in Play Mode
            if (PlayMode.IsPlaying && _debugFrameCounter++ % 60 == 0)
            {
                System.Console.WriteLine($"[WeatherInspector] Displaying SnowAccumulation={weather.SnowAccumulation:F3}, SnowIntensity={weather.SnowIntensity:F3}");
            }

            DrawPresetSection(weather);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawWindSection(weather);
            ImGui.Spacing();

            DrawRainSection(weather);
            ImGui.Spacing();

            DrawSnowSection(weather);
            ImGui.Spacing();

            DrawFogSection(weather);
            ImGui.Spacing();

            DrawCloudSection(weather);
            ImGui.Spacing();

            /*
            DrawMaterialsSection(weather);
            ImGui.Spacing();

            DrawParticleSystemsSection(weather);
            ImGui.Spacing();
            */

            DrawTransitionSettings(weather);
        }

        private static void DrawPresetSection(Engine.Components.WeatherComponent weather)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Weather Presets");
            ImGui.Spacing();

            // Get all presets
            var presets = Engine.Components.WeatherPreset.GetAllPresets();
            
            // Preset selection dropdown
            string currentPresetName = _selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Length 
                ? presets[_selectedPresetIndex].Name 
                : "Select Preset...";

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.6f);
            if (ImGui.BeginCombo("##WeatherPreset", currentPresetName))
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    bool isSelected = _selectedPresetIndex == i;
                    if (ImGui.Selectable(presets[i].Name, isSelected))
                    {
                        _selectedPresetIndex = i;
                    }
                    
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();

            // Apply buttons
            if (ImGui.Button("Apply Instant"))
            {
                if (_selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Length)
                {
                    weather.ApplyPreset(presets[_selectedPresetIndex]);
                }
            }

            ImGui.SameLine();

            if (ImGui.Button("Transition"))
            {
                if (_selectedPresetIndex >= 0 && _selectedPresetIndex < presets.Length)
                {
                    weather.TransitionToPreset(presets[_selectedPresetIndex]);
                }
            }

            // Show transition progress if active
            if (weather.TargetState != null)
            {
                ImGui.Spacing();
                float progress = weather.TargetState.ElapsedTime / weather.TargetState.TransitionDuration;
                ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), $"Transitioning... {progress * 100:F0}%%");
                ImGui.ProgressBar(progress, new Vector2(-1, 0));
            }
        }

        private static void DrawWindSection(Engine.Components.WeatherComponent weather)
        {
            // Section header
            if (ThemedImGui.CollapsingHeader("🌬️ Wind", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Wind Strength (main control)
                float windStrength = weather.WindStrength;
                ImGui.SetNextItemWidth(-120);
                if (ImGui.SliderFloat("Strength", ref windStrength, 0.0f, 1.0f, "%.2f"))
                {
                    weather.WindStrength = Math.Clamp(windStrength, 0.0f, 1.0f);
                    UpdateWeatherManager(weather);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Overall wind intensity");

                // Wind Direction (2D vector visualizer)
                var windDir = weather.GetWindDirection();
                float dirX = windDir.X;
                float dirZ = windDir.Y;

                ImGui.Text("Direction");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(-120);

                // Simple angle control
                float angle = MathF.Atan2(dirZ, dirX) * (180.0f / MathF.PI);
                if (ImGui.SliderFloat("##WindAngle", ref angle, -180.0f, 180.0f, "%.0f°"))
                {
                    float rad = angle * (MathF.PI / 180.0f);
                    weather.WindDirectionX = MathF.Cos(rad);
                    weather.WindDirectionZ = MathF.Sin(rad);
                    UpdateWeatherManager(weather);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Wind direction in degrees (0° = East, 90° = North)");

                // Advanced wind controls (collapsed by default)
                if (ImGui.TreeNode("Advanced Wind"))
                {
                    float windSpeed = weather.WindSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Animation Speed", ref windSpeed, 0.1f, 3.0f, "%.2f"))
                    {
                        weather.WindSpeed = Math.Clamp(windSpeed, 0.1f, 3.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How fast the wind animation plays");

                    float gustiness = weather.WindGustiness;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Gustiness", ref gustiness, 0.0f, 1.0f, "%.2f"))
                    {
                        weather.WindGustiness = Math.Clamp(gustiness, 0.0f, 1.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("0 = smooth wind, 1 = gusty turbulent wind");

                    ImGui.Spacing();
                    ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.9f, 1.0f, 1.0f), "Vegetation Detail:");
                    ImGui.Spacing();

                    // Trunk parameters
                    float trunkStiffness = weather.TrunkStiffness;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Trunk Stiffness", ref trunkStiffness, 0.0f, 1.0f, "%.2f"))
                    {
                        weather.TrunkStiffness = Math.Clamp(trunkStiffness, 0.0f, 1.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Trunk rigidity: 0 = flexible, 1 = very rigid");

                    float trunkBendAmount = weather.TrunkBendAmount;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Trunk Bend Amount", ref trunkBendAmount, 0.0f, 2.0f, "%.2f"))
                    {
                        weather.TrunkBendAmount = Math.Clamp(trunkBendAmount, 0.0f, 2.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How much the trunk bends at the top");

                    // Branch parameters
                    float branchAmplitude = weather.BranchAmplitude;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Branch Amplitude", ref branchAmplitude, 0.0f, 5.0f, "%.2f"))
                    {
                        weather.BranchAmplitude = Math.Clamp(branchAmplitude, 0.0f, 5.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Branch sway intensity multiplier");

                    float branchSpeed = weather.BranchSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Branch Speed", ref branchSpeed, 0.5f, 15.0f, "%.2f"))
                    {
                        weather.BranchSpeed = Math.Clamp(branchSpeed, 0.5f, 15.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Branch oscillation frequency");

                    float branchTurbulence = weather.BranchTurbulence;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Branch Turbulence", ref branchTurbulence, 0.0f, 2.0f, "%.2f"))
                    {
                        weather.BranchTurbulence = Math.Clamp(branchTurbulence, 0.0f, 2.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Branch detail and chaotic motion");

                    // Leaf parameters
                    float leafFlutter = weather.LeafFlutter;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Leaf Flutter", ref leafFlutter, 0.0f, 2.0f, "%.2f"))
                    {
                        weather.LeafFlutter = Math.Clamp(leafFlutter, 0.0f, 2.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Individual leaf fluttering intensity");

                    float leafFlutterSpeed = weather.LeafFlutterSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Leaf Flutter Speed", ref leafFlutterSpeed, 1.0f, 20.0f, "%.2f"))
                    {
                        weather.LeafFlutterSpeed = Math.Clamp(leafFlutterSpeed, 1.0f, 20.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Leaf flutter frequency");

                    ImGui.TreePop();
                }
            }
        }

        private static void DrawRainSection(Engine.Components.WeatherComponent weather)
        {
            if (ThemedImGui.CollapsingHeader("☔ Rain", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Rain Intensity
                float rainIntensity = weather.RainIntensity;
                ImGui.SetNextItemWidth(-120);
                if (ImGui.SliderFloat("Intensity##RainIntensity", ref rainIntensity, 0.0f, 1.0f, "%.2f"))
                {
                    weather.RainIntensity = Math.Clamp(rainIntensity, 0.0f, 1.0f);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Rain intensity (0 = no rain, 1 = heavy rain)");

                // Current Wetness (read-only, auto-updated by system)
                float wetness = weather.Wetness;
                ImGui.SetNextItemWidth(-120);
                ImGui.SliderFloat("Wetness", ref wetness, 0.0f, 1.0f, "%.2f", ImGuiSliderFlags.NoInput);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Surface wetness (auto-updated based on rain)");

                // Advanced rain controls
                if (ImGui.TreeNode("Advanced Rain"))
                {
                    float wetnessSpeed = weather.RainWetnessSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Wetness Speed", ref wetnessSpeed, 0.01f, 1.0f, "%.2f"))
                    {
                        weather.RainWetnessSpeed = Math.Clamp(wetnessSpeed, 0.01f, 1.0f);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How fast surfaces get wet");

                    float dryingSpeed = weather.RainDryingSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Drying Speed", ref dryingSpeed, 0.01f, 0.5f, "%.2f"))
                    {
                        weather.RainDryingSpeed = Math.Clamp(dryingSpeed, 0.01f, 0.5f);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How fast surfaces dry when rain stops");

                    ImGui.TreePop();
                }
            }
        }

        private static void DrawSnowSection(Engine.Components.WeatherComponent weather)
        {
            if (ThemedImGui.CollapsingHeader("❄️ Snow", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Snow Material Assignment
                ImGui.TextColored(new Vector4(0.8f, 0.9f, 1.0f, 1.0f), "Snow Material");
                var snowMaterial = EditorWidgets.AssetField(
                    "Snow Material",
                    weather.SnowMapMaterial,
                    "Material",
                    "Material with snow textures (albedo, normal, roughness) for realistic snow appearance",
                    showPreview: false,
                    dragDropHeight: 50f
                );
                if (snowMaterial != weather.SnowMapMaterial)
                {
                    weather.SnowMapMaterial = snowMaterial;
                    UpdateWeatherManager(weather); // Update WeatherManager immediately for live preview
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                // Snow Intensity
                float snowIntensity = weather.SnowIntensity;
                ImGui.SetNextItemWidth(-120);
                if (ImGui.SliderFloat("Intensity##SnowIntensity", ref snowIntensity, 0.0f, 1.0f, "%.2f"))
                {
                    weather.SnowIntensity = Math.Clamp(snowIntensity, 0.0f, 1.0f);
                    UpdateWeatherManager(weather);

                    // DEBUG LOG
                    System.Console.WriteLine($"[WeatherInspector] SnowIntensity changed to {weather.SnowIntensity:F3} (IsPlaying={PlayMode.IsPlaying})");
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Snowfall intensity (0 = no snow, 1 = heavy snowfall)");

                // Snow Accumulation (can exceed 1.0 for thick snow layers)
                // In Play Mode, this is read-only (auto-updated by WeatherSystem)
                float snowAccumulation = weather.SnowAccumulation;
                ImGui.SetNextItemWidth(-120);

                if (PlayMode.IsPlaying)
                {
                    // Read-only in Play Mode (auto-updated by system)
                    ImGui.BeginDisabled();
                    ImGui.SliderFloat("Accumulation", ref snowAccumulation, 0.0f, 3.0f, "%.2f");
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Snow coverage (auto-updated in Play Mode based on intensity - read only)");
                }
                else
                {
                    // Editable in Edit Mode
                    if (ImGui.SliderFloat("Accumulation", ref snowAccumulation, 0.0f, 3.0f, "%.2f"))
                    {
                        weather.SnowAccumulation = Math.Max(0.0f, snowAccumulation); // No upper clamp
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Snow coverage on surfaces (manually set or auto-updated based on intensity)");
                }

                // Advanced snow controls
                if (ImGui.TreeNode("Advanced Snow"))
                {
                    // Accumulation & Melt Speed
                    ImGui.TextColored(new Vector4(0.7f, 0.9f, 1.0f, 1.0f), "Temporal Dynamics:");
                    ImGui.Spacing();

                    float accumSpeed = weather.SnowAccumulationSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Accumulation Speed", ref accumSpeed, 0.01f, 0.5f, "%.3f"))
                    {
                        weather.SnowAccumulationSpeed = Math.Clamp(accumSpeed, 0.01f, 0.5f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How fast snow accumulates when SnowIntensity > 0");

                    float meltSpeed = weather.SnowMeltSpeed;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Melt Speed", ref meltSpeed, 0.01f, 0.3f, "%.3f"))
                    {
                        weather.SnowMeltSpeed = Math.Clamp(meltSpeed, 0.01f, 0.3f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How fast snow melts when SnowIntensity = 0");

                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(0.7f, 0.9f, 1.0f, 1.0f), "Surface Placement:");
                    ImGui.Spacing();

                    // Slope Angle Control
                    float slopeMin = weather.SnowSlopeMin;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Min Slope Angle", ref slopeMin, 0.0f, 90.0f, "%.1f°"))
                    {
                        weather.SnowSlopeMin = Math.Clamp(slopeMin, 0.0f, 90.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Minimum surface angle (degrees) where snow accumulates (0° = flat ground)");

                    float slopeMax = weather.SnowSlopeMax;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Max Slope Angle", ref slopeMax, 0.0f, 90.0f, "%.1f°"))
                    {
                        weather.SnowSlopeMax = Math.Clamp(slopeMax, 0.0f, 90.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Maximum surface angle (degrees) where snow accumulates (45° = typical, 90° = vertical walls)");

                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(0.7f, 0.9f, 1.0f, 1.0f), "Visual Effects:");
                    ImGui.Spacing();

                    // Sparkle Effect
                    float sparkle = weather.SnowSparkle;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Sparkle", ref sparkle, 0.0f, 1.0f, "%.2f"))
                    {
                        weather.SnowSparkle = Math.Clamp(sparkle, 0.0f, 1.0f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Snow sparkle/glitter effect intensity (simulates light reflecting off ice crystals)");

                    // Displacement
                    float displacement = weather.SnowDisplacement;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Displacement", ref displacement, 0.0f, 0.1f, "%.3f"))
                    {
                        weather.SnowDisplacement = Math.Clamp(displacement, 0.0f, 0.1f);
                        UpdateWeatherManager(weather);
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Snow height displacement in world units (adds 3D depth to snow layer)");

                    ImGui.TreePop();
                }
            }
        }

        private static void DrawFogSection(Engine.Components.WeatherComponent weather)
        {
            if (ThemedImGui.CollapsingHeader("🌫️ Fog", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Fog Enabled
                bool fogEnabled = weather.FogEnabled;
                if (ImGui.Checkbox("Enabled", ref fogEnabled))
                {
                    weather.FogEnabled = fogEnabled;
                    UpdateWeatherManager(weather);
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }

                if (weather.FogEnabled)
                {
                    // Fog Density
                    float fogDensity = weather.FogDensity;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Density##Fog", ref fogDensity, 0.0f, 0.5f, "%.3f"))
                    {
                        weather.FogDensity = Math.Clamp(fogDensity, 0.0f, 0.5f);
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Exponential fog density");

                    // Fog Start/End (Linear fog)
                    float fogStart = weather.FogStart;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.DragFloat("Start Distance", ref fogStart, 0.1f, 0.0f, weather.FogEnd - 0.1f))
                    {
                        weather.FogStart = Math.Max(0.0f, fogStart);
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Distance where fog starts (linear fog)");

                    float fogEnd = weather.FogEnd;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.DragFloat("End Distance", ref fogEnd, 0.1f, weather.FogStart + 0.1f, 1000.0f))
                    {
                        weather.FogEnd = Math.Max(weather.FogStart + 0.1f, fogEnd);
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Distance where fog reaches maximum (linear fog)");

                    // Fog Color Mode
                    string[] colorModeNames = { "Custom", "Ambient", "Skybox", "IBL" };
                    int currentColorMode = (int)weather.FogColorMode;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.Combo("Color Mode", ref currentColorMode, colorModeNames, colorModeNames.Length))
                    {
                        weather.FogColorMode = (Engine.Components.FogColorMode)currentColorMode;
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Custom: Use color picker below\nAmbient: Match ambient light (realistic night fog)\nSkybox: Match horizon color\nIBL: Match HDRI irradiance");

                    // Fog Color (only show when Custom mode)
                    if (weather.FogColorMode == Engine.Components.FogColorMode.Custom)
                    {
                        var fogColor = weather.FogColor;
                        var color = new System.Numerics.Vector3(fogColor.X, fogColor.Y, fogColor.Z);
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.ColorEdit3("Color", ref color, ImGuiColorEditFlags.NoAlpha))
                        {
                            weather.FogColor = new System.Numerics.Vector3(color.X, color.Y, color.Z);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "  Color determined by environment");
                    }

                    // Advanced Fog Parameters
                    if (ImGui.TreeNode("🌫️ Advanced Fog"))
                    {
                        float fogOpacity = weather.FogOpacity;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Opacity", ref fogOpacity, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.FogOpacity = Math.Clamp(fogOpacity, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Overall fog opacity multiplier");

                        float fogThickness = weather.FogThickness;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Thickness", ref fogThickness, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.FogThickness = Math.Clamp(fogThickness, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("How opaque/thick fog gets at maximum density");

                        float fogLayerHeight = weather.FogLayerHeight;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.DragFloat("Layer Height", ref fogLayerHeight, 1.0f, -100.0f, 500.0f))
                        {
                            weather.FogLayerHeight = fogLayerHeight;
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Height where fog is thickest (meters)");

                        float fogNoiseScale = weather.FogNoiseScale;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Noise Scale", ref fogNoiseScale, 0.1f, 2.0f, "%.2f"))
                        {
                            weather.FogNoiseScale = Math.Clamp(fogNoiseScale, 0.1f, 2.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Volumetric noise detail scale");

                        float fogNoiseSpeed = weather.FogNoiseSpeed;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Noise Speed", ref fogNoiseSpeed, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.FogNoiseSpeed = Math.Clamp(fogNoiseSpeed, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Fog animation/morphing speed");

                        float fogScattering = weather.FogScattering;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Sun Scattering", ref fogScattering, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.FogScattering = Math.Clamp(fogScattering, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Sun glow through fog (forward scattering)");

                        // FBM Parameters
                        if (ImGui.TreeNode("FBM Parameters"))
                        {
                            int fogFBMOctaves = weather.FogFBMOctaves;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderInt("Octaves", ref fogFBMOctaves, 2, 6))
                            {
                                weather.FogFBMOctaves = Math.Clamp(fogFBMOctaves, 2, 6);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Number of noise octaves (higher = more detail, slower)");

                            float fogFBMLacunarity = weather.FogFBMLacunarity;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Lacunarity", ref fogFBMLacunarity, 1.5f, 3.0f, "%.2f"))
                            {
                                weather.FogFBMLacunarity = Math.Clamp(fogFBMLacunarity, 1.5f, 3.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Frequency multiplier per octave");

                            float fogFBMGain = weather.FogFBMGain;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Gain", ref fogFBMGain, 0.3f, 0.7f, "%.2f"))
                            {
                                weather.FogFBMGain = Math.Clamp(fogFBMGain, 0.3f, 0.7f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Amplitude multiplier per octave");

                            ImGui.TreePop();
                        }

                        ImGui.TreePop();
                    }
                }
            }
        }

        private static void DrawCloudSection(Engine.Components.WeatherComponent weather)
        {
            if (ThemedImGui.CollapsingHeader("☁️ Clouds", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Cloud Enabled toggle
                bool cloudEnabled = weather.CloudEnabled;
                if (ImGui.Checkbox("Enabled##CloudEnabled", ref cloudEnabled))
                {
                    weather.CloudEnabled = cloudEnabled;
                    UpdateWeatherManager(weather);
                    Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                }

                if (weather.CloudEnabled)
                {
                    // Cloud Type dropdown
                    string[] typeNames = { "Cirrus", "Cumulus", "Stratus", "Storm" };
                    int currentType = (int)weather.CloudType;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.Combo("Type", ref currentType, typeNames, typeNames.Length))
                    {
                        weather.CloudType = (Engine.Components.CloudType)currentType;
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Cirrus: Thin wispy / Cumulus: Fluffy puffy / Stratus: Layered / Storm: Dense dark");

                    // Coverage slider (CORRECTED: higher value = more clouds)
                    float coverage = weather.CloudCoverage;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Coverage", ref coverage, 0.0f, 1.0f, "%.2f"))
                    {
                        weather.CloudCoverage = Math.Clamp(coverage, 0.0f, 1.0f);
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Cloud coverage: 0 = clear sky, 1 = overcast (FIXED: now correctly increases clouds)");

                    // Density slider
                    float density = weather.CloudDensity;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Density##Cloud", ref density, 0.0f, 1.0f, "%.2f"))
                    {
                        weather.CloudDensity = Math.Clamp(density, 0.0f, 1.0f);
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("0 = transparent, 1 = opaque");

                    // Opacity slider
                    float opacity = weather.CloudOpacity;
                    ImGui.SetNextItemWidth(-120);
                    if (ImGui.SliderFloat("Opacity", ref opacity, 0.0f, 1.0f, "%.2f"))
                    {
                        weather.CloudOpacity = Math.Clamp(opacity, 0.0f, 1.0f);
                        UpdateWeatherManager(weather);
                        Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Overall opacity multiplier (0 = invisible, 1 = full opacity)");

                    // Advanced cloud parameters
                    if (ImGui.TreeNode("Advanced Cloud"))
                    {
                        float scattering = weather.CloudScattering;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Sun Scattering", ref scattering, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.CloudScattering = Math.Clamp(scattering, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Intensity of sun light scattering through clouds (silver lining effect).\nClouds automatically dim at night based on light intensity (like WaterOcean).");

                        float ambient = weather.CloudAmbient;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Ambient", ref ambient, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.CloudAmbient = Math.Clamp(ambient, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Ambient sky light contribution.\nAutomatically reduced at night based on environment brightness.");

                        float cloudSpeed = weather.CloudSpeed;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Animation Speed", ref cloudSpeed, 0.0f, 3.0f, "%.2f"))
                        {
                            weather.CloudSpeed = Math.Clamp(cloudSpeed, 0.0f, 3.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Cloud animation speed multiplier (affected by wind)");

                        float turbulence = weather.CloudTurbulence;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Turbulence", ref turbulence, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.CloudTurbulence = Math.Clamp(turbulence, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Shape distortion and chaos over time");

                        float detailSpeed = weather.CloudDetailSpeed;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Detail Evolution", ref detailSpeed, 0.0f, 2.0f, "%.2f"))
                        {
                            weather.CloudDetailSpeed = Math.Clamp(detailSpeed, 0.0f, 2.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Speed of organic shape changes using FBM evolution (0 = static, 2 = fast morphing).\nControls how fast cloud details evolve over time.");

                        ImGui.Spacing();
                        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.9f, 1.0f, 1.0f), "Fine-Tune Controls:");
                        ImGui.Spacing();

                        // Noise Scale
                        float noiseScale = weather.CloudNoiseScale;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Noise Scale", ref noiseScale, 0.1f, 5.0f, "%.2f"))
                        {
                            weather.CloudNoiseScale = Math.Clamp(noiseScale, 0.1f, 5.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Global scale of noise patterns (lower = larger clouds, higher = smaller details)");

                        // Morph Speed
                        float morphSpeed = weather.CloudMorphSpeed;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Morph Speed", ref morphSpeed, 0.0f, 2.0f, "%.2f"))
                        {
                            weather.CloudMorphSpeed = Math.Clamp(morphSpeed, 0.0f, 2.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Speed of organic morphing animation (0 = frozen, 2 = fast evolution)");

                        // Edge Softness
                        float edgeSoftness = weather.CloudEdgeSoftness;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Edge Softness", ref edgeSoftness, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.CloudEdgeSoftness = Math.Clamp(edgeSoftness, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Cloud edge appearance (0 = sharp/hard, 1 = soft/fuzzy)");

                        // Billowiness
                        float billowiness = weather.CloudBillowiness;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Billowiness", ref billowiness, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.CloudBillowiness = Math.Clamp(billowiness, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Cotton/billowy appearance strength (0 = smooth, 1 = fluffy/puffy)");

                        // Detail Strength
                        float detailStrength = weather.CloudDetailStrength;
                        ImGui.SetNextItemWidth(-120);
                        if (ImGui.SliderFloat("Detail Strength", ref detailStrength, 0.0f, 1.0f, "%.2f"))
                        {
                            weather.CloudDetailStrength = Math.Clamp(detailStrength, 0.0f, 1.0f);
                            UpdateWeatherManager(weather);
                            Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                        }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("Fine detail layer strength (0 = smooth/simple, 1 = highly detailed)");

                        // === DUAL-LAYER SCROLLING NOISE ===
                        if (ImGui.TreeNode("🌪️ Dual-Layer Scrolling Noise"))
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(0.9f, 0.7f, 0.4f, 1.0f), "Layer 1 (Primary - Large Shapes):");
                            ImGui.Spacing();

                            // Layer 1 Speed
                            float layer1Speed = weather.NoiseLayer1Speed;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Speed##Layer1", ref layer1Speed, 0.0f, 3.0f, "%.2f"))
                            {
                                weather.NoiseLayer1Speed = Math.Clamp(layer1Speed, 0.0f, 3.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Scrolling speed multiplier for primary noise layer");

                            // Layer 1 Direction
                            float layer1Angle = MathF.Atan2(weather.NoiseLayer1DirectionY, weather.NoiseLayer1DirectionX) * (180.0f / MathF.PI);
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Direction##Layer1", ref layer1Angle, -180.0f, 180.0f, "%.0f°"))
                            {
                                float rad = layer1Angle * (MathF.PI / 180.0f);
                                weather.NoiseLayer1DirectionX = MathF.Cos(rad);
                                weather.NoiseLayer1DirectionY = MathF.Sin(rad);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Scrolling direction for primary noise layer (0° = East, 90° = North)");

                            // Layer 1 Scale
                            float layer1Scale = weather.NoiseLayer1Scale;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Scale##Layer1", ref layer1Scale, 0.1f, 5.0f, "%.2f"))
                            {
                                weather.NoiseLayer1Scale = Math.Clamp(layer1Scale, 0.1f, 5.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Noise scale for primary layer (lower = larger patterns)");

                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();
                            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.9f, 0.7f, 1.0f), "Layer 2 (Detail - Fine Erosion):");
                            ImGui.Spacing();

                            // Layer 2 Speed
                            float layer2Speed = weather.NoiseLayer2Speed;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Speed##Layer2", ref layer2Speed, 0.0f, 3.0f, "%.2f"))
                            {
                                weather.NoiseLayer2Speed = Math.Clamp(layer2Speed, 0.0f, 3.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Scrolling speed multiplier for detail noise layer");

                            // Layer 2 Direction
                            float layer2Angle = MathF.Atan2(weather.NoiseLayer2DirectionY, weather.NoiseLayer2DirectionX) * (180.0f / MathF.PI);
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Direction##Layer2", ref layer2Angle, -180.0f, 180.0f, "%.0f°"))
                            {
                                float rad = layer2Angle * (MathF.PI / 180.0f);
                                weather.NoiseLayer2DirectionX = MathF.Cos(rad);
                                weather.NoiseLayer2DirectionY = MathF.Sin(rad);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Scrolling direction for detail noise layer (0° = East, 90° = North)");

                            // Layer 2 Scale
                            float layer2Scale = weather.NoiseLayer2Scale;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Scale##Layer2", ref layer2Scale, 0.1f, 5.0f, "%.2f"))
                            {
                                weather.NoiseLayer2Scale = Math.Clamp(layer2Scale, 0.1f, 5.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Noise scale for detail layer (higher = finer details and erosion)");

                            ImGui.TreePop();
                        }

                        // === FBM PARAMETERS ===
                        if (ImGui.TreeNode("🔬 FBM (Fractal Brownian Motion)"))
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.8f, 0.3f, 1.0f), "Advanced Noise Configuration:");
                            ImGui.Spacing();

                            // FBM Octaves
                            int fbmOctaves = weather.FBMOctaves;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderInt("Octaves", ref fbmOctaves, 2, 8))
                            {
                                weather.FBMOctaves = Math.Clamp(fbmOctaves, 2, 8);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Number of noise layers (more = more detail but slower)");

                            // FBM Lacunarity
                            float fbmLacunarity = weather.FBMLacunarity;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Lacunarity", ref fbmLacunarity, 1.5f, 3.0f, "%.2f"))
                            {
                                weather.FBMLacunarity = Math.Clamp(fbmLacunarity, 1.5f, 3.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Frequency multiplier per octave (higher = faster frequency increase)");

                            // FBM Gain
                            float fbmGain = weather.FBMGain;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Gain", ref fbmGain, 0.3f, 0.7f, "%.2f"))
                            {
                                weather.FBMGain = Math.Clamp(fbmGain, 0.3f, 0.7f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Amplitude multiplier per octave (higher = stronger detail layers)");

                            // FBM Strength
                            float fbmStrength = weather.FBMStrength;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Strength", ref fbmStrength, 0.0f, 1.0f, "%.2f"))
                            {
                                weather.FBMStrength = Math.Clamp(fbmStrength, 0.0f, 1.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Overall FBM contribution (0 = simple noise, 1 = full fractal detail)");

                            ImGui.Spacing();
                            ImGui.Separator();
                            ImGui.Spacing();

                            // Worley Weight
                            float worleyWeight = weather.WorleyWeight;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Worley Weight", ref worleyWeight, 0.0f, 1.0f, "%.2f"))
                            {
                                weather.WorleyWeight = Math.Clamp(worleyWeight, 0.0f, 1.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Cellular noise weight (0 = Perlin smooth, 1 = Worley cellular/billowy)");

                            // Erosion
                            float erosion = weather.Erosion;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Erosion", ref erosion, 0.0f, 1.0f, "%.2f"))
                            {
                                weather.Erosion = Math.Clamp(erosion, 0.0f, 1.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Creates holes and tears in clouds (0 = solid, 1 = highly eroded/torn)");

                            // Sharpness
                            float sharpness = weather.Sharpness;
                            ImGui.SetNextItemWidth(-120);
                            if (ImGui.SliderFloat("Sharpness", ref sharpness, 0.0f, 1.0f, "%.2f"))
                            {
                                weather.Sharpness = Math.Clamp(sharpness, 0.0f, 1.0f);
                                UpdateWeatherManager(weather);
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Edge definition (0 = very soft/fuzzy edges, 1 = sharp/defined edges)");

                            ImGui.TreePop();
                        }

                        ImGui.TreePop();
                    }
                }
            }
        }

        private static void DrawMaterialsSection(Engine.Components.WeatherComponent weather)
        {
            if (ThemedImGui.CollapsingHeader("🎨 Surface Materials", ImGuiTreeNodeFlags.None))
            {
                // Snow Material
                ImGui.TextColored(new Vector4(0.8f, 0.9f, 1.0f, 1.0f), "Snow Material");
                
                if (weather.SnowMapMaterial.HasValue)
                {
                    string snowName = Engine.Assets.AssetDatabase.GetName(weather.SnowMapMaterial.Value) ?? "Unknown";
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.3f, 0.4f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.4f, 0.5f, 1.0f));
                    ImGui.Button($"  {snowName}  ", new Vector2(-1, 40));
                    ImGui.PopStyleColor(2);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.4f, 1.0f));
                    ImGui.Button("Drag & Drop Snow Material Here", new Vector2(-1, 40));
                    ImGui.PopStyleColor(2);
                }
                
                // Drag & drop target for snow material
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                    unsafe
                    {
                        if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                        {
                            try
                            {
                                var span = new ReadOnlySpan<byte>((void*)payload.Data, 16);
                                var droppedGuid = new Guid(span);
                                
                                if (Engine.Assets.AssetDatabase.TryGet(droppedGuid, out var record))
                                {
                                    if (string.Equals(record.Type, "Material", StringComparison.OrdinalIgnoreCase))
                                    {
                                        weather.SnowMapMaterial = droppedGuid;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Right-click context menu
                if (ImGui.BeginPopupContextItem("SnowMapContext"))
                {
                    if (ImGui.MenuItem("Clear"))
                    {
                        weather.SnowMapMaterial = null;
                    }
                    ImGui.EndPopup();
                }

                ImGui.Spacing();

                // Wetness Map Material
                ImGui.TextColored(new Vector4(0.4f, 0.7f, 0.9f, 1.0f), "Wetness Map");
                
                if (weather.WetnessMapMaterial.HasValue)
                {
                    string wetnessName = Engine.Assets.AssetDatabase.GetName(weather.WetnessMapMaterial.Value) ?? "Unknown";
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.3f, 0.4f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.4f, 0.5f, 1.0f));
                    ImGui.Button($"  {wetnessName}  ", new Vector2(-1, 40));
                    ImGui.PopStyleColor(2);
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.2f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.3f, 0.4f, 1.0f));
                    ImGui.Button("Drag & Drop Wetness Map Here", new Vector2(-1, 40));
                    ImGui.PopStyleColor(2);
                }
                
                // Drag & drop target for wetness material
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                    unsafe
                    {
                        if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                        {
                            try
                            {
                                var span = new ReadOnlySpan<byte>((void*)payload.Data, 16);
                                var droppedGuid = new Guid(span);
                                
                                if (Engine.Assets.AssetDatabase.TryGet(droppedGuid, out var record))
                                {
                                    if (string.Equals(record.Type, "Material", StringComparison.OrdinalIgnoreCase))
                                    {
                                        weather.WetnessMapMaterial = droppedGuid;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Right-click context menu
                if (ImGui.BeginPopupContextItem("WetnessMapContext"))
                {
                    if (ImGui.MenuItem("Clear"))
                    {
                        weather.WetnessMapMaterial = null;
                    }
                    ImGui.EndPopup();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Procedural generation options
                ImGui.TextColored(new Vector4(0.9f, 0.8f, 0.4f, 1.0f), "⚡ Procedural Generation");
                
                if (ImGui.Button("Generate Wetness Map (Perlin)", new Vector2(-1, 0)))
                {
                    GenerateProceduralWetnessMap(weather);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Generate procedural wetness map using Perlin noise");
                
                if (ImGui.Button("Generate Snow Map (Simplex)", new Vector2(-1, 0)))
                {
                    GenerateProceduralSnowMap(weather);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Generate procedural snow coverage map using Simplex noise");
            }
        }

        private static void DrawParticleSystemsSection(Engine.Components.WeatherComponent weather)
        {
            if (ThemedImGui.CollapsingHeader("💨 Particle Systems", ImGuiTreeNodeFlags.None))
            {
                var scene = Editor.Panels.EditorUI.MainViewport.Renderer?.Scene;
                
                // Rain Particle System
                ImGui.Text("Rain Particles");
                ImGui.SameLine();
                
                string rainName = weather.RainParticleSystem != null 
                    ? (weather.RainParticleSystem.Name ?? $"Entity {weather.RainParticleSystem.Id}") 
                    : "<none>";
                
                var buttonColor = weather.RainParticleSystem != null 
                    ? new Vector4(0.2f, 0.5f, 0.8f, 1.0f) 
                    : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
                
                ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(buttonColor.X * 1.2f, buttonColor.Y * 1.2f, buttonColor.Z * 1.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(buttonColor.X * 0.8f, buttonColor.Y * 0.8f, buttonColor.Z * 0.8f, 1.0f));
                
                ImGui.Button($"{rainName}##RainPSButton", new Vector2(-1, 24));
                
                ImGui.PopStyleColor(3);
                
                // Drag & drop target for rain particle system entity
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("ENTITY_ID");
                    unsafe
                    {
                        if (payload.NativePtr != null && payload.DataSize == sizeof(int))
                        {
                            int droppedId = *(int*)payload.Data;
                            var entity = scene?.GetById((uint)droppedId);
                            if (entity != null && entity.HasComponent<Engine.Components.ParticleSystem>())
                            {
                                weather.RainParticleSystem = entity;
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Right-click context menu
                if (ImGui.BeginPopupContextItem("RainPSContext"))
                {
                    if (ImGui.MenuItem("Clear"))
                    {
                        weather.RainParticleSystem = null;
                    }
                    ImGui.EndPopup();
                }

                ImGui.Spacing();

                // Snow Particle System
                ImGui.Text("Snow Particles");
                ImGui.SameLine();
                
                string snowName = weather.SnowParticleSystem != null 
                    ? (weather.SnowParticleSystem.Name ?? $"Entity {weather.SnowParticleSystem.Id}") 
                    : "<none>";
                
                buttonColor = weather.SnowParticleSystem != null 
                    ? new Vector4(0.8f, 0.9f, 1.0f, 1.0f) 
                    : new Vector4(0.3f, 0.3f, 0.3f, 1.0f);
                
                ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(buttonColor.X * 1.1f, buttonColor.Y * 1.1f, buttonColor.Z * 1.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(buttonColor.X * 0.9f, buttonColor.Y * 0.9f, buttonColor.Z * 0.9f, 1.0f));
                
                ImGui.Button($"{snowName}##SnowPSButton", new Vector2(-1, 24));
                
                ImGui.PopStyleColor(3);
                
                // Drag & drop target for snow particle system entity
                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("ENTITY_ID");
                    unsafe
                    {
                        if (payload.NativePtr != null && payload.DataSize == sizeof(int))
                        {
                            int droppedId = *(int*)payload.Data;
                            var entity = scene?.GetById((uint)droppedId);
                            if (entity != null && entity.HasComponent<Engine.Components.ParticleSystem>())
                            {
                                weather.SnowParticleSystem = entity;
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
                
                // Right-click context menu
                if (ImGui.BeginPopupContextItem("SnowPSContext"))
                {
                    if (ImGui.MenuItem("Clear"))
                    {
                        weather.SnowParticleSystem = null;
                    }
                    ImGui.EndPopup();
                }

                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Tip: Drag entities with ParticleSystem from Hierarchy");
                ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Particles auto-enable/disable based on intensity.");
            }
        }

        private static void DrawTransitionSettings(Engine.Components.WeatherComponent weather)
        {
            if (ImGui.CollapsingHeader("⚙️ Transition Settings"))
            {
                // Transition Speed
                float transSpeed = weather.TransitionSpeed;
                ImGui.SetNextItemWidth(-120);
                if (ImGui.SliderFloat("Speed", ref transSpeed, 0.1f, 5.0f, "%.1f"))
                {
                    weather.TransitionSpeed = Math.Clamp(transSpeed, 0.1f, 5.0f);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Speed multiplier for weather transitions");

                // Auto Transitions
                bool autoTransitions = weather.EnableAutoTransitions;
                if (ImGui.Checkbox("Enable Auto Transitions", ref autoTransitions))
                {
                    weather.EnableAutoTransitions = autoTransitions;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Automatically transition between random weather presets");

                if (weather.EnableAutoTransitions)
                {
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "  Weather will change every ~2 minutes");
                }
            }
        }

        private static void GenerateProceduralWetnessMap(Engine.Components.WeatherComponent weather)
        {
            try
            {
                // Generate procedural wetness texture using Perlin noise
                const int width = 512;
                const int height = 512;
                byte[] pixels = new byte[width * height * 4]; // RGBA

                var noise = new Engine.Mathx.Noise.PerlinNoise(DateTime.Now.Millisecond);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float nx = x / (float)width;
                        float ny = y / (float)height;
                        
                        // Multi-octave Perlin noise for wetness pattern
                        float value = noise.SampleFractal(nx * 8f, ny * 8f, 4, 2.0f, 0.5f);
                        
                        // Remap from [-1,1] to [0,1]
                        value = (value + 1f) * 0.5f;
                        byte intensity = (byte)(value * 255f);
                        
                        int idx = (y * width + x) * 4;
                        pixels[idx + 0] = intensity; // R
                        pixels[idx + 1] = intensity; // G
                        pixels[idx + 2] = intensity; // B
                        pixels[idx + 3] = 255; // A
                    }
                }

                // Save texture to Assets folder
                string texturePath = $"Assets/Textures/Generated/wetness_map_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                SaveTextureToFile(pixels, width, height, texturePath);
                
                Editor.Logging.LogManager.LogInfo($"Wetness map generated: {texturePath}", "WeatherInspector");
            }
            catch (Exception ex)
            {
                Editor.Logging.LogManager.LogError($"Failed to generate wetness map: {ex.Message}", "WeatherInspector");
            }
        }

        private static void GenerateProceduralSnowMap(Engine.Components.WeatherComponent weather)
        {
            try
            {
                // Generate procedural snow coverage texture using Simplex noise
                const int width = 512;
                const int height = 512;
                byte[] pixels = new byte[width * height * 4]; // RGBA

                var noise = new Engine.Mathx.Noise.SimplexNoise(DateTime.Now.Millisecond);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float nx = x / (float)width;
                        float ny = y / (float)height;
                        
                        // Multi-octave Simplex noise for snow coverage
                        float value = noise.SampleFractal(nx * 6f, ny * 6f, 3, 2.0f, 0.6f);
                        
                        // Remap and boost contrast
                        value = (value + 1f) * 0.5f;
                        value = MathF.Pow(value, 1.5f); // Increase contrast
                        byte intensity = (byte)(value * 255f);
                        
                        int idx = (y * width + x) * 4;
                        pixels[idx + 0] = intensity; // R
                        pixels[idx + 1] = intensity; // G
                        pixels[idx + 2] = intensity; // B
                        pixels[idx + 3] = 255; // A
                    }
                }

                // Save texture to Assets folder
                string texturePath = $"Assets/Textures/Generated/snow_map_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                SaveTextureToFile(pixels, width, height, texturePath);
                
                Editor.Logging.LogManager.LogInfo($"Snow map generated: {texturePath}", "WeatherInspector");
            }
            catch (Exception ex)
            {
                Editor.Logging.LogManager.LogError($"Failed to generate snow map: {ex.Message}", "WeatherInspector");
            }
        }

        private static void SaveTextureToFile(byte[] pixels, int width, int height, string relativePath)
        {
            // Ensure directory exists
            string fullPath = Path.Combine(Environment.CurrentDirectory, relativePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save as PNG using SixLabors.ImageSharp if available, otherwise use System.Drawing
            try
            {
                // Try using System.Drawing.Common (should be available on Windows)
                using var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bitmapData = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
                bitmap.UnlockBits(bitmapData);
                
                bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);
                
                // Refresh asset database to recognize new texture
                Engine.Assets.AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Editor.Logging.LogManager.LogError($"Failed to save texture: {ex.Message}", "WeatherInspector");
            }
        }
    }
}
