using System;
using System.Numerics;
using Engine.Audio.Components;
using Engine.Audio.Effects;
using ImGuiNET;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector for ReverbZoneComponent
    /// </summary>
    public static class ReverbZoneInspector
    {
        public static void DrawInspector(ReverbZoneComponent zone)
        {
            if (zone == null) return;

            ImGui.Separator();
            ImGui.Text("Reverb Zone");
            ImGui.Spacing();

            // Enabled toggle
            bool enabled = zone.Enabled;
            if (ImGui.Checkbox("Enabled", ref enabled))
            {
                zone.Enabled = enabled;
            }

            // Inner and Outer Radius
            float innerRadius = zone.InnerRadius;
            if (ImGui.DragFloat("Inner Radius", ref innerRadius, 0.1f, 0.1f, 1000f))
            {
                zone.InnerRadius = Math.Max(0.1f, innerRadius);
            }

            float outerRadius = zone.OuterRadius;
            if (ImGui.DragFloat("Outer Radius", ref outerRadius, 0.1f, 0.1f, 1000f))
            {
                zone.OuterRadius = Math.Max(zone.InnerRadius, outerRadius);
            }

            // Reverb Preset
            ImGui.Spacing();
            ImGui.Text("Reverb Preset:");
            var currentPreset = zone.Preset;
            string[] presetNames = Enum.GetNames(typeof(ReverbZoneComponent.ReverbPreset));
            int currentPresetIndex = (int)currentPreset;

            if (ImGui.Combo("##ReverbPreset", ref currentPresetIndex, presetNames, presetNames.Length))
            {
                zone.SetPreset((ReverbZoneComponent.ReverbPreset)currentPresetIndex);
            }

            // Advanced settings (collapsible)
            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Advanced Reverb Settings"))
            {
                var settings = zone.ReverbSettings;

                float density = settings.Density;
                if (ImGui.SliderFloat("Density", ref density, 0f, 1f))
                {
                    settings.Density = density;
                    zone.UpdateReverbSettings(settings);
                }

                float diffusion = settings.Diffusion;
                if (ImGui.SliderFloat("Diffusion", ref diffusion, 0f, 1f))
                {
                    settings.Diffusion = diffusion;
                    zone.UpdateReverbSettings(settings);
                }

                float gain = settings.Gain;
                if (ImGui.SliderFloat("Gain", ref gain, 0f, 1f))
                {
                    settings.Gain = gain;
                    zone.UpdateReverbSettings(settings);
                }

                float gainHF = settings.GainHF;
                if (ImGui.SliderFloat("Gain HF", ref gainHF, 0f, 1f))
                {
                    settings.GainHF = gainHF;
                    zone.UpdateReverbSettings(settings);
                }

                float decayTime = settings.DecayTime;
                if (ImGui.SliderFloat("Decay Time", ref decayTime, 0.1f, 20f))
                {
                    settings.DecayTime = decayTime;
                    zone.UpdateReverbSettings(settings);
                }

                float lateReverbGain = settings.LateReverbGain;
                if (ImGui.SliderFloat("Late Reverb Gain", ref lateReverbGain, 0f, 10f))
                {
                    settings.LateReverbGain = lateReverbGain;
                    zone.UpdateReverbSettings(settings);
                }
            }

            // Visual help
            ImGui.Spacing();
            ImGui.TextWrapped("The reverb zone will apply reverb to 3D audio sources within its range. Sources inside the inner radius get full reverb, and it fades out to the outer radius.");
        }
    }
}
