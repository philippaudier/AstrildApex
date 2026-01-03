using System;
using ImGuiNET;
using Engine.Assets;
using Editor.UI;
using Numerics = System.Numerics;

namespace Editor.Inspector
{
    public static class WaterForwardInspector
    {
        public static void DrawWaterForwardProperties(MaterialAsset mat)
        {
            if (mat.WaterProperties == null)
            {
                mat.WaterProperties = new WaterProperties();
            }

            var water = mat.WaterProperties;
            bool changed = false;

            ImGui.Separator();
            ImGui.Text("🌊 WaterForward Shader Properties");
            ImGui.Spacing();

            // === PRESETS ===
            if (ThemedImGui.CollapsingHeader("Presets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Quick presets for common water types");
                ImGui.Spacing();

                if (ImGui.Button("Clear Water (Lake)"))
                {
                    mat.WaterProperties = WaterProperties.CreateClearWater();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Ocean"))
                {
                    mat.WaterProperties = WaterProperties.CreateOcean();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Lake"))
                {
                    mat.WaterProperties = WaterProperties.CreateLake();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }

                if (ImGui.Button("Stylized Water"))
                {
                    mat.WaterProperties = WaterProperties.CreateStylizedWater();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
            }

            // === PHASE 1: BASE COLOR ===
            if (ThemedImGui.CollapsingHeader("Phase 1: Base Color", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Water Color (shallow)
                var waterColor = new Numerics.Vector4(
                    water.WaterColor[0],
                    water.WaterColor[1],
                    water.WaterColor[2],
                    water.WaterColor[3]
                );
                if (ImGui.ColorEdit4("Water Color (Shallow)", ref waterColor))
                {
                    water.WaterColor = new float[] { waterColor.X, waterColor.Y, waterColor.Z, waterColor.W };
                    changed = true;
                }
                ImGui.TextDisabled("Color of water in shallow areas");

                // Deep Water Color
                var deepWaterColor = new Numerics.Vector4(
                    water.DeepWaterColor[0],
                    water.DeepWaterColor[1],
                    water.DeepWaterColor[2],
                    water.DeepWaterColor[3]
                );
                if (ImGui.ColorEdit4("Deep Water Color", ref deepWaterColor))
                {
                    water.DeepWaterColor = new float[] { deepWaterColor.X, deepWaterColor.Y, deepWaterColor.Z, deepWaterColor.W };
                    changed = true;
                }
                ImGui.TextDisabled("Color of water in deep areas");

                // Transparency
                float transparency = water.Transparency;
                if (ImGui.SliderFloat("Transparency", ref transparency, 0.0f, 1.0f))
                {
                    water.Transparency = transparency;
                    changed = true;
                }
                ImGui.TextDisabled("Overall water transparency (0 = opaque, 1 = fully transparent)");
            }

            // === PHASE 1 & 6: WAVE ANIMATION ===
            if (ThemedImGui.CollapsingHeader("Phase 1 & 6: Wave Animation"))
            {
                // Wave Speed
                float waveSpeed = water.WaveSpeed;
                if (ImGui.DragFloat("Wave Speed", ref waveSpeed, 0.05f, 0.0f, 10.0f))
                {
                    water.WaveSpeed = waveSpeed;
                    changed = true;
                }
                ImGui.TextDisabled("Speed of wave animation");

                // Wave Amplitude (Vertex Displacement)
                float waveAmplitude = water.WaveAmplitude;
                if (ImGui.DragFloat("Wave Amplitude", ref waveAmplitude, 0.01f, 0.0f, 2.0f))
                {
                    water.WaveAmplitude = waveAmplitude;
                    changed = true;
                }
                ImGui.TextDisabled("Height of waves (Phase 6: vertex displacement, 0 = disabled)");

                // Wave Frequency
                float waveFrequency = water.WaveFrequency;
                if (ImGui.DragFloat("Wave Frequency", ref waveFrequency, 0.1f, 0.1f, 10.0f))
                {
                    water.WaveFrequency = waveFrequency;
                    changed = true;
                }
                ImGui.TextDisabled("Frequency of waves (how many waves)");

                // Wave Direction
                var waveDir = new Numerics.Vector2(water.WaveDirection[0], water.WaveDirection[1]);
                if (ImGui.DragFloat2("Wave Direction", ref waveDir, 0.01f, -1.0f, 1.0f))
                {
                    water.WaveDirection = new float[] { waveDir.X, waveDir.Y };
                    changed = true;
                }
                ImGui.TextDisabled("Direction of wave movement (normalized XZ)");
            }

            // === PHASE 2: NORMAL MAPPING ===
            if (ThemedImGui.CollapsingHeader("Phase 2: Normal Mapping (Two Layers)"))
            {
                // Normal Texture 1
                var newNormal = EditorWidgets.AssetField("Normal Map Layer 1", mat.NormalTexture, "Texture", "Assign Normal Map 1", showPreview: true);
                if (newNormal != mat.NormalTexture)
                {
                    mat.NormalTexture = newNormal;
                    changed = true;
                }

                // Normal Strength 1
                float normalStrength = water.NormalStrength;
                if (ImGui.SliderFloat("Normal Strength 1", ref normalStrength, 0.0f, 3.0f))
                {
                    water.NormalStrength = normalStrength;
                    changed = true;
                }

                // Normal Layer 1 Scale (Tiling)
                float normalLayer1Scale = water.NormalLayer1Scale;
                if (ImGui.DragFloat("Layer 1 Tiling Scale", ref normalLayer1Scale, 0.05f, 0.01f, float.MaxValue))
                {
                    water.NormalLayer1Scale = normalLayer1Scale;
                    changed = true;
                }
                ImGui.TextDisabled("Tiling scale for first normal map layer (any value)");

                // Normal Layer 1 Speed
                float normalLayer1Speed = water.NormalLayer1Speed;
                if (ImGui.DragFloat("Layer 1 Speed", ref normalLayer1Speed, 0.001f, -1.0f, 1.0f))
                {
                    water.NormalLayer1Speed = normalLayer1Speed;
                    changed = true;
                }

                // Normal Layer 1 Direction
                var normalLayer1Dir = new Numerics.Vector2(water.NormalLayer1Direction[0], water.NormalLayer1Direction[1]);
                if (ImGui.DragFloat2("Layer 1 Direction", ref normalLayer1Dir, 0.01f, -1.0f, 1.0f))
                {
                    water.NormalLayer1Direction = new float[] { normalLayer1Dir.X, normalLayer1Dir.Y };
                    changed = true;
                }

                ImGui.Separator();

                // Normal Texture 2 (using MetallicTexture slot for second normal)
                var newNormal2 = EditorWidgets.AssetField("Normal Map Layer 2", mat.MetallicTexture, "Texture", "Assign Normal Map 2", showPreview: true);
                if (newNormal2 != mat.MetallicTexture)
                {
                    mat.MetallicTexture = newNormal2;
                    changed = true;
                }

                // Normal Strength 2
                float normalStrength2 = water.NormalStrength2;
                if (ImGui.SliderFloat("Normal Strength 2", ref normalStrength2, 0.0f, 3.0f))
                {
                    water.NormalStrength2 = normalStrength2;
                    changed = true;
                }

                // Normal Layer 2 Scale (Tiling)
                float normalLayer2Scale = water.NormalLayer2Scale;
                if (ImGui.DragFloat("Layer 2 Tiling Scale", ref normalLayer2Scale, 0.05f, 0.01f, float.MaxValue))
                {
                    water.NormalLayer2Scale = normalLayer2Scale;
                    changed = true;
                }
                ImGui.TextDisabled("Tiling scale for second normal map layer (any value)");

                // Normal Layer 2 Speed
                float normalLayer2Speed = water.NormalLayer2Speed;
                if (ImGui.DragFloat("Layer 2 Speed", ref normalLayer2Speed, 0.001f, -1.0f, 1.0f))
                {
                    water.NormalLayer2Speed = normalLayer2Speed;
                    changed = true;
                }

                // Normal Layer 2 Direction
                var normalLayer2Dir = new Numerics.Vector2(water.NormalLayer2Direction[0], water.NormalLayer2Direction[1]);
                if (ImGui.DragFloat2("Layer 2 Direction", ref normalLayer2Dir, 0.01f, -1.0f, 1.0f))
                {
                    water.NormalLayer2Direction = new float[] { normalLayer2Dir.X, normalLayer2Dir.Y };
                    changed = true;
                }

                ImGui.Separator();

                // Normal Blend
                float normalBlend = water.NormalBlend;
                if (ImGui.SliderFloat("Normal Blend", ref normalBlend, 0.0f, 1.0f))
                {
                    water.NormalBlend = normalBlend;
                    changed = true;
                }
                ImGui.TextDisabled("Blend between layer 1 (0.0) and layer 2 (1.0)");
            }

            // === PHASE 2: DEPTH & REFRACTION ===
            if (ThemedImGui.CollapsingHeader("Phase 2: Depth & Refraction"))
            {
                // Depth Fade Distance
                float depthFadeDistance = water.DepthFadeDistance;
                if (ImGui.DragFloat("Depth Fade Distance", ref depthFadeDistance, 0.1f, 0.0f, 50.0f))
                {
                    water.DepthFadeDistance = depthFadeDistance;
                    changed = true;
                }
                ImGui.TextDisabled("Distance (meters) over which water fades to opaque");

                // Use Refraction
                bool useRefraction = water.UseRefraction;
                if (ImGui.Checkbox("Enable Refraction", ref useRefraction))
                {
                    water.UseRefraction = useRefraction;
                    changed = true;
                }
                ImGui.TextDisabled("Enable screen-space refraction (requires scene color texture)");

                if (water.UseRefraction)
                {
                    // Refraction Strength
                    float refractionStrength = water.RefractionStrength;
                    if (ImGui.SliderFloat("Refraction Strength", ref refractionStrength, 0.0f, 1.0f))
                    {
                        water.RefractionStrength = refractionStrength;
                        changed = true;
                    }
                    ImGui.TextDisabled("Strength of refraction distortion");
                }
            }

            // === PHASE 3: PBR & LIGHTING ===
            if (ThemedImGui.CollapsingHeader("Phase 3: PBR & Lighting (IBL)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Roughness
                float roughness = water.Roughness;
                if (ImGui.SliderFloat("Roughness", ref roughness, 0.0f, 1.0f))
                {
                    water.Roughness = roughness;
                    changed = true;
                }
                ImGui.TextDisabled("Surface roughness (0 = mirror reflection, 1 = diffuse)");

                // Metallic
                float metallic = water.Metallic;
                if (ImGui.SliderFloat("Metallic", ref metallic, 0.0f, 1.0f))
                {
                    water.Metallic = metallic;
                    changed = true;
                }
                ImGui.TextDisabled("Metallic property (usually 0 for water)");

                // Fresnel
                float fresnel = water.Fresnel;
                if (ImGui.SliderFloat("Fresnel Strength", ref fresnel, 0.0f, 5.0f))
                {
                    water.Fresnel = fresnel;
                    changed = true;
                }
                ImGui.TextDisabled("Fresnel effect strength (reflection at grazing angles)");

                // Specular Strength
                float specularStrength = water.SpecularStrength;
                if (ImGui.SliderFloat("Specular Strength", ref specularStrength, 0.0f, 5.0f))
                {
                    water.SpecularStrength = specularStrength;
                    changed = true;
                }
                ImGui.TextDisabled("Strength of direct specular highlights from sun");

                ImGui.TextColored(new Numerics.Vector4(0.4f, 0.8f, 1.0f, 1.0f), "✓ IBL (Image-Based Lighting) is automatically used");
                ImGui.TextColored(new Numerics.Vector4(0.4f, 0.8f, 1.0f, 1.0f), "✓ Irradiance for diffuse + Prefiltered environment for specular");
            }

            // === PHASE 4: COLOR ABSORPTION ===
            if (ThemedImGui.CollapsingHeader("Phase 4: Color Absorption"))
            {
                // Absorption Color
                var absorptionColor = new Numerics.Vector3(
                    water.AbsorptionColor[0],
                    water.AbsorptionColor[1],
                    water.AbsorptionColor[2]
                );
                if (ImGui.ColorEdit3("Absorption Color", ref absorptionColor))
                {
                    water.AbsorptionColor = new float[] { absorptionColor.X, absorptionColor.Y, absorptionColor.Z };
                    changed = true;
                }
                ImGui.TextDisabled("Color that gets absorbed with depth (RGB)");

                // Absorption Strength
                float absorptionStrength = water.AbsorptionStrength;
                if (ImGui.DragFloat("Absorption Strength", ref absorptionStrength, 0.01f, 0.0f, 1.0f))
                {
                    water.AbsorptionStrength = absorptionStrength;
                    changed = true;
                }
                ImGui.TextDisabled("How strongly color is absorbed with depth");
            }

            // === PHASE 5: FOAM & EDGE EFFECTS ===
            if (ThemedImGui.CollapsingHeader("Phase 5: Foam & Edge Effects"))
            {
                // Foam Texture (using EmissiveTexture slot)
                var newFoam = EditorWidgets.AssetField("Foam Texture", mat.EmissiveTexture, "Texture", "Assign Foam Texture", showPreview: true);
                if (newFoam != mat.EmissiveTexture)
                {
                    mat.EmissiveTexture = newFoam;
                    changed = true;
                }

                // Foam Amount
                float foamAmount = water.FoamAmount;
                if (ImGui.SliderFloat("Foam Amount", ref foamAmount, 0.0f, 2.0f))
                {
                    water.FoamAmount = foamAmount;
                    changed = true;
                }
                ImGui.TextDisabled("Amount of foam at edges (0 = no foam)");

                // Foam Cutoff
                float foamCutoff = water.FoamCutoff;
                if (ImGui.DragFloat("Foam Cutoff", ref foamCutoff, 0.05f, 0.0f, 5.0f))
                {
                    water.FoamCutoff = foamCutoff;
                    changed = true;
                }
                ImGui.TextDisabled("Depth threshold (meters) for foam appearance");

                // Foam Color
                var foamColor = new Numerics.Vector4(
                    water.FoamColor[0],
                    water.FoamColor[1],
                    water.FoamColor[2],
                    water.FoamColor[3]
                );
                if (ImGui.ColorEdit4("Foam Color", ref foamColor))
                {
                    water.FoamColor = new float[] { foamColor.X, foamColor.Y, foamColor.Z, foamColor.W };
                    changed = true;
                }
                ImGui.TextDisabled("Color of foam (RGBA)");

                // Foam Texture Scale
                float foamTextureScale = water.FoamTextureScale;
                if (ImGui.DragFloat("Foam Texture Scale", ref foamTextureScale, 0.1f, 0.1f, 20.0f))
                {
                    water.FoamTextureScale = foamTextureScale;
                    changed = true;
                }
                ImGui.TextDisabled("Tiling scale for foam texture");

                // Foam Alpha Clip Threshold
                float foamAlphaClipThreshold = water.FoamAlphaClipThreshold;
                if (ImGui.SliderFloat("Foam Alpha Clip Threshold", ref foamAlphaClipThreshold, 0.0f, 1.0f))
                {
                    water.FoamAlphaClipThreshold = foamAlphaClipThreshold;
                    changed = true;
                }
                ImGui.TextDisabled("Alpha threshold for foam texture clipping (higher = less foam)");

                // Edge Fade Distance
                float edgeFadeDistance = water.EdgeFadeDistance;
                if (ImGui.DragFloat("Edge Fade Distance", ref edgeFadeDistance, 0.05f, 0.0f, 5.0f))
                {
                    water.EdgeFadeDistance = edgeFadeDistance;
                    changed = true;
                }
                ImGui.TextDisabled("Distance for edge blending");
            }

            // === PHASE 6: AAA FEATURES ===
            if (ThemedImGui.CollapsingHeader("Phase 6: AAA Features (Caustics & Reflections)"))
            {
                ImGui.TextDisabled("Advanced water effects for AAA quality");
                ImGui.Spacing();

                // === CAUSTICS ===
                ImGui.SeparatorText("Caustics");

                bool useCaustics = water.UseCaustics;
                if (ImGui.Checkbox("Enable Caustics", ref useCaustics))
                {
                    water.UseCaustics = useCaustics;
                    changed = true;
                }
                ImGui.TextDisabled("Procedural caustics pattern (light refraction through water)");

                if (water.UseCaustics)
                {
                    // Caustics Strength
                    float causticsStrength = water.CausticsStrength;
                    if (ImGui.SliderFloat("Caustics Strength", ref causticsStrength, 0.0f, 3.0f))
                    {
                        water.CausticsStrength = causticsStrength;
                        changed = true;
                    }
                    ImGui.TextDisabled("Intensity of caustics pattern");

                    // Caustics Scale
                    float causticsScale = water.CausticsScale;
                    if (ImGui.DragFloat("Caustics Scale", ref causticsScale, 0.1f, 0.1f, 10.0f))
                    {
                        water.CausticsScale = causticsScale;
                        changed = true;
                    }
                    ImGui.TextDisabled("Tiling scale for caustics pattern (higher = smaller patterns)");

                    // Caustics Speed
                    float causticsSpeed = water.CausticsSpeed;
                    if (ImGui.DragFloat("Caustics Speed", ref causticsSpeed, 0.05f, 0.0f, 5.0f))
                    {
                        water.CausticsSpeed = causticsSpeed;
                        changed = true;
                    }
                    ImGui.TextDisabled("Animation speed of caustics pattern");

                    // Caustics Color
                    var causticsColor = new Numerics.Vector3(
                        water.CausticsColor[0],
                        water.CausticsColor[1],
                        water.CausticsColor[2]
                    );
                    if (ImGui.ColorEdit3("Caustics Color", ref causticsColor))
                    {
                        water.CausticsColor = new float[] { causticsColor.X, causticsColor.Y, causticsColor.Z };
                        changed = true;
                    }
                    ImGui.TextDisabled("Tint color for caustics (default: warm sunlight)");

                    // Caustics Distortion (Physics-based)
                    float causticsDistortion = water.CausticsDistortion;
                    if (ImGui.SliderFloat("Caustics Distortion", ref causticsDistortion, 0.0f, 2.0f))
                    {
                        water.CausticsDistortion = causticsDistortion;
                        changed = true;
                    }
                    ImGui.TextDisabled("How much water normals affect caustics (0=none, 1=realistic physics)");

                    // Chromatic Aberration
                    float causticsSplit = water.CausticsSplit;
                    if (ImGui.SliderFloat("Chromatic Aberration", ref causticsSplit, 0.0f, 0.1f))
                    {
                        water.CausticsSplit = causticsSplit;
                        changed = true;
                    }
                    ImGui.TextDisabled("RGB split for caustics (simulates light dispersion)");

                    ImGui.TextColored(new Numerics.Vector4(0.4f, 1.0f, 0.4f, 1.0f), "✓ Caustics are now physically-based on water surface normals!");
                }

                ImGui.Separator();

                // === PLANAR REFLECTIONS ===
                ImGui.SeparatorText("Planar Reflections");

                bool usePlanarReflections = water.UsePlanarReflections;
                if (ImGui.Checkbox("Enable Planar Reflections", ref usePlanarReflections))
                {
                    water.UsePlanarReflections = usePlanarReflections;
                    changed = true;
                }
                ImGui.TextDisabled("Screen-space planar reflections (future implementation)");

                if (water.UsePlanarReflections)
                {
                    // Reflection Blur
                    float reflectionBlur = water.ReflectionBlur;
                    if (ImGui.SliderFloat("Reflection Blur", ref reflectionBlur, 0.0f, 1.0f))
                    {
                        water.ReflectionBlur = reflectionBlur;
                        changed = true;
                    }
                    ImGui.TextDisabled("Blur amount for reflections (0 = sharp, 1 = very blurry)");

                    // Reflection Resolution
                    int reflectionResolution = water.ReflectionResolution;
                    string[] resolutionOptions = new[] { "256", "512", "1024", "2048" };
                    int[] resolutionValues = new[] { 256, 512, 1024, 2048 };
                    int currentIndex = Array.IndexOf(resolutionValues, reflectionResolution);
                    if (currentIndex == -1) currentIndex = 2; // Default to 1024

                    if (ImGui.Combo("Reflection Resolution", ref currentIndex, resolutionOptions, resolutionOptions.Length))
                    {
                        water.ReflectionResolution = resolutionValues[currentIndex];
                        changed = true;
                    }
                    ImGui.TextDisabled("Resolution of reflection render texture (higher = sharper but slower)");

                    // Flip Reflection X
                    bool flipX = water.FlipReflectionX;
                    if (ImGui.Checkbox("Flip Reflection X (Horizontal)", ref flipX))
                    {
                        water.FlipReflectionX = flipX;
                        changed = true;
                    }
                    ImGui.TextDisabled("Flip reflection horizontally");

                    // Flip Reflection Y
                    bool flipY = water.FlipReflectionY;
                    if (ImGui.Checkbox("Flip Reflection Y (Vertical)", ref flipY))
                    {
                        water.FlipReflectionY = flipY;
                        changed = true;
                    }
                    ImGui.TextDisabled("Flip reflection vertically");

                    ImGui.Spacing();
                    ImGui.TextColored(new Numerics.Vector4(0.4f, 1.0f, 0.4f, 1.0f), "✓ Planar reflections are now integrated with all water effects!");
                }
            }

            // Auto-save if changed
            if (changed)
            {
                try
                {
                    Engine.Assets.AssetDatabase.SaveMaterialAsync(mat);
                }
                catch { }
            }
        }
    }
}
