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

                if (ImGui.MenuItem("FXAA (Triple-A)") && !globalEffects.HasEffect<FXAAEffect>())
                {
                    globalEffects.AddEffect<FXAAEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("SSAO") && !globalEffects.HasEffect<SSAOEffect>())
                {
                    globalEffects.AddEffect<SSAOEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("GTAO") && !globalEffects.HasEffect<GTAOEffect>())
                {
                    globalEffects.AddEffect<GTAOEffect>();
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
                else if (effect is SSAOEffect)
                    globalEffects.RemoveEffect<SSAOEffect>();
                else if (effect is GTAOEffect)
                    globalEffects.RemoveEffect<GTAOEffect>();
                    else if (effect is FXAAEffect)
                        globalEffects.RemoveEffect<FXAAEffect>();
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
                else if (effect is SSAOEffect ssao)
                {
                    DrawSSAOInspector(ssao, index);
                }
                else if (effect is GTAOEffect gtao)
                {
                    DrawGTAOInspector(gtao, index);
                }
                else if (effect is FXAAEffect fxaa)
                {
                    DrawFXAAInspector(fxaa, index);
                }

                ImGui.TreePop();
            }
        }

        private static void DrawFXAAInspector(FXAAEffect fxaa, int index)
        {
            fxaa.Quality = ImGuiHelper.SliderFloat($"Quality##{index}", fxaa.Quality, 0.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("FXAA quality: 0 = faster / softer, 1 = higher quality");
            }

            // (debug checkbox removed)
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

        private static void DrawSSAOInspector(SSAOEffect ssao, int index)
        {
            // Rayon d'échantillonnage
            ssao.Radius = ImGuiHelper.SliderFloat($"Radius##{index}", ssao.Radius, 0.1f, 3.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Sampling radius in view space\nSmaller (0.1-0.5) = fine details, Larger (1-3) = broad occlusion");
            }

            // Bias pour éviter l'acné
            ssao.Bias = ImGuiHelper.SliderFloat($"Bias##{index}", ssao.Bias, 0.001f, 0.1f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Depth bias to prevent acne artifacts\nHigher = less artifacts but less detail");
            }

            // Puissance pour le contraste
            ssao.Power = ImGuiHelper.SliderFloat($"Power##{index}", ssao.Power, 0.5f, 3.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Occlusion power for contrast adjustment\nHigher = darker/more contrasted occlusion");
            }

            // Nombre d'échantillons
            int sampleCount = ssao.SampleCount;
            if (ImGui.SliderInt($"Sample Count##{index}", ref sampleCount, 4, 64))
                ssao.SampleCount = sampleCount;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of samples per pixel\nMore samples = better quality but slower\nRecommended: 16-32");
            }

            // Taille du flou
            int blurSize = ssao.BlurSize;
            if (ImGui.SliderInt($"Blur Size##{index}", ref blurSize, 0, 5))
                ssao.BlurSize = blurSize;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Blur kernel size to smooth the SSAO\n0 = no blur, 3-5 = smooth result");
            }

            // Distance maximale avec fade out progressif
            ssao.MaxDistance = ImGuiHelper.SliderFloat($"Max Distance##{index}", ssao.MaxDistance, 10.0f, 200.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Maximum distance for SSAO effect\nFades out progressively from 70% to 100% of this distance\nOptimizes performance by skipping distant objects");
            }
        }

        private static void DrawGTAOInspector(GTAOEffect gtao, int index)
        {
            // Rayon d'échantillonnage
            gtao.Radius = ImGuiHelper.SliderFloat($"Radius##{index}", gtao.Radius, 0.1f, 2.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Sampling radius in view space units\nSmaller (0.1-0.5) = fine details, Larger (0.5-2.0) = broad occlusion");
            }

            // Épaisseur des surfaces
            gtao.Thickness = ImGuiHelper.SliderFloat($"Thickness##{index}", gtao.Thickness, 0.1f, 3.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Surface thickness for occlusion calculation\nHigher = thicker surfaces, more occlusion");
            }

            // Falloff range
            gtao.FalloffRange = ImGuiHelper.SliderFloat($"Falloff Range##{index}", gtao.FalloffRange, 0.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Distance falloff range for occlusion\nControls how occlusion fades with distance");
            }

            // Nombre d'échantillons par slice
            int sampleCount = gtao.SampleCount;
            if (ImGui.SliderInt($"Samples per Slice##{index}", ref sampleCount, 2, 6))
                gtao.SampleCount = sampleCount;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of samples per slice direction\nMore samples = better quality but slower\nRecommended: 3-4");
            }

            // Nombre de slices
            int sliceCount = gtao.SliceCount;
            if (ImGui.SliderInt($"Slice Count##{index}", ref sliceCount, 1, 4))
                gtao.SliceCount = sliceCount;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of slice directions\nMore slices = better quality but slower\nRecommended: 2-3");
            }

            // Rayon du blur
            int blurRadius = gtao.BlurRadius;
            if (ImGui.SliderInt($"Blur Radius##{index}", ref blurRadius, 1, 5))
                gtao.BlurRadius = blurRadius;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Spatial blur radius for denoising\n1-2 = sharp, 3-5 = smooth");
            }

            // Distance maximale
            gtao.MaxDistance = ImGuiHelper.SliderFloat($"Max Distance##{index}", gtao.MaxDistance, 10.0f, 200.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Maximum distance for GTAO effect\nFades out progressively to optimize performance");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Temporal Filtering");
            ImGui.Spacing();

            // Enable temporal filtering
            bool enableTemporal = gtao.EnableTemporal;
            if (ImGui.Checkbox($"Enable Temporal##{index}", ref enableTemporal))
                gtao.EnableTemporal = enableTemporal;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable temporal accumulation to reduce noise\nMassively improves quality by reusing previous frames\nRecommended: ON");
            }

            // Only show temporal parameters if enabled
            if (gtao.EnableTemporal)
            {
                // Blend factor
                gtao.TemporalBlendFactor = ImGuiHelper.SliderFloat($"Blend Factor##{index}", gtao.TemporalBlendFactor, 0.7f, 0.98f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Weight of history vs current frame\nHigher = smoother but more ghosting\nLower = sharper but more noise\nRecommended: 0.9");
                }

                // Variance threshold
                gtao.TemporalVarianceThreshold = ImGuiHelper.SliderFloat($"Variance Threshold##{index}", gtao.TemporalVarianceThreshold, 0.05f, 0.3f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Rejection threshold for high variance areas\nLower = more temporal stability\nHigher = less ghosting on moving objects\nRecommended: 0.15");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Multi-Scale (Hierarchical)");
            ImGui.Spacing();

            // Mip levels
            int mipLevels = gtao.MipLevels;
            if (ImGui.SliderInt($"Mip Levels##{index}", ref mipLevels, 1, 4))
                gtao.MipLevels = mipLevels;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of mip levels to sample\n1 = single scale (default)\n2 = dual scale (detail + large)\n3-4 = multi-scale for complex scenes\nMore levels = better quality but slower");
            }

            // Preset buttons for common configurations
            ImGui.Spacing();
            if (ImGui.Button($"Single Scale##{index}"))
            {
                gtao.MipLevels = 1;
                gtao.MipWeight0 = 1.0f;
                gtao.MipWeight1 = 0.0f;
                gtao.MipWeight2 = 0.0f;
                gtao.MipWeight3 = 0.0f;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Dual Scale##{index}"))
            {
                gtao.MipLevels = 2;
                gtao.MipWeight0 = 0.6f;
                gtao.MipWeight1 = 0.4f;
                gtao.MipWeight2 = 0.0f;
                gtao.MipWeight3 = 0.0f;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Multi-Scale##{index}"))
            {
                gtao.MipLevels = 3;
                gtao.MipWeight0 = 0.5f;
                gtao.MipWeight1 = 0.3f;
                gtao.MipWeight2 = 0.2f;
                gtao.MipWeight3 = 0.0f;
            }

            // Show weights and radii for active mip levels
            if (gtao.MipLevels > 1)
            {
                ImGui.Spacing();
                ImGui.Text("Per-Level Settings:");
                
                for (int mip = 0; mip < gtao.MipLevels && mip < 4; mip++)
                {
                    ImGui.PushID(mip);
                    ImGui.Spacing();
                    ImGui.Text($"  Mip {mip} ({(1 << mip)}x downsampled):");
                    
                    // Weight
                    float weight = mip == 0 ? gtao.MipWeight0 : mip == 1 ? gtao.MipWeight1 : mip == 2 ? gtao.MipWeight2 : gtao.MipWeight3;
                    if (ImGui.SliderFloat($"Weight##{index}_{mip}", ref weight, 0.0f, 1.0f))
                    {
                        if (mip == 0) gtao.MipWeight0 = weight;
                        else if (mip == 1) gtao.MipWeight1 = weight;
                        else if (mip == 2) gtao.MipWeight2 = weight;
                        else if (mip == 3) gtao.MipWeight3 = weight;
                    }
                    
                    // Radius multiplier
                    float radius = mip == 0 ? gtao.MipRadius0 : mip == 1 ? gtao.MipRadius1 : mip == 2 ? gtao.MipRadius2 : gtao.MipRadius3;
                    if (ImGui.SliderFloat($"Radius Scale##{index}_{mip}", ref radius, 0.5f, 16.0f))
                    {
                        if (mip == 0) gtao.MipRadius0 = radius;
                        else if (mip == 1) gtao.MipRadius1 = radius;
                        else if (mip == 2) gtao.MipRadius2 = radius;
                        else if (mip == 3) gtao.MipRadius3 = radius;
                    }
                    
                    ImGui.PopID();
                }
                
                // Display total weight
                float totalWeight = 0.0f;
                for (int mip = 0; mip < gtao.MipLevels && mip < 4; mip++)
                {
                    totalWeight += mip == 0 ? gtao.MipWeight0 : mip == 1 ? gtao.MipWeight1 : mip == 2 ? gtao.MipWeight2 : gtao.MipWeight3;
                }
                ImGui.Spacing();
                ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), $"Total Weight: {totalWeight:F2} (should be ~1.0)");
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