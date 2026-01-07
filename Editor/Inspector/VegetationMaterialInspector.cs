using System;
using ImGuiNET;
using Engine.Assets;
using Editor.UI;
using Editor.Themes;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    public static class VegetationMaterialInspector
    {
        private static UITheme UI => ThemeManager.UI;

        /// <summary>
        /// Draw vegetation material UI. Returns true if any property was changed.
        /// </summary>
        public static bool Draw(MaterialAsset material)
        {
            if (material == null || material.VegetationProperties == null) return false;
            
            bool changed = false;

            var veg = material.VegetationProperties;

            // Wind Mode Control (Global/Local/Blend)
            if (InspectorWidgets.Section("Wind Animation System", defaultOpen: true))
            {
                string[] modes = { "Global (Weather)", "Local (Material)", "Blend" };
                int currentMode = veg.WindMode;

                if (ImGui.Combo("Wind Mode", ref currentMode, modes, modes.Length))
                {
                    veg.WindMode = currentMode;
                    changed = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Global: Wind controlled by WeatherComponent\n" +
                        "Local: Wind controlled by material parameters\n" +
                        "Blend: Mix between local and global");
                }

                // Blend factor (only for Blend mode)
                if (currentMode == 2)
                {
                    float blendFactor = veg.WindBlendFactor;
                    if (ImGui.SliderFloat("Blend Factor", ref blendFactor, 0.0f, 1.0f))
                    {
                        veg.WindBlendFactor = blendFactor;
                        changed = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("0 = Local parameters, 1 = Global (Weather)");
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Local Wind Parameters");
                ImGui.Spacing();

                // Basic wind
                float windStrength = veg.WindStrength;
                if (ImGui.SliderFloat("Wind Strength", ref windStrength, 0.0f, 1.0f))
                {
                    veg.WindStrength = windStrength;
                    changed = true;
                }

                var windDir = new System.Numerics.Vector2(veg.WindDirection[0], veg.WindDirection[1]);
                if (ImGui.DragFloat2("Wind Direction", ref windDir, 0.01f))
                {
                    var normalized = Vector2.Normalize(new Vector2(windDir.X, windDir.Y));
                    veg.WindDirection = new float[] { normalized.X, normalized.Y };
                    changed = true;
                }

                float windSpeed = veg.WindSpeed;
                if (ImGui.DragFloat("Wind Speed", ref windSpeed, 0.01f, 0.0f, 5.0f))
                {
                    veg.WindSpeed = windSpeed;
                    changed = true;
                }

                float windGustiness = veg.WindGustiness;
                if (ImGui.SliderFloat("Wind Gustiness", ref windGustiness, 0.0f, 1.0f))
                {
                    veg.WindGustiness = windGustiness;
                    changed = true;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Advanced Wind (Vegetation)");
                ImGui.Spacing();

                // Branch parameters
                float branchAmp = veg.BranchAmplitude;
                if (ImGui.DragFloat("Branch Amplitude", ref branchAmp, 0.1f, 0.0f, 10.0f))
                {
                    veg.BranchAmplitude = branchAmp;
                    changed = true;
                }

                float branchSpeed = veg.BranchSpeed;
                if (ImGui.DragFloat("Branch Speed", ref branchSpeed, 0.1f, 0.0f, 10.0f))
                {
                    veg.BranchSpeed = branchSpeed;
                    changed = true;
                }

                float branchTurb = veg.BranchTurbulence;
                if (ImGui.SliderFloat("Branch Turbulence", ref branchTurb, 0.0f, 1.0f))
                {
                    veg.BranchTurbulence = branchTurb;
                    changed = true;
                }

                // Trunk parameters
                float trunkStiff = veg.TrunkStiffness;
                if (ImGui.SliderFloat("Trunk Stiffness", ref trunkStiff, 0.0f, 1.0f))
                {
                    veg.TrunkStiffness = trunkStiff;
                    changed = true;
                }

                float trunkBend = veg.TrunkBendAmount;
                if (ImGui.SliderFloat("Trunk Bend", ref trunkBend, 0.0f, 1.0f))
                {
                    veg.TrunkBendAmount = trunkBend;
                    changed = true;
                }

                // Leaf parameters
                float leafFlutter = veg.LeafFlutter;
                if (ImGui.SliderFloat("Leaf Flutter", ref leafFlutter, 0.0f, 1.0f))
                {
                    veg.LeafFlutter = leafFlutter;
                    changed = true;
                }

                float leafSpeed = veg.LeafFlutterSpeed;
                if (ImGui.DragFloat("Leaf Flutter Speed", ref leafSpeed, 0.1f, 0.0f, 20.0f))
                {
                    veg.LeafFlutterSpeed = leafSpeed;
                    changed = true;
                }

                InspectorWidgets.InfoBox(
                    currentMode == 0
                        ? "Global mode: Vegetation wind driven by WeatherComponent"
                        : currentMode == 1
                        ? "Local mode: Wind controlled by material parameters only"
                        : "Blend mode: Mix between local material and global weather");
            }
            
            return changed;
        }
    }
}
