using System;
using System.Linq;
using ImGuiNET;
using Engine.Components;
using Engine.Rendering;
using System.Numerics;

namespace Editor.Inspector
{
    public static class GlobalEffectsInspector
    {
        public static void DrawInspector(GlobalEffects globalEffects)
        {
            if (globalEffects == null) return;

            ImGui.Text("Global Effects");
            ImGui.Separator();

            // Bouton pour ajouter des effets
            if (ImGui.Button("Add Effect", new Vector2(120, 0)))
            {
                ImGui.OpenPopup("AddEffectPopup");
            }

            // Menu contextuel pour ajouter des effets
            if (ImGui.BeginPopup("AddEffectPopup"))
            {
                ImGui.Text("Post-Process Effects");
                ImGui.Separator();

                if (ImGui.MenuItem("Bloom") && !globalEffects.HasEffect<BloomEffect>())
                {
                    globalEffects.AddEffect<BloomEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Tone Mapping") && !globalEffects.HasEffect<ToneMappingEffect>())
                {
                    globalEffects.AddEffect<ToneMappingEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Chromatic Aberration") && !globalEffects.HasEffect<ChromaticAberrationEffect>())
                {
                    globalEffects.AddEffect<ChromaticAberrationEffect>();
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            ImGui.Spacing();

            // Afficher les effets existants
            var effects = globalEffects.Effects.ToList();
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                DrawEffectInspector(effect, globalEffects, i);
            }
        }

        private static void DrawEffectInspector(PostProcessEffect effect, GlobalEffects globalEffects, int index)
        {
            var flags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Framed;
            bool nodeOpen = ImGui.TreeNodeEx($"{effect.EffectName}##{index}", flags);

            // Bouton pour supprimer l'effet
            ImGui.SameLine(ImGui.GetWindowWidth() - 30);
            if (ImGui.Button($"X##{index}", new Vector2(20, 0)))
            {
                if (effect is BloomEffect)
                    globalEffects.RemoveEffect<BloomEffect>();
                else if (effect is ToneMappingEffect)
                    globalEffects.RemoveEffect<ToneMappingEffect>();
                else if (effect is ChromaticAberrationEffect)
                    globalEffects.RemoveEffect<ChromaticAberrationEffect>();
            }

            if (nodeOpen)
            {
                // Paramètres communs
                var enabled = effect.Enabled;
                if (ImGui.Checkbox($"Enabled##{index}", ref enabled))
                    effect.Enabled = enabled;
                effect.Intensity = ImGuiHelper.SliderFloat($"Intensity##{index}", effect.Intensity, 0f, 2f);

                ImGui.Spacing();

                // Paramètres spécifiques à chaque effet
                if (effect is BloomEffect bloom)
                {
                    DrawBloomInspector(bloom, index);
                }
                else if (effect is ToneMappingEffect toneMap)
                {
                    DrawToneMappingInspector(toneMap, index);
                }
                else if (effect is ChromaticAberrationEffect chromatic)
                {
                    DrawChromaticAberrationInspector(chromatic, index);
                }

                ImGui.TreePop();
            }
        }

        private static void DrawToneMappingInspector(ToneMappingEffect toneMap, int index)
        {
            // Mode de tone mapping
            var modes = Enum.GetNames(typeof(ToneMappingEffect.ToneMappingMode));
            int currentMode = (int)toneMap.Mode;
            if (ImGui.Combo($"Mode##{index}", ref currentMode, modes, modes.Length))
            {
                toneMap.Mode = (ToneMappingEffect.ToneMappingMode)currentMode;
            }

            // Exposition
            toneMap.Exposure = ImGuiHelper.SliderFloat($"Exposure##{index}", toneMap.Exposure, 0.1f, 5.0f);

            // White Point (seulement pour Reinhard Extended)
            if (toneMap.Mode == ToneMappingEffect.ToneMappingMode.ReinhardExtended)
            {
                toneMap.WhitePoint = ImGuiHelper.SliderFloat($"White Point##{index}", toneMap.WhitePoint, 0.5f, 5.0f);
            }

            // Gamma
            toneMap.Gamma = ImGuiHelper.SliderFloat($"Gamma##{index}", toneMap.Gamma, 1.0f, 3.0f);
        }

        private static void DrawBloomInspector(BloomEffect bloom, int index)
        {
            // Seuil d'extraction des zones lumineuses
            bloom.Threshold = ImGuiHelper.SliderFloat($"Threshold##{index}", bloom.Threshold, 0.0f, 3.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Brightness threshold for bloom extraction\nHigher values = only very bright areas bloom");
            }

            // Transition douce autour du seuil
            bloom.SoftKnee = ImGuiHelper.SliderFloat($"Soft Knee##{index}", bloom.SoftKnee, 0.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Soft transition around the threshold\nHigher values = smoother bloom falloff");
            }

            // Rayon du bloom
            bloom.Radius = ImGuiHelper.SliderFloat($"Radius##{index}", bloom.Radius, 0.1f, 3.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Bloom blur radius\nHigher values = larger bloom effect");
            }

            // Nombre d'itérations
            int iterations = bloom.Iterations;
            if (ImGui.SliderInt($"Iterations##{index}", ref iterations, 1, 8))
                bloom.Iterations = iterations;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of downsampling/upsampling passes\nMore iterations = larger bloom but more expensive");
            }

            // Clamp HDR
            bloom.Clamp = ImGuiHelper.SliderFloat($"HDR Clamp##{index}", bloom.Clamp, 1000.0f, 100000.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Clamps HDR values to prevent infinite bloom\nLower values = more controlled bloom");
            }

            // Scattering
            bloom.Scattering = ImGuiHelper.SliderFloat($"Scattering##{index}", bloom.Scattering, 0.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Controls bloom diffusion amount\nHigher values = more scattered/softer bloom");
            }
        }

        private static void DrawChromaticAberrationInspector(ChromaticAberrationEffect chromatic, int index)
        {
            // Force de l'aberration
            chromatic.Strength = ImGuiHelper.SliderFloat($"Strength##{index}", chromatic.Strength, 0.0f, 2.0f);

            // Distance focale
            chromatic.FocalLength = ImGuiHelper.SliderFloat($"Focal Length##{index}", chromatic.FocalLength, 10.0f, 200.0f);

            // Mode spectral
            var useSpectralLut = chromatic.UseSpectralLut;
            if (ImGui.Checkbox($"Use Spectral LUT##{index}", ref useSpectralLut))
                chromatic.UseSpectralLut = useSpectralLut;

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Use spectral sampling for more realistic chromatic aberration\n(more expensive but higher quality)");
            }
        }
    }

    /// <summary>
    /// Helper pour les contrôles ImGui
    /// </summary>
    public static class ImGuiHelper
    {
        public static float SliderFloat(string label, float value, float min, float max)
        {
            ImGui.SliderFloat(label, ref value, min, max);
            return value;
        }
    }
}