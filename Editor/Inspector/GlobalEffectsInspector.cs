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