using System;
using ImGuiNET;
using Engine.Assets;
using Editor.UI;
using Editor.Themes;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    public static class WaterMaterialInspector
    {
        private static UITheme UI => ThemeManager.UI;

        public static bool Draw(MaterialAsset material)
        {
            if (material == null || material.WaterProperties == null) return false;

            bool changed = false;
            var water = material.WaterProperties;

            // Wave Mode Control (Global/Local/Blend)
            if (InspectorWidgets.Section("Wave Animation System", defaultOpen: true))
            {
                string[] modes = { "Global (Weather)", "Local (Material)", "Blend" };
                int currentMode = water.WaveMode;

                if (ImGui.Combo("Wave Mode", ref currentMode, modes, modes.Length))
                {
                    water.WaveMode = currentMode;
                    changed = true;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Global: Waves controlled by WeatherComponent (wind)\n" +
                        "Local: Waves controlled by material parameters\n" +
                        "Blend: Mix between local and global");
                }

                // Blend factor (only for Blend mode)
                if (currentMode == 2)
                {
                    float blendFactor = water.WaveBlendFactor;
                    if (ImGui.SliderFloat("Blend Factor", ref blendFactor, 0.0f, 1.0f))
                    {
                        water.WaveBlendFactor = blendFactor;
                        changed = true;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("0 = Local parameters, 1 = Global (Weather)");
                    }
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Local Wave Parameters");
                ImGui.Spacing();

                // Local parameters (used when Mode = Local or Blend)
                float waveSpeed = water.WaveSpeed;
                if (ImGui.DragFloat("Wave Speed", ref waveSpeed, 0.01f, 0.0f, 10.0f))
                {
                    water.WaveSpeed = waveSpeed;
                    changed = true;
                }

                float waveAmplitude = water.WaveAmplitude;
                if (ImGui.DragFloat("Wave Amplitude", ref waveAmplitude, 0.01f, 0.0f, 5.0f))
                {
                    water.WaveAmplitude = waveAmplitude;
                    changed = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Base wave height (meters). In Global mode, this is scaled by wind strength.");
                }

                float waveFrequency = water.WaveFrequency;
                if (ImGui.DragFloat("Wave Frequency", ref waveFrequency, 0.01f, 0.1f, 10.0f))
                {
                    water.WaveFrequency = waveFrequency;
                    changed = true;
                }

                var waveDir = new System.Numerics.Vector2(water.WaveDirection[0], water.WaveDirection[1]);
                if (ImGui.DragFloat2("Wave Direction", ref waveDir, 0.01f))
                {
                    var normalized = Vector2.Normalize(new Vector2(waveDir.X, waveDir.Y));
                    water.WaveDirection = new float[] { normalized.X, normalized.Y };
                    changed = true;
                }

                InspectorWidgets.InfoBox(
                    currentMode == 0
                        ? "Global mode: Waves driven by Weather → Wind parameters"
                        : currentMode == 1
                        ? "Local mode: Waves controlled by material parameters only"
                        : "Blend mode: Mix between local material and global weather");
            }

            return changed;
        }
    }
}
