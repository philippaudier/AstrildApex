using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Engine.Components;
using Engine.Rendering;
using System.Numerics;
using Editor.Themes;
using Editor.UI;

namespace Editor.Inspector
{
    public static class GlobalEffectsInspector
    {
        private static UITheme UI => ThemeManager.UI;

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

                if (ImGui.MenuItem("Depth of Field") && !globalEffects.HasEffect<DepthOfFieldEffect>())
                {
                    globalEffects.AddEffect<DepthOfFieldEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Motion Blur") && !globalEffects.HasEffect<MotionBlurEffect>())
                {
                    globalEffects.AddEffect<MotionBlurEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Image Sharpening") && !globalEffects.HasEffect<ImageSharpeningEffect>())
                {
                    globalEffects.AddEffect<ImageSharpeningEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Volumetric Fog") && !globalEffects.HasEffect<VolumetricFogEffect>())
                {
                    globalEffects.AddEffect<VolumetricFogEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Color Grading") && !globalEffects.HasEffect<ColorGradingEffect>())
                {
                    globalEffects.AddEffect<ColorGradingEffect>();
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.MenuItem("Underwater Volume") && !globalEffects.HasEffect<UnderwaterEffect>())
                {
                    globalEffects.AddEffect<UnderwaterEffect>();
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
            bool nodeOpen = ThemedImGui.TreeNodeEx($"{effect.EffectName}##{index}", flags);

            // Bouton pour supprimer l'effet - placer aligné à droite du contenu
            // Calculer la position X dans la zone de contenu et placer le bouton
            float cursorX = ImGui.GetCursorPosX();
            var avail = ImGui.GetContentRegionAvail();
            ImGui.SetCursorPosX(cursorX + avail.X - 30f);
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
                else if (effect is DepthOfFieldEffect)
                    globalEffects.RemoveEffect<DepthOfFieldEffect>();
                else if (effect is MotionBlurEffect)
                    globalEffects.RemoveEffect<MotionBlurEffect>();
                else if (effect is ImageSharpeningEffect)
                    globalEffects.RemoveEffect<ImageSharpeningEffect>();
                else if (effect is VolumetricFogEffect)
                    globalEffects.RemoveEffect<VolumetricFogEffect>();
                else if (effect is ColorGradingEffect)
                    globalEffects.RemoveEffect<ColorGradingEffect>();
                else if (effect is UnderwaterEffect)
                    globalEffects.RemoveEffect<UnderwaterEffect>();
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
                else if (effect is DepthOfFieldEffect dof)
                {
                    DrawDOFInspector(dof, index);
                }
                else if (effect is MotionBlurEffect motionBlur)
                {
                    DrawMotionBlurInspector(motionBlur, index);
                }
                else if (effect is ImageSharpeningEffect sharpening)
                {
                    DrawImageSharpeningInspector(sharpening, index);
                }
                else if (effect is VolumetricFogEffect volumetricFog)
                {
                    DrawVolumetricFogInspector(volumetricFog, index);
                }
                else if (effect is ColorGradingEffect colorGrading)
                {
                    DrawColorGradingInspector(colorGrading, index);
                }
                else if (effect is UnderwaterEffect underwater)
                {
                    DrawUnderwaterInspector(underwater, index);
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

            // Auto-Exposure toggle and parameters
            var auto = toneMap.AutoExposure;
            if (ImGui.Checkbox($"Auto-Exposure##{index}", ref auto))
                toneMap.AutoExposure = auto;

            if (toneMap.AutoExposure)
            {
                toneMap.MinExposure = ImGuiHelper.SliderFloat($"Min Exposure##{index}", toneMap.MinExposure, 0.01f, 1.0f);
                toneMap.MaxExposure = ImGuiHelper.SliderFloat($"Max Exposure##{index}", toneMap.MaxExposure, 1.0f, 8.0f);
                toneMap.AdaptationSpeed = ImGuiHelper.SliderFloat($"Adaptation Speed##{index}", toneMap.AdaptationSpeed, 0.1f, 10.0f);
                toneMap.TargetBrightness = ImGuiHelper.SliderFloat($"Target Brightness##{index}", toneMap.TargetBrightness, 0.01f, 1.0f);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Exposure Compensation (EV)");
                toneMap.ExposureCompensation = ImGuiHelper.SliderFloat($"Compensation##{index}", toneMap.ExposureCompensation, -3.0f, 3.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Manual bias for auto-exposure\n-1 EV = half as bright (darker)\n+1 EV = twice as bright (brighter)\n\nUse this to override auto-exposure without disabling it");
                }
            }
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
            // === QUALITY PRESET (Main Control) ===
            ImGui.Text("Quality Preset");
            ImGui.SameLine();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Like AAA games: Low/Medium/High/Ultra presets\nCustom = manual control of all parameters");
            }

            var currentQuality = gtao.Quality;
            string[] qualityNames = { "Low", "Medium", "High", "Ultra", "Custom" };
            int currentQualityIndex = (int)currentQuality;

            ImGui.SetNextItemWidth(200);
            if (ImGui.Combo($"##Quality##{index}", ref currentQualityIndex, qualityNames, qualityNames.Length))
            {
                var newQuality = (GTAOQuality)currentQualityIndex;
                gtao.ApplyPreset(newQuality);
            }

            // Show preset info
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            switch (gtao.Quality)
            {
                case GTAOQuality.Low:
                    ImGui.Text("Performance mode: 4 samples, no temporal");
                    break;
                case GTAOQuality.Medium:
                    ImGui.Text("Balanced mode: 6 samples, temporal ON");
                    break;
                case GTAOQuality.High:
                    ImGui.Text("Quality mode: 12 samples, dual-scale, temporal ON");
                    break;
                case GTAOQuality.Ultra:
                    ImGui.Text("Maximum quality: 20 samples, multi-scale, temporal ON");
                    break;
                case GTAOQuality.Custom:
                    ImGui.Text("Custom: User-defined parameters");
                    break;
            }
            ImGui.PopStyleColor();
            ImGui.Spacing();

            // === ADVANCED SETTINGS (Collapsible) ===
            if (gtao.Quality == GTAOQuality.Custom || ImGui.CollapsingHeader($"Advanced Settings##{index}"))
            {
                ImGui.Indent();
                ImGui.Spacing();

                // Auto-switch to Custom when user tweaks parameters
                bool switchedToCustom = false;

                // Core parameters
                ImGui.Text("Core Parameters");
                ImGui.Spacing();

                float oldRadius = gtao.Radius;
                gtao.Radius = ImGuiHelper.SliderFloat($"Radius##{index}", gtao.Radius, 0.1f, 2.0f);
                if (gtao.Radius != oldRadius && gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Sampling radius in view space\nSmaller = fine details, Larger = broad occlusion");
                }

                int oldSampleCount = gtao.SampleCount;
                int sampleCount = gtao.SampleCount;
                if (ImGui.SliderInt($"Samples per Slice##{index}", ref sampleCount, 2, 6))
                {
                    gtao.SampleCount = sampleCount;
                    if (gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Samples per direction\nMore = better quality, slower");
                }

                int oldSliceCount = gtao.SliceCount;
                int sliceCount = gtao.SliceCount;
                if (ImGui.SliderInt($"Slice Count##{index}", ref sliceCount, 1, 4))
                {
                    gtao.SliceCount = sliceCount;
                    if (gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Number of directions\nTotal samples = {gtao.SampleCount * gtao.SliceCount}");
                }

                int oldBlurRadius = gtao.BlurRadius;
                int blurRadius = gtao.BlurRadius;
                if (ImGui.SliderInt($"Blur Radius##{index}", ref blurRadius, 1, 5))
                {
                    gtao.BlurRadius = blurRadius;
                    if (gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Spatial denoising blur\n1-2 = sharp, 3-5 = smooth");
                }

                float oldMaxDistance = gtao.MaxDistance;
                gtao.MaxDistance = ImGuiHelper.SliderFloat($"Max Distance##{index}", gtao.MaxDistance, 20.0f, 200.0f);
                if (gtao.MaxDistance != oldMaxDistance && gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Maximum render distance for GTAO\nFade out beyond this distance\nLower = better performance");
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Temporal Filtering");
                ImGui.Spacing();

                bool oldEnableTemporal = gtao.EnableTemporal;
                bool enableTemporal = gtao.EnableTemporal;
                if (ImGui.Checkbox($"Enable Temporal##{index}", ref enableTemporal))
                {
                    gtao.EnableTemporal = enableTemporal;
                    if (gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Temporal accumulation\nMassively improves quality");
                }

                if (gtao.EnableTemporal)
                {
                    float oldBlendFactor = gtao.TemporalBlendFactor;
                    gtao.TemporalBlendFactor = ImGuiHelper.SliderFloat($"Blend Factor##{index}", gtao.TemporalBlendFactor, 0.7f, 0.98f);
                    if (gtao.TemporalBlendFactor != oldBlendFactor && gtao.Quality != GTAOQuality.Custom) switchedToCustom = true;
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("History weight\nHigher = smoother, more ghosting\nLower = sharper, more noise");
                    }
                }

                // Switch to Custom if user modified parameters
                if (switchedToCustom)
                {
                    gtao.Quality = GTAOQuality.Custom;
                }

                ImGui.Unindent();
                ImGui.Spacing();
            }
        }

        // Remove old multi-scale section helper code
        private static void DrawOldGTAOMultiScaleSection(GTAOEffect gtao, int index)
        {
            // Old code removed - multi-scale now controlled by presets
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

        private static void DrawDOFInspector(DepthOfFieldEffect dof, int index)
        {
            ImGui.Text("Focus Parameters");
            ImGui.Separator();
            ImGui.Spacing();

            // Focus distance
            dof.FocusDistance = ImGuiHelper.SliderFloat($"Focus Distance##{index}", dof.FocusDistance, 0.1f, 100.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Distance to the focus plane\nObjects at this distance are perfectly sharp");
            }

            // Focus range
            dof.FocusRange = ImGuiHelper.SliderFloat($"Focus Range##{index}", dof.FocusRange, 0.0f, 10.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Range around focus distance that stays sharp\n0 = only exact distance sharp, higher = larger sharp area");
            }

            // Focal length
            dof.FocalLength = ImGuiHelper.SliderFloat($"Focal Length (mm)##{index}", dof.FocalLength, 10.0f, 200.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Camera focal length in millimeters\n24mm = wide angle, 50mm = standard, 85mm = portrait, 200mm = telephoto");
            }

            // Aperture (f-stop)
            dof.Aperture = ImGuiHelper.SliderFloat($"Aperture (f-stop)##{index}", dof.Aperture, 1.4f, 22.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Camera aperture (f-stop)\nLower = more blur (f/1.4, f/2.8)\nHigher = less blur (f/11, f/22)");
            }

            // Max CoC
            dof.MaxCoC = ImGuiHelper.SliderFloat($"Max Blur Radius##{index}", dof.MaxCoC, 1.0f, 50.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Maximum circle of confusion radius in pixels\nControls maximum blur amount");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Quality Settings");
            ImGui.Spacing();

            // Sample count
            int sampleCount = dof.SampleCount;
            if (ImGui.SliderInt($"Sample Count##{index}", ref sampleCount, 16, 128))
                dof.SampleCount = sampleCount;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of bokeh samples\n32 = fast, 64 = balanced, 128 = ultra quality");
            }

            // Bokeh radius
            dof.BokehRadius = ImGuiHelper.SliderFloat($"Bokeh Size##{index}", dof.BokehRadius, 1.0f, 10.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Bokeh size multiplier\nControls the size of out-of-focus highlights");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Adaptive DOF (Auto-Focus)");
            ImGui.Spacing();

            // Enable adaptive DOF checkbox
            bool enableAdaptive = dof.EnableAdaptiveDOF;
            if (ImGui.Checkbox($"Enable Adaptive DOF##{index}", ref enableAdaptive))
                dof.EnableAdaptiveDOF = enableAdaptive;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable automatic focus adjustment\nFocus will adapt to the scene depth\n\nWARNING: Uses GL.ReadPixels which can reduce FPS significantly!");
            }

            // Performance warning if enabled
            if (dof.EnableAdaptiveDOF)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.5f, 0.0f, 1.0f)); // Orange
                ImGui.TextWrapped("\u26a0 PERFORMANCE WARNING: Adaptive DOF uses GPU readback which can reduce FPS by 50-70%!");
                ImGui.PopStyleColor();
            }

            // Show adaptive parameters only when enabled
            if (dof.EnableAdaptiveDOF)
            {
                ImGui.Spacing();
                ImGui.Indent();

                // Adaptation speed
                dof.AdaptiveSpeed = ImGuiHelper.SliderFloat($"Adaptation Speed##{index}", dof.AdaptiveSpeed, 0.5f, 10.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("How fast the focus adapts to changes\n1.0 = slow and smooth\n5.0 = fast and responsive");
                }

                // Center bias
                dof.AdaptiveCenterBias = ImGuiHelper.SliderFloat($"Center Bias##{index}", dof.AdaptiveCenterBias, 0.0f, 1.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("How much to focus on screen center\n0.0 = average full screen depth\n1.0 = only center of screen");
                }

                // Min distance
                dof.AdaptiveMinDistance = ImGuiHelper.SliderFloat($"Min Distance##{index}", dof.AdaptiveMinDistance, 0.1f, 50.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Minimum focus distance\nPrevents focusing too close");
                }

                // Max distance
                dof.AdaptiveMaxDistance = ImGuiHelper.SliderFloat($"Max Distance##{index}", dof.AdaptiveMaxDistance, 10.0f, 500.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Maximum focus distance\nPrevents focusing too far");
                }

                ImGui.Unindent();
            }

            // Add preset buttons for common camera setups
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Presets");
            ImGui.Spacing();

            if (ImGui.Button($"Portrait (85mm f/1.8)##{index}", new Vector2(-1, 0)))
            {
                dof.FocalLength = 85.0f;
                dof.Aperture = 1.8f;
                dof.FocusRange = 1.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Portrait photography setup\nShallow depth of field, soft background");
            }

            if (ImGui.Button($"Cinematic (50mm f/2.8)##{index}", new Vector2(-1, 0)))
            {
                dof.FocalLength = 50.0f;
                dof.Aperture = 2.8f;
                dof.FocusRange = 2.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Cinematic look\nBalanced depth of field");
            }

            if (ImGui.Button($"Landscape (24mm f/11)##{index}", new Vector2(-1, 0)))
            {
                dof.FocalLength = 24.0f;
                dof.Aperture = 11.0f;
                dof.FocusRange = 10.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Landscape photography\nDeep depth of field, everything sharp");
            }

            if (ImGui.Button($"Macro (100mm f/2.8)##{index}", new Vector2(-1, 0)))
            {
                dof.FocalLength = 100.0f;
                dof.Aperture = 2.8f;
                dof.FocusRange = 0.5f;
                dof.FocusDistance = 2.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Macro photography\nVery shallow depth of field, close focus");
            }
        }

        private static void DrawMotionBlurInspector(MotionBlurEffect motionBlur, int index)
        {
            ImGui.Text("Motion Blur Settings");
            ImGui.Separator();
            ImGui.Spacing();

            // Sample count
            int sampleCount = motionBlur.SampleCount;
            if (ImGui.SliderInt($"Sample Count##{index}", ref sampleCount, 4, 32))
                motionBlur.SampleCount = sampleCount;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of samples along motion vector\n4 = fast/subtle, 16 = balanced, 32 = ultra quality/strong blur");
            }

            // Max blur radius
            motionBlur.MaxBlurRadius = ImGuiHelper.SliderFloat($"Max Blur Radius##{index}", motionBlur.MaxBlurRadius, 10.0f, 100.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Maximum blur radius in pixels\nLower = subtle motion blur\nHigher = dramatic motion blur");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Info");
            ImGui.Spacing();

            ImGui.TextWrapped("Motion Blur simulates camera and object motion.");
            ImGui.Spacing();
            ImGui.TextWrapped("Works best with camera movement. For moving objects, a velocity buffer is needed.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Presets");
            ImGui.Spacing();

            if (ImGui.Button($"Subtle (8 samples)##{index}", new Vector2(-1, 0)))
            {
                motionBlur.SampleCount = 8;
                motionBlur.MaxBlurRadius = 20.0f;
                motionBlur.Intensity = 0.5f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Subtle motion blur\nGood for first-person games");
            }

            if (ImGui.Button($"Cinematic (16 samples)##{index}", new Vector2(-1, 0)))
            {
                motionBlur.SampleCount = 16;
                motionBlur.MaxBlurRadius = 40.0f;
                motionBlur.Intensity = 1.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Cinematic motion blur\nBalanced quality and performance");
            }

            if (ImGui.Button($"Extreme (32 samples)##{index}", new Vector2(-1, 0)))
            {
                motionBlur.SampleCount = 32;
                motionBlur.MaxBlurRadius = 80.0f;
                motionBlur.Intensity = 1.5f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Extreme motion blur\nVery strong effect, high quality");
            }
        }

        private static void DrawImageSharpeningInspector(ImageSharpeningEffect sharpening, int index)
        {
            ImGui.Text("Image Sharpening Settings");
            ImGui.Separator();
            ImGui.Spacing();

            // Sharpness strength
            sharpening.Sharpness = ImGuiHelper.SliderFloat($"Sharpness##{index}", sharpening.Sharpness, 0.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Sharpening strength\n0.0 = no sharpening\n0.5 = balanced (recommended)\n1.0 = maximum sharpening");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Info");
            ImGui.Spacing();

            ImGui.TextWrapped("AMD FidelityFX Contrast Adaptive Sharpening (CAS)");
            ImGui.Spacing();
            ImGui.TextWrapped("Edge-aware sharpening that prevents over-sharpening and halos. High contrast areas (edges) get less sharpening to prevent artifacts, while low contrast areas get more sharpening for clarity.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Presets");
            ImGui.Spacing();

            if (ImGui.Button($"Subtle (0.3)##{index}", new Vector2(-1, 0)))
            {
                sharpening.Sharpness = 0.3f;
                sharpening.Intensity = 1.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Subtle sharpening\nGood for general use");
            }

            if (ImGui.Button($"Balanced (0.5)##{index}", new Vector2(-1, 0)))
            {
                sharpening.Sharpness = 0.5f;
                sharpening.Intensity = 1.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Balanced sharpening (recommended)\nNoticeable improvement without artifacts");
            }

            if (ImGui.Button($"Strong (0.8)##{index}", new Vector2(-1, 0)))
            {
                sharpening.Sharpness = 0.8f;
                sharpening.Intensity = 1.0f;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Strong sharpening\nVery crisp image, use carefully");
            }
        }

        private static void DrawVolumetricFogInspector(VolumetricFogEffect fog, int index)
        {
            ImGui.Text("Volumetric Fog (Ray Marching)");
            ImGui.Separator();
            ImGui.Spacing();

            // Source mode
            var sources = Enum.GetNames(typeof(VolumetricFogEffect.FogSource));
            int currentSource = (int)fog.Source;
            if (ImGui.Combo($"Source##{index}", ref currentSource, sources, sources.Length))
            {
                fog.Source = (VolumetricFogEffect.FogSource)currentSource;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Local = manual parameters\nGlobal = WeatherComponent fog\nBlend = mix between both");
            }

            // Blend factor (only for Blend mode)
            if (fog.Source == VolumetricFogEffect.FogSource.Blend)
            {
                fog.BlendFactor = ImGuiHelper.SliderFloat($"Blend Factor##{index}", fog.BlendFactor, 0.0f, 1.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("0 = Local parameters, 1 = Global (Weather)");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Fog Parameters");
            ImGui.Spacing();

            // Color
            var fogColor = new System.Numerics.Vector3(fog.FogColor.X, fog.FogColor.Y, fog.FogColor.Z);
            if (ImGui.ColorEdit3($"Color##{index}", ref fogColor))
            {
                fog.FogColor = new OpenTK.Mathematics.Vector3(fogColor.X, fogColor.Y, fogColor.Z);
            }

            // Density
            fog.Density = ImGuiHelper.SliderFloat($"Density##{index}", fog.Density, 0.0f, 0.2f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Base fog density\n0 = no fog, 0.02 = light fog, 0.1 = heavy fog");
            }

            // Depth range
            fog.DepthStart = ImGuiHelper.SliderFloat($"Start Distance##{index}", fog.DepthStart, 0.0f, 100.0f);
            fog.DepthEnd = ImGuiHelper.SliderFloat($"End Distance##{index}", fog.DepthEnd, 10.0f, 2000.0f);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Ray Marching");
            ImGui.Spacing();

            int steps = fog.RayMarchSteps;
            if (ImGui.SliderInt($"Steps##{index}", ref steps, 8, 64))
                fog.RayMarchSteps = steps;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of ray march steps\nHigher = better quality but slower\n32 is a good balance");
            }

            fog.MaxRayDistance = ImGuiHelper.SliderFloat($"Max Ray Distance##{index}", fog.MaxRayDistance, 50.0f, 1000.0f);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Height-Based Fog");
            ImGui.Spacing();

            bool useHeight = fog.UseHeightFog;
            if (ImGui.Checkbox($"Enable Height Fog##{index}", ref useHeight))
                fog.UseHeightFog = useHeight;

            if (fog.UseHeightFog)
            {
                ImGui.Indent();
                fog.HeightFalloff = ImGuiHelper.SliderFloat($"Falloff##{index}", fog.HeightFalloff, 0.0f, 0.5f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("How quickly fog density decreases with height");
                }
                fog.BaseHeight = ImGuiHelper.SliderFloat($"Base Height##{index}", fog.BaseHeight, -100.0f, 100.0f);
                fog.MaxHeight = ImGuiHelper.SliderFloat($"Max Height##{index}", fog.MaxHeight, fog.BaseHeight + 10.0f, 500.0f);
                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Light Scattering (Physically Based)");
            ImGui.Spacing();

            fog.ExtinctionFactor = ImGuiHelper.SliderFloat($"Extinction##{index}", fog.ExtinctionFactor, 0.1f, 5.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Light absorption coefficient (Beer-Lambert)\nHigher = fog absorbs more light (darker)");
            }

            fog.AmbientIntensity = ImGuiHelper.SliderFloat($"Ambient##{index}", fog.AmbientIntensity, 0.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Ambient light in fog (fills shadows)\n0 = pure black shadows\n0.3 = natural look");
            }

            ImGui.Spacing();

            bool useSunScatter = fog.UseSunScattering;
            if (ImGui.Checkbox($"Enable Sun Scattering##{index}", ref useSunScatter))
                fog.UseSunScattering = useSunScatter;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable directional light scattering\nCreates light shafts where sun penetrates fog");
            }

            if (fog.UseSunScattering)
            {
                ImGui.Indent();

                fog.ScatteringIntensity = ImGuiHelper.SliderFloat($"Sun Intensity##{index}", fog.ScatteringIntensity, 0.0f, 10.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Sun light intensity for scattering");
                }

                fog.MieG = ImGuiHelper.SliderFloat($"Mie G##{index}", fog.MieG, 0.0f, 0.99f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Henyey-Greenstein anisotropy\n0.7-0.95 = strong forward scatter (sun halo)\n0.5 = moderate scatter");
                }

                var sunColor = new System.Numerics.Vector3(fog.SunScatteringColor.X, fog.SunScatteringColor.Y, fog.SunScatteringColor.Z);
                if (ImGui.ColorEdit3($"Sun Color##{index}", ref sunColor))
                {
                    fog.SunScatteringColor = new OpenTK.Mathematics.Vector3(sunColor.X, sunColor.Y, sunColor.Z);
                }

                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("God Rays (Light Shafts)");
            ImGui.Spacing();

            fog.GodRaysIntensity = ImGuiHelper.SliderFloat($"God Rays Intensity##{index}", fog.GodRaysIntensity, 0.0f, 5.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Intensity of light shafts\nObjects naturally block the light");
            }

            fog.GodRaysDensity = ImGuiHelper.SliderFloat($"God Rays Density##{index}", fog.GodRaysDensity, 0.3f, 2.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Radial blur spread\nHigher = longer rays");
            }

            fog.GodRaysDecay = ImGuiHelper.SliderFloat($"God Rays Decay##{index}", fog.GodRaysDecay, 0.9f, 0.995f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Decay per sample\nHigher = rays extend further");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("3D Noise");
            ImGui.Spacing();

            bool useNoise = fog.UseNoise;
            if (ImGui.Checkbox($"Enable Noise##{index}", ref useNoise))
                fog.UseNoise = useNoise;
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Add animated 3D Simplex noise for realistic fog variation");
            }

            if (fog.UseNoise)
            {
                ImGui.Indent();
                fog.NoiseScale = ImGuiHelper.SliderFloat($"Scale##{index}", fog.NoiseScale, 0.001f, 0.2f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Noise scale (smaller = larger clouds)");
                }
                fog.NoiseSpeed = ImGuiHelper.SliderFloat($"Speed##{index}", fog.NoiseSpeed, 0.0f, 1.0f);
                fog.NoiseStrength = ImGuiHelper.SliderFloat($"Strength##{index}", fog.NoiseStrength, 0.0f, 1.0f);

                int octaves = fog.NoiseOctaves;
                if (ImGui.SliderInt($"Octaves##{index}", ref octaves, 1, 6))
                    fog.NoiseOctaves = octaves;
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("FBM noise octaves\nHigher = more detail but slower");
                }
                ImGui.Unindent();
            }
        }

        private static void DrawColorGradingInspector(ColorGradingEffect colorGrading, int index)
        {
            ImGui.Text("Color Grading Settings");
            ImGui.Separator();
            ImGui.Spacing();

            // Source mode
            var sources = Enum.GetNames(typeof(ColorGradingEffect.ColorGradingSource));
            int currentSource = (int)colorGrading.Source;
            if (ImGui.Combo($"Source##{index}", ref currentSource, sources, sources.Length))
            {
                colorGrading.Source = (ColorGradingEffect.ColorGradingSource)currentSource;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Manual = fixed parameters\nTimeOfDay = automatic time-based color grading\nBlend = mix between both");
            }

            // Blend factor (only for Blend mode)
            if (colorGrading.Source == ColorGradingEffect.ColorGradingSource.Blend)
            {
                colorGrading.BlendFactor = ImGuiHelper.SliderFloat($"Blend Factor##{index}", colorGrading.BlendFactor, 0.0f, 1.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("0 = Manual parameters, 1 = Time of Day");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Basic Adjustments");
            ImGui.Spacing();

            // Basic parameters
            colorGrading.Saturation = ImGuiHelper.SliderFloat($"Saturation##{index}", colorGrading.Saturation, 0.0f, 2.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("0 = grayscale, 1 = normal, 2 = vibrant");
            }

            colorGrading.Contrast = ImGuiHelper.SliderFloat($"Contrast##{index}", colorGrading.Contrast, 0.0f, 2.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("0 = flat, 1 = normal, 2 = high contrast");
            }

            colorGrading.Brightness = ImGuiHelper.SliderFloat($"Brightness##{index}", colorGrading.Brightness, -1.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("-1 = darker, 0 = normal, +1 = brighter");
            }

            colorGrading.Vibrance = ImGuiHelper.SliderFloat($"Vibrance##{index}", colorGrading.Vibrance, -1.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Selective saturation boost for dull colors");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("White Balance");
            ImGui.Spacing();

            colorGrading.Temperature = ImGuiHelper.SliderFloat($"Temperature##{index}", colorGrading.Temperature, -1.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("-1 = cool (blue), 0 = neutral, +1 = warm (orange)");
            }

            colorGrading.Tint = ImGuiHelper.SliderFloat($"Tint##{index}", colorGrading.Tint, -1.0f, 1.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("-1 = green, 0 = neutral, +1 = magenta");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Advanced");
            ImGui.Spacing();

            colorGrading.HueShift = ImGuiHelper.SliderFloat($"Hue Shift##{index}", colorGrading.HueShift, 0.0f, 360.0f);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Rotate hue by degrees (0-360)");
            }

            var colorFilter = new System.Numerics.Vector3(colorGrading.ColorFilter.X, colorGrading.ColorFilter.Y, colorGrading.ColorFilter.Z);
            if (ImGui.ColorEdit3($"Color Filter##{index}", ref colorFilter))
            {
                colorGrading.ColorFilter = new OpenTK.Mathematics.Vector3(colorFilter.X, colorFilter.Y, colorFilter.Z);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("RGB multiplier (tint)");
            }

            // Show time-of-day settings if relevant
            if (colorGrading.Source != ColorGradingEffect.ColorGradingSource.Manual)
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Text("Time of Day Presets");
                ImGui.Spacing();

                if (ImGui.CollapsingHeader($"Night##{index}"))
                {
                    ImGui.Indent();
                    colorGrading.NightSaturation = ImGuiHelper.SliderFloat($"Saturation##{index}_night", colorGrading.NightSaturation, 0.0f, 2.0f);
                    colorGrading.NightContrast = ImGuiHelper.SliderFloat($"Contrast##{index}_night", colorGrading.NightContrast, 0.0f, 2.0f);
                    colorGrading.NightTemperature = ImGuiHelper.SliderFloat($"Temperature##{index}_night", colorGrading.NightTemperature, -1.0f, 1.0f);
                    ImGui.Unindent();
                }

                if (ImGui.CollapsingHeader($"Day##{index}"))
                {
                    ImGui.Indent();
                    colorGrading.DaySaturation = ImGuiHelper.SliderFloat($"Saturation##{index}_day", colorGrading.DaySaturation, 0.0f, 2.0f);
                    colorGrading.DayContrast = ImGuiHelper.SliderFloat($"Contrast##{index}_day", colorGrading.DayContrast, 0.0f, 2.0f);
                    colorGrading.DayTemperature = ImGuiHelper.SliderFloat($"Temperature##{index}_day", colorGrading.DayTemperature, -1.0f, 1.0f);
                    ImGui.Unindent();
                }

                if (ImGui.CollapsingHeader($"Sunrise##{index}"))
                {
                    ImGui.Indent();
                    colorGrading.SunriseSaturation = ImGuiHelper.SliderFloat($"Saturation##{index}_sunrise", colorGrading.SunriseSaturation, 0.0f, 2.0f);
                    colorGrading.SunriseContrast = ImGuiHelper.SliderFloat($"Contrast##{index}_sunrise", colorGrading.SunriseContrast, 0.0f, 2.0f);
                    colorGrading.SunriseTemperature = ImGuiHelper.SliderFloat($"Temperature##{index}_sunrise", colorGrading.SunriseTemperature, -1.0f, 1.0f);
                    ImGui.Unindent();
                }

                if (ImGui.CollapsingHeader($"Sunset##{index}"))
                {
                    ImGui.Indent();
                    colorGrading.SunsetSaturation = ImGuiHelper.SliderFloat($"Saturation##{index}_sunset", colorGrading.SunsetSaturation, 0.0f, 2.0f);
                    colorGrading.SunsetContrast = ImGuiHelper.SliderFloat($"Contrast##{index}_sunset", colorGrading.SunsetContrast, 0.0f, 2.0f);
                    colorGrading.SunsetTemperature = ImGuiHelper.SliderFloat($"Temperature##{index}_sunset", colorGrading.SunsetTemperature, -1.0f, 1.0f);
                    ImGui.Unindent();
                }

                ImGui.Spacing();
                colorGrading.TransitionSpeed = ImGuiHelper.SliderFloat($"Transition Speed##{index}", colorGrading.TransitionSpeed, 0.1f, 10.0f);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("How fast to blend between time-of-day presets");
                }
            }
        }

        private static void DrawUnderwaterInspector(UnderwaterEffect uw, int index)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 1.0f, 1.0f), "Subnautica-style Underwater Volume");
            ImGui.Spacing();

            // Water Level Source
            var sources = Enum.GetNames(typeof(UnderwaterEffect.WaterLevelSource));
            int currentSource = (int)uw.Source;
            if (ImGui.Combo($"Water Level Source##{index}", ref currentSource, sources, sources.Length))
            {
                uw.Source = (UnderwaterEffect.WaterLevelSource)currentSource;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Auto = detect from WaterPlaneComponent in scene\nManual = use custom value");

            if (uw.Source == UnderwaterEffect.WaterLevelSource.Auto)
            {
                // Show detected water level (read-only)
                ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f),
                    $"Detected Water Level: {uw.DetectedWaterLevel:F2}m");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Automatically detected from WaterPlaneComponent position");
            }
            else
            {
                // Manual water level input
                uw.WaterLevel = ImGuiHelper.SliderFloat($"Water Level##{index}", uw.WaterLevel, -100.0f, 100.0f);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Y position of water surface - effect applies when camera is below this level");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Underwater Fog");
            ImGui.Spacing();

            var fogEnabled = uw.FogEnabled;
            if (ImGui.Checkbox($"Enable Fog##{index}", ref fogEnabled))
                uw.FogEnabled = fogEnabled;

            if (uw.FogEnabled)
            {
                var fogColor = new Vector3(uw.FogColor.X, uw.FogColor.Y, uw.FogColor.Z);
                if (ImGui.ColorEdit3($"Fog Color##{index}", ref fogColor))
                    uw.FogColor = new OpenTK.Mathematics.Vector3(fogColor.X, fogColor.Y, fogColor.Z);

                uw.FogDensity = ImGuiHelper.SliderFloat($"Fog Density##{index}", uw.FogDensity, 0.001f, 0.1f);
                uw.Visibility = ImGuiHelper.SliderFloat($"Visibility##{index}", uw.Visibility, 5.0f, 200.0f);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Maximum visibility distance in meters");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Light Absorption (Beer-Lambert)");
            ImGui.Spacing();

            var absorptionEnabled = uw.AbsorptionEnabled;
            if (ImGui.Checkbox($"Enable Absorption##{index}", ref absorptionEnabled))
                uw.AbsorptionEnabled = absorptionEnabled;

            if (uw.AbsorptionEnabled)
            {
                ImGui.TextDisabled("Red absorbs first, blue penetrates deepest");
                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.5f, 0.1f, 0.1f, 0.5f));
                uw.AbsorptionR = ImGuiHelper.SliderFloat($"Red Absorption##{index}", uw.AbsorptionR, 0.0f, 1.0f);
                ImGui.PopStyleColor();

                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.1f, 0.5f, 0.1f, 0.5f));
                uw.AbsorptionG = ImGuiHelper.SliderFloat($"Green Absorption##{index}", uw.AbsorptionG, 0.0f, 0.5f);
                ImGui.PopStyleColor();

                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.1f, 0.1f, 0.5f, 0.5f));
                uw.AbsorptionB = ImGuiHelper.SliderFloat($"Blue Absorption##{index}", uw.AbsorptionB, 0.0f, 0.2f);
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("God Rays (Volumetric Light)");
            ImGui.Spacing();

            var godRaysEnabled = uw.GodRaysEnabled;
            if (ImGui.Checkbox($"Enable God Rays##{index}", ref godRaysEnabled))
                uw.GodRaysEnabled = godRaysEnabled;

            if (uw.GodRaysEnabled)
            {
                uw.GodRaysIntensity = ImGuiHelper.SliderFloat($"Intensity##GR{index}", uw.GodRaysIntensity, 0.0f, 5.0f);
                ImGui.SetItemTooltip("Overall brightness of god rays. Higher values make rays more visible.");

                var godRaysColor = new Vector3(uw.GodRaysColor.X, uw.GodRaysColor.Y, uw.GodRaysColor.Z);
                if (ImGui.ColorEdit3($"Ray Color##{index}", ref godRaysColor))
                    uw.GodRaysColor = new OpenTK.Mathematics.Vector3(godRaysColor.X, godRaysColor.Y, godRaysColor.Z);
                ImGui.SetItemTooltip("Color of the light shafts. Warm colors (yellow/orange) look like sunlight.");

                uw.GodRaysDensity = ImGuiHelper.SliderFloat($"Shaft Density##{index}", uw.GodRaysDensity, 0.1f, 3.0f);
                ImGui.SetItemTooltip("Density/frequency of light shafts. Higher = more detailed shafts.");

                uw.GodRaysDecay = ImGuiHelper.SliderFloat($"Transmittance##{index}", uw.GodRaysDecay, 0.9f, 0.995f);
                ImGui.SetItemTooltip("How far rays travel through water. Higher = rays reach deeper.");

                int samples = uw.GodRaysSamples;
                if (ImGui.SliderInt($"Samples##GR{index}", ref samples, 16, 64))
                    uw.GodRaysSamples = samples;
                ImGui.SetItemTooltip("Ray marching quality. Higher = better quality but more GPU cost.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Volumetric Floating Particles");
            ImGui.Spacing();

            var particlesEnabled = uw.ParticlesEnabled;
            if (ImGui.Checkbox($"Enable Particles##{index}", ref particlesEnabled))
                uw.ParticlesEnabled = particlesEnabled;

            if (uw.ParticlesEnabled)
            {
                // Basic settings
                uw.ParticleDensity = ImGuiHelper.SliderFloat($"Density##{index}part", uw.ParticleDensity, 0.1f, 2.0f);
                ImGui.SetItemTooltip("Particle density. Higher = more particles.");

                var particleColor = new Vector3(uw.ParticleColor.X, uw.ParticleColor.Y, uw.ParticleColor.Z);
                if (ImGui.ColorEdit3($"Color##{index}part", ref particleColor))
                    uw.ParticleColor = new OpenTK.Mathematics.Vector3(particleColor.X, particleColor.Y, particleColor.Z);

                uw.ParticleBrightness = ImGuiHelper.SliderFloat($"Brightness##{index}part", uw.ParticleBrightness, 0.0f, 1.0f);
                ImGui.SetItemTooltip("Overall particle brightness.");

                uw.ParticleSpeed = ImGuiHelper.SliderFloat($"Drift Speed##{index}part", uw.ParticleSpeed, 0.0f, 1.0f);
                ImGui.SetItemTooltip("How fast particles drift upward.");

                ImGui.Spacing();
                ImGui.Text("Size & Depth");

                uw.ParticleSizeMin = ImGuiHelper.SliderFloat($"Min Size##{index}part", uw.ParticleSizeMin, 0.1f, 2.0f);
                ImGui.SetItemTooltip("Minimum particle size.");

                uw.ParticleSizeMax = ImGuiHelper.SliderFloat($"Max Size##{index}part", uw.ParticleSizeMax, 1.0f, 10.0f);
                ImGui.SetItemTooltip("Maximum particle size.");

                var depthLayers = uw.ParticleDepthLayers;
                if (ImGui.SliderInt($"Depth Layers##{index}part", ref depthLayers, 1, 8))
                    uw.ParticleDepthLayers = depthLayers;
                ImGui.SetItemTooltip("Number of depth layers. More = better volumetric look, higher GPU cost.");

                ImGui.Spacing();
                ImGui.Text("Lighting & Scattering");

                uw.ParticleLighting = ImGuiHelper.SliderFloat($"Light Reaction##{index}part", uw.ParticleLighting, 0.0f, 1.0f);
                ImGui.SetItemTooltip("How much particles react to sunlight (0 = ambient only, 1 = full lighting).");

                uw.ParticleScattering = ImGuiHelper.SliderFloat($"Forward Scatter##{index}part", uw.ParticleScattering, 0.0f, 1.5f);
                ImGui.SetItemTooltip("Glow when looking towards the sun (subsurface scattering effect).");

                uw.ParticleGodRayGlow = ImGuiHelper.SliderFloat($"God Ray Glow##{index}part", uw.ParticleGodRayGlow, 0.0f, 1.5f);
                ImGui.SetItemTooltip("Extra brightness when particle is in a light shaft.");

                ImGui.Spacing();
                ImGui.Text("Movement");

                uw.ParticleTurbulence = ImGuiHelper.SliderFloat($"Turbulence##{index}part", uw.ParticleTurbulence, 0.0f, 1.0f);
                ImGui.SetItemTooltip("Random wobbling movement intensity.");

                ImGui.Spacing();
                ImGui.Text("Depth of Field");

                uw.ParticleFocusDistance = ImGuiHelper.SliderFloat($"Focus Distance##{index}part", uw.ParticleFocusDistance, 1.0f, 50.0f);
                ImGui.SetItemTooltip("Distance where particles are sharpest.");

                uw.ParticleFocusRange = ImGuiHelper.SliderFloat($"Focus Range##{index}part", uw.ParticleFocusRange, 5.0f, 100.0f);
                ImGui.SetItemTooltip("Range of sharp focus. Particles outside blur (bokeh effect).");

                ImGui.Spacing();
                ImGui.Text("Distance Fade");

                uw.ParticleNearFade = ImGuiHelper.SliderFloat($"Near Fade##{index}part", uw.ParticleNearFade, 0.5f, 10.0f);
                ImGui.SetItemTooltip("Distance at which particles fade when too close to camera.");

                uw.ParticleFarFade = ImGuiHelper.SliderFloat($"Far Fade##{index}part", uw.ParticleFarFade, 20.0f, 200.0f);
                ImGui.SetItemTooltip("Distance at which particles fade when too far from camera.");

                ImGui.Spacing();
                ImGui.Text("Particle Texture");

                var newParticleTexture = EditorWidgets.AssetField(
                    $"Texture##{index}partTex",
                    uw.ParticleTextureGuid,
                    "Texture",
                    "Optional texture for particles (uses procedural if empty)",
                    showPreview: true);

                if (newParticleTexture != uw.ParticleTextureGuid)
                {
                    uw.ParticleTextureGuid = newParticleTexture;
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Caustics (GPU Gems + Chromatic Aberration)");
            ImGui.Spacing();

            var causticsEnabled = uw.CausticsEnabled;
            if (ImGui.Checkbox($"Enable Caustics##{index}", ref causticsEnabled))
                uw.CausticsEnabled = causticsEnabled;

            if (uw.CausticsEnabled)
            {
                uw.CausticsIntensity = ImGuiHelper.SliderFloat($"Intensity##{index}caust", uw.CausticsIntensity, 0.0f, 2.0f);
                uw.CausticsBrightness = ImGuiHelper.SliderFloat($"Brightness##{index}caust", uw.CausticsBrightness, 0.0f, 3.0f);
                uw.CausticsScale = ImGuiHelper.SliderFloat($"Scale##{index}caust", uw.CausticsScale, 0.1f, 5.0f);
                uw.CausticsSpeed = ImGuiHelper.SliderFloat($"Speed##{index}caust", uw.CausticsSpeed, 0.1f, 3.0f);

                ImGui.Spacing();
                ImGui.Text("Detail & Shape");

                var octaves = uw.CausticsOctaves;
                if (ImGui.SliderInt($"Octaves##{index}caust", ref octaves, 1, 6))
                    uw.CausticsOctaves = octaves;
                ImGui.SetItemTooltip("Detail layers (1-6). More octaves = finer detail but higher GPU cost.");

                uw.CausticsSharpness = ImGuiHelper.SliderFloat($"Sharpness##{index}caust", uw.CausticsSharpness, 1.0f, 10.0f);
                ImGui.SetItemTooltip("How focused the light rays are. Higher = sharper caustic lines.");

                uw.CausticsDistortion = ImGuiHelper.SliderFloat($"Distortion##{index}caust", uw.CausticsDistortion, 0.0f, 2.0f);
                ImGui.SetItemTooltip("Refraction distortion from water surface waves.");

                ImGui.Spacing();
                ImGui.Text("Attenuation & Color");

                uw.CausticsDepthFalloff = ImGuiHelper.SliderFloat($"Depth Falloff##{index}caust", uw.CausticsDepthFalloff, 0.0f, 1.0f);
                ImGui.SetItemTooltip("How fast caustics fade with depth below surface.");

                uw.CausticsChromatic = ImGuiHelper.SliderFloat($"Chromatic Aberration##{index}caust", uw.CausticsChromatic, 0.0f, 0.2f);
                ImGui.SetItemTooltip("RGB color separation. Simulates light dispersion through water.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Tint & Ambient");
            ImGui.Spacing();

            var tintColor = new Vector3(uw.TintColor.X, uw.TintColor.Y, uw.TintColor.Z);
            if (ImGui.ColorEdit3($"Tint Color##{index}", ref tintColor))
                uw.TintColor = new OpenTK.Mathematics.Vector3(tintColor.X, tintColor.Y, tintColor.Z);

            uw.AmbientIntensity = ImGuiHelper.SliderFloat($"Ambient Intensity##{index}", uw.AmbientIntensity, 0.0f, 0.5f);

            var ambientColor = new Vector3(uw.AmbientColor.X, uw.AmbientColor.Y, uw.AmbientColor.Z);
            if (ImGui.ColorEdit3($"Ambient Color##{index}", ref ambientColor))
                uw.AmbientColor = new OpenTK.Mathematics.Vector3(ambientColor.X, ambientColor.Y, ambientColor.Z);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Screen Distortion");
            ImGui.Spacing();

            var distortionEnabled = uw.DistortionEnabled;
            if (ImGui.Checkbox($"Enable Distortion##{index}", ref distortionEnabled))
                uw.DistortionEnabled = distortionEnabled;

            if (uw.DistortionEnabled)
            {
                uw.DistortionIntensity = ImGuiHelper.SliderFloat($"Intensity##{index}dist", uw.DistortionIntensity, 0.0f, 0.1f);
                ImGui.SetItemTooltip("Overall distortion strength. Subtle values (0.01-0.03) work best.");

                uw.DistortionSpeed = ImGuiHelper.SliderFloat($"Speed##{index}dist", uw.DistortionSpeed, 0.1f, 3.0f);
                ImGui.SetItemTooltip("Animation speed of the distortion effect.");

                uw.DistortionScale = ImGuiHelper.SliderFloat($"Scale##{index}dist", uw.DistortionScale, 0.1f, 5.0f);
                ImGui.SetItemTooltip("Scale of the noise pattern used for distortion.");

                ImGui.Spacing();
                ImGui.Text("Sources");

                var useWaves = uw.DistortionUseWaves;
                if (ImGui.Checkbox($"Use Gerstner Waves##{index}dist", ref useWaves))
                    uw.DistortionUseWaves = useWaves;
                ImGui.SetItemTooltip("Use actual water surface waves for distortion (matches visible waves).");

                uw.DistortionWaveInfluence = ImGuiHelper.SliderFloat($"Wave Influence##{index}dist", uw.DistortionWaveInfluence, 0.0f, 2.0f);
                ImGui.SetItemTooltip("How much the Gerstner waves affect the distortion.");

                uw.DistortionNoiseInfluence = ImGuiHelper.SliderFloat($"Noise Influence##{index}dist", uw.DistortionNoiseInfluence, 0.0f, 2.0f);
                ImGui.SetItemTooltip("How much procedural noise affects the distortion (adds fine detail).");

                ImGui.Spacing();
                ImGui.Text("Effects");

                uw.DistortionChromatic = ImGuiHelper.SliderFloat($"Chromatic Aberration##{index}dist", uw.DistortionChromatic, 0.0f, 0.02f);
                ImGui.SetItemTooltip("RGB color separation in distorted areas. Creates a refraction-like effect.");

                uw.DistortionDepthFade = ImGuiHelper.SliderFloat($"Depth Fade##{index}dist", uw.DistortionDepthFade, 0.0f, 0.1f);
                ImGui.SetItemTooltip("Reduce distortion with depth (more distortion near surface).");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Snell's Window (Total Internal Reflection)");
            ImGui.Spacing();

            var snellEnabled = uw.SnellWindowEnabled;
            if (ImGui.Checkbox($"Enable Snell's Window##{index}", ref snellEnabled))
                uw.SnellWindowEnabled = snellEnabled;
            ImGui.SetItemTooltip("When looking up from underwater, light can only enter through a ~97° cone (Snell's window). Outside this cone, you see reflections of the underwater world.");

            if (uw.SnellWindowEnabled)
            {
                uw.SnellCriticalAngle = ImGuiHelper.SliderFloat($"Critical Angle##{index}snell", uw.SnellCriticalAngle, 30.0f, 60.0f);
                ImGui.SetItemTooltip("Critical angle in degrees. Water/air interface is 48.6°. Lower = smaller window.");

                uw.SnellEdgeSoftness = ImGuiHelper.SliderFloat($"Edge Softness##{index}snell", uw.SnellEdgeSoftness, 0.0f, 0.5f);
                ImGui.SetItemTooltip("Softness of the window edge. 0 = hard edge, higher = gradual transition.");

                ImGui.Spacing();
                ImGui.Text("Reflection");

                var reflectionTint = new Vector3(uw.SnellReflectionTint.X, uw.SnellReflectionTint.Y, uw.SnellReflectionTint.Z);
                if (ImGui.ColorEdit3($"Reflection Tint##{index}snell", ref reflectionTint))
                    uw.SnellReflectionTint = new OpenTK.Mathematics.Vector3(reflectionTint.X, reflectionTint.Y, reflectionTint.Z);
                ImGui.SetItemTooltip("Color tint applied to the reflected underwater scene.");

                uw.SnellReflectionStrength = ImGuiHelper.SliderFloat($"Reflection Strength##{index}snell", uw.SnellReflectionStrength, 0.0f, 1.0f);
                ImGui.SetItemTooltip("How bright the underwater reflection appears outside the window.");

                uw.SnellFresnelPower = ImGuiHelper.SliderFloat($"Fresnel Power##{index}snell", uw.SnellFresnelPower, 1.0f, 10.0f);
                ImGui.SetItemTooltip("Controls the brightness at the window edge (Fresnel effect). Higher = brighter edge.");

                uw.SnellWaveDistortion = ImGuiHelper.SliderFloat($"Wave Distortion##{index}snell", uw.SnellWaveDistortion, 0.0f, 1.0f);
                ImGui.SetItemTooltip("How much waves distort the Snell's window edge. 0 = perfect circle, 1 = fully wave-distorted.");
            }

            // === WATER TRANSITION EFFECTS ===
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Water Transition Effects");
            ImGui.Spacing();

            var transitionEnabled = uw.TransitionEnabled;
            if (ImGui.Checkbox($"Enable Transition Effects##{index}trans", ref transitionEnabled))
                uw.TransitionEnabled = transitionEnabled;
            ImGui.SetItemTooltip("FBM distortion effect when entering/exiting water.");

            if (uw.TransitionEnabled)
            {
                uw.TransitionDuration = ImGuiHelper.SliderFloat($"Duration##{index}trans", uw.TransitionDuration, 0.5f, 5.0f);
                ImGui.SetItemTooltip("How long the transition effect lasts (seconds).");

                // Show current transition state (debug info)
                if (uw.TransitionProgress > 0.0f)
                {
                    ImGui.TextColored(UI.Success, $"Active: {(uw.IsEnteringWater ? "Entering" : "Exiting")} - {uw.TransitionProgress * 100:F0}%");
                }

                ImGui.Spacing();
                ImGui.Text("FBM Distortion Settings");

                uw.ExitDropletIntensity = ImGuiHelper.SliderFloat($"Intensity##{index}fbm", uw.ExitDropletIntensity, 0.0f, 3.0f);
                ImGui.SetItemTooltip("Strength of the FBM screen distortion.");

                uw.ExitDropletSize = ImGuiHelper.SliderFloat($"Scale##{index}fbm", uw.ExitDropletSize, 0.5f, 3.0f);
                ImGui.SetItemTooltip("Scale of the distortion pattern. Higher = larger warping.");

                uw.ExitDripSpeed = ImGuiHelper.SliderFloat($"Animation Speed##{index}fbm", uw.ExitDripSpeed, 0.1f, 3.0f);
                ImGui.SetItemTooltip("Speed of the distortion animation.");
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