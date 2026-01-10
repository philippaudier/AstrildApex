using System;
using ImGuiNET;
using Engine.Components;
using Editor.UI;
using Numerics = System.Numerics;
using OpenTK.Mathematics;

namespace Editor.Inspector
{
    /// <summary>
    /// Inspector for WaterPlaneComponent - realistic ocean water with tessellation
    /// </summary>
    public static class WaterPlaneInspector
    {
        public static bool Draw(WaterPlaneComponent water)
        {
            if (water == null) return false;
            bool changed = false;

            ImGui.Separator();
            ImGui.Text("🌊 Water Plane (Ocean)");
            ImGui.Spacing();

            // === MESH GENERATION ===
            if (ThemedImGui.CollapsingHeader("Mesh Generation", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Resolution
                int resolution = water.Resolution;
                if (ImGui.DragInt("Resolution", ref resolution, 1, 4, 512))
                {
                    water.Resolution = Math.Clamp(resolution, 4, 512);
                    changed = true;
                }
                ImGui.TextDisabled("Grid vertices per side (higher = more detail)");

                // Size
                float size = water.Size;
                if (ImGui.DragFloat("Size", ref size, 1.0f, 1.0f, 10000.0f))
                {
                    water.Size = Math.Max(1.0f, size);
                    changed = true;
                }
                ImGui.TextDisabled("World size of water plane");

                ImGui.Spacing();
                
                // Generate button
                if (ImGui.Button("Generate Water Plane", new Numerics.Vector2(-1, 30)))
                {
                    water.NeedsRegeneration = true;
                    water.GenerateMesh();
                }
                
                if (water.MeshGenerated)
                {
                    ImGui.TextColored(new Numerics.Vector4(0.3f, 0.8f, 0.3f, 1.0f), 
                        $"✓ Mesh generated: {water.VertexCount} vertices, {water.IndexCount / 3} triangles");
                }
                else
                {
                    ImGui.TextColored(new Numerics.Vector4(1.0f, 0.5f, 0.3f, 1.0f), 
                        "⚠ Click 'Generate Water Plane' to create mesh");
                }
            }

            // === TESSELLATION ===
            if (ThemedImGui.CollapsingHeader("Tessellation", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool tessEnabled = water.TessellationEnabled;
                if (ImGui.Checkbox("Enable Tessellation", ref tessEnabled))
                {
                    water.TessellationEnabled = tessEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("GPU tessellation for adaptive detail");

                if (water.TessellationEnabled)
                {
                    float tessFactor = water.TessellationFactor;
                    if (ImGui.SliderFloat("Tessellation Factor", ref tessFactor, 1.0f, 64.0f))
                    {
                        water.TessellationFactor = tessFactor;
                        changed = true;
                    }
                    ImGui.TextDisabled("Base tessellation multiplier");

                    float minDist = water.TessellationMinDistance;
                    if (ImGui.DragFloat("Min Distance", ref minDist, 1.0f, 1.0f, 500.0f))
                    {
                        water.TessellationMinDistance = minDist;
                        changed = true;
                    }
                    ImGui.TextDisabled("Distance for maximum tessellation");

                    float maxDist = water.TessellationMaxDistance;
                    if (ImGui.DragFloat("Max Distance", ref maxDist, 1.0f, 10.0f, 2000.0f))
                    {
                        water.TessellationMaxDistance = maxDist;
                        changed = true;
                    }
                    ImGui.TextDisabled("Distance for minimum tessellation");

                    float minLevel = water.TessellationMinLevel;
                    if (ImGui.SliderFloat("Min Tess Level", ref minLevel, 1.0f, 16.0f))
                    {
                        water.TessellationMinLevel = minLevel;
                        changed = true;
                    }

                    float maxLevel = water.TessellationMaxLevel;
                    if (ImGui.SliderFloat("Max Tess Level", ref maxLevel, 1.0f, 64.0f))
                    {
                        water.TessellationMaxLevel = maxLevel;
                        changed = true;
                    }
                }
            }

            // === LOD ===
            if (ThemedImGui.CollapsingHeader("Level of Detail (LOD)"))
            {
                bool lodEnabled = water.LodEnabled;
                if (ImGui.Checkbox("Enable LOD", ref lodEnabled))
                {
                    water.LodEnabled = lodEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("Distance-based level of detail");

                if (water.LodEnabled)
                {
                    float lod1 = water.LodDistance1;
                    if (ImGui.DragFloat("LOD Distance 1", ref lod1, 1.0f, 10.0f, 500.0f))
                    {
                        water.LodDistance1 = lod1;
                        changed = true;
                    }

                    float lod2 = water.LodDistance2;
                    if (ImGui.DragFloat("LOD Distance 2", ref lod2, 1.0f, 50.0f, 1000.0f))
                    {
                        water.LodDistance2 = lod2;
                        changed = true;
                    }

                    float lod3 = water.LodDistance3;
                    if (ImGui.DragFloat("LOD Distance 3", ref lod3, 1.0f, 100.0f, 2000.0f))
                    {
                        water.LodDistance3 = lod3;
                        changed = true;
                    }
                }
            }

            // === WEATHER INTEGRATION ===
            if (ThemedImGui.CollapsingHeader("Weather Integration", ImGuiTreeNodeFlags.DefaultOpen))
            {
                string[] modes = { "Global (Weather)", "Local", "Blend" };
                int waveMode = water.WaveMode;
                if (ImGui.Combo("Wave Mode", ref waveMode, modes, modes.Length))
                {
                    water.WaveMode = waveMode;
                    changed = true;
                }
                ImGui.TextDisabled("0=Global (from WeatherComponent), 1=Local, 2=Blend");

                if (water.WaveMode == 2) // Blend
                {
                    float blendFactor = water.WaveBlendFactor;
                    if (ImGui.SliderFloat("Blend Factor", ref blendFactor, 0.0f, 1.0f))
                    {
                        water.WaveBlendFactor = blendFactor;
                        changed = true;
                    }
                    ImGui.TextDisabled("0 = Local, 1 = Global");
                }
            }

            // === GERSTNER WAVES ===
            if (ThemedImGui.CollapsingHeader("Gerstner Waves", ImGuiTreeNodeFlags.DefaultOpen))
            {
                int waveIter = water.WaveIterations;
                if (ImGui.SliderInt("Wave Iterations", ref waveIter, 1, 16))
                {
                    water.WaveIterations = waveIter;
                    changed = true;
                }
                ImGui.TextDisabled("Number of wave octaves (quality vs performance)");

                float waveAmp = water.WaveAmplitude;
                if (ImGui.DragFloat("Wave Amplitude", ref waveAmp, 0.01f, 0.0f, 10.0f))
                {
                    water.WaveAmplitude = waveAmp;
                    changed = true;
                }
                ImGui.TextDisabled("Base wave height");

                float waveFreq = water.WaveFrequency;
                if (ImGui.DragFloat("Wave Frequency", ref waveFreq, 0.01f, 0.1f, 10.0f))
                {
                    water.WaveFrequency = waveFreq;
                    changed = true;
                }
                ImGui.TextDisabled("Wave density");

                float waveSpeed = water.WaveSpeed;
                if (ImGui.DragFloat("Wave Speed", ref waveSpeed, 0.1f, 0.0f, 20.0f))
                {
                    water.WaveSpeed = waveSpeed;
                    changed = true;
                }
                ImGui.TextDisabled("Animation speed");

                float waveSteep = water.WaveSteepness;
                if (ImGui.SliderFloat("Wave Steepness", ref waveSteep, 0.0f, 1.0f))
                {
                    water.WaveSteepness = waveSteep;
                    changed = true;
                }
                ImGui.TextDisabled("0 = smooth sine, 1 = sharp peaks");

                float waveDrag = water.WaveDrag;
                if (ImGui.SliderFloat("Wave Drag", ref waveDrag, 0.0f, 1.0f))
                {
                    water.WaveDrag = waveDrag;
                    changed = true;
                }
                ImGui.TextDisabled("How much waves pull on the water");

                float waveDepth = water.WaveDepth;
                if (ImGui.DragFloat("Wave Depth", ref waveDepth, 0.1f, 0.1f, 10.0f))
                {
                    water.WaveDepth = waveDepth;
                    changed = true;
                }
                ImGui.TextDisabled("Water depth affecting wave behavior");

                // Wave Direction
                var waveDir = new Numerics.Vector2(water.WaveDirectionX, water.WaveDirectionZ);
                if (ImGui.DragFloat2("Wave Direction", ref waveDir, 0.01f, -1.0f, 1.0f))
                {
                    water.WaveDirectionX = waveDir.X;
                    water.WaveDirectionZ = waveDir.Y;
                    changed = true;
                }
                ImGui.TextDisabled("Primary wave direction (XZ)");
            }

            // === FBM DETAIL ===
            if (ThemedImGui.CollapsingHeader("FBM Detail Noise"))
            {
                bool fbmEnabled = water.FbmEnabled;
                if (ImGui.Checkbox("Enable FBM", ref fbmEnabled))
                {
                    water.FbmEnabled = fbmEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("Fractal Brownian Motion for additional detail");

                if (water.FbmEnabled)
                {
                    int fbmOctaves = water.FbmOctaves;
                    if (ImGui.SliderInt("FBM Octaves", ref fbmOctaves, 1, 8))
                    {
                        water.FbmOctaves = fbmOctaves;
                        changed = true;
                    }

                    float fbmAmp = water.FbmAmplitude;
                    if (ImGui.DragFloat("FBM Amplitude", ref fbmAmp, 0.01f, 0.0f, 1.0f))
                    {
                        water.FbmAmplitude = fbmAmp;
                        changed = true;
                    }

                    float fbmFreq = water.FbmFrequency;
                    if (ImGui.DragFloat("FBM Frequency", ref fbmFreq, 0.1f, 0.1f, 10.0f))
                    {
                        water.FbmFrequency = fbmFreq;
                        changed = true;
                    }

                    float fbmLac = water.FbmLacunarity;
                    if (ImGui.DragFloat("FBM Lacunarity", ref fbmLac, 0.1f, 1.0f, 4.0f))
                    {
                        water.FbmLacunarity = fbmLac;
                        changed = true;
                    }

                    float fbmPers = water.FbmPersistence;
                    if (ImGui.SliderFloat("FBM Persistence", ref fbmPers, 0.0f, 1.0f))
                    {
                        water.FbmPersistence = fbmPers;
                        changed = true;
                    }
                }
            }

            // === WATER COLORS ===
            if (ThemedImGui.CollapsingHeader("Water Colors", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var shallowColor = new Numerics.Vector4(
                    water.ShallowColor.X, water.ShallowColor.Y,
                    water.ShallowColor.Z, water.ShallowColor.W);
                if (ImGui.ColorEdit4("Shallow Color", ref shallowColor))
                {
                    water.ShallowColor = new Vector4(
                        shallowColor.X, shallowColor.Y, shallowColor.Z, shallowColor.W);
                    changed = true;
                }

                var deepColor = new Numerics.Vector4(
                    water.DeepColor.X, water.DeepColor.Y,
                    water.DeepColor.Z, water.DeepColor.W);
                if (ImGui.ColorEdit4("Deep Color", ref deepColor))
                {
                    water.DeepColor = new Vector4(
                        deepColor.X, deepColor.Y, deepColor.Z, deepColor.W);
                    changed = true;
                }

                var horizonColor = new Numerics.Vector4(
                    water.HorizonColor.X, water.HorizonColor.Y,
                    water.HorizonColor.Z, water.HorizonColor.W);
                if (ImGui.ColorEdit4("Horizon Color", ref horizonColor))
                {
                    water.HorizonColor = new Vector4(
                        horizonColor.X, horizonColor.Y, horizonColor.Z, horizonColor.W);
                    changed = true;
                }

                float colorDepthFade = water.ColorDepthFade;
                if (ImGui.DragFloat("Depth Fade Distance", ref colorDepthFade, 0.1f, 0.1f, 100.0f))
                {
                    water.ColorDepthFade = colorDepthFade;
                    changed = true;
                }
            }

            // === FRESNEL ===
            if (ThemedImGui.CollapsingHeader("Fresnel"))
            {
                float fresnelPower = water.FresnelPower;
                if (ImGui.SliderFloat("Fresnel Power", ref fresnelPower, 1.0f, 10.0f))
                {
                    water.FresnelPower = fresnelPower;
                    changed = true;
                }

                float fresnelBias = water.FresnelBias;
                if (ImGui.SliderFloat("Fresnel Bias", ref fresnelBias, 0.0f, 0.5f))
                {
                    water.FresnelBias = fresnelBias;
                    changed = true;
                }

                float fresnelScale = water.FresnelScale;
                if (ImGui.SliderFloat("Fresnel Scale", ref fresnelScale, 0.0f, 2.0f))
                {
                    water.FresnelScale = fresnelScale;
                    changed = true;
                }
            }

            // === SUBSURFACE SCATTERING ===
            if (ThemedImGui.CollapsingHeader("Subsurface Scattering"))
            {
                bool sssEnabled = water.SSSEnabled;
                if (ImGui.Checkbox("Enable SSS", ref sssEnabled))
                {
                    water.SSSEnabled = sssEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("Light scattering through water");

                if (water.SSSEnabled)
                {
                    var sssColor = new Numerics.Vector3(
                        water.SSSColor.X, water.SSSColor.Y, water.SSSColor.Z);
                    if (ImGui.ColorEdit3("SSS Color", ref sssColor))
                    {
                        water.SSSColor = new Vector3(sssColor.X, sssColor.Y, sssColor.Z);
                        changed = true;
                    }

                    float sssIntensity = water.SSSIntensity;
                    if (ImGui.SliderFloat("SSS Intensity", ref sssIntensity, 0.0f, 2.0f))
                    {
                        water.SSSIntensity = sssIntensity;
                        changed = true;
                    }

                    float sssDistortion = water.SSSDistortion;
                    if (ImGui.SliderFloat("SSS Distortion", ref sssDistortion, 0.0f, 1.0f))
                    {
                        water.SSSDistortion = sssDistortion;
                        changed = true;
                    }

                    float sssPower = water.SSSPower;
                    if (ImGui.SliderFloat("SSS Power", ref sssPower, 0.5f, 5.0f))
                    {
                        water.SSSPower = sssPower;
                        changed = true;
                    }
                }
            }

            // === CREST FOAM ===
            if (ThemedImGui.CollapsingHeader("Crest Foam"))
            {
                bool foamEnabled = water.CrestFoamEnabled;
                if (ImGui.Checkbox("Enable Crest Foam", ref foamEnabled))
                {
                    water.CrestFoamEnabled = foamEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("White foam on wave peaks");

                if (water.CrestFoamEnabled)
                {
                    float foamThreshold = water.CrestFoamThreshold;
                    if (ImGui.SliderFloat("Foam Threshold", ref foamThreshold, 0.0f, 1.0f))
                    {
                        water.CrestFoamThreshold = foamThreshold;
                        changed = true;
                    }

                    float foamIntensity = water.CrestFoamIntensity;
                    if (ImGui.SliderFloat("Foam Intensity", ref foamIntensity, 0.0f, 3.0f))
                    {
                        water.CrestFoamIntensity = foamIntensity;
                        changed = true;
                    }

                    var foamColor = new Numerics.Vector4(
                        water.CrestFoamColor.X, water.CrestFoamColor.Y,
                        water.CrestFoamColor.Z, water.CrestFoamColor.W);
                    if (ImGui.ColorEdit4("Foam Color", ref foamColor))
                    {
                        water.CrestFoamColor = new Vector4(
                            foamColor.X, foamColor.Y, foamColor.Z, foamColor.W);
                        changed = true;
                    }

                    float foamScale = water.CrestFoamScale;
                    if (ImGui.DragFloat("Foam Scale", ref foamScale, 0.1f, 0.1f, 20.0f))
                    {
                        water.CrestFoamScale = foamScale;
                        changed = true;
                    }

                    float foamSpeed = water.CrestFoamSpeed;
                    if (ImGui.DragFloat("Foam Speed", ref foamSpeed, 0.01f, 0.0f, 2.0f))
                    {
                        water.CrestFoamSpeed = foamSpeed;
                        changed = true;
                    }
                    ImGui.TextDisabled("Animation speed for foam texture");

                    var newFoamTexture = EditorWidgets.AssetField(
                        "Foam Texture",
                        water.CrestFoamTextureGuid,
                        "Texture",
                        "Optional foam texture (uses procedural if not set)",
                        showPreview: true);

                    if (newFoamTexture != water.CrestFoamTextureGuid)
                    {
                        water.CrestFoamTextureGuid = newFoamTexture;
                        changed = true;
                    }
                }
            }

            // === SHORE FOAM ===
            if (ThemedImGui.CollapsingHeader("Shore Foam"))
            {
                bool shoreFoamEnabled = water.ShoreFoamEnabled;
                if (ImGui.Checkbox("Enable Shore Foam", ref shoreFoamEnabled))
                {
                    water.ShoreFoamEnabled = shoreFoamEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("Foam in shallow water near shorelines");

                if (water.ShoreFoamEnabled)
                {
                    float shoreFoamDepth = water.ShoreFoamDepth;
                    if (ImGui.SliderFloat("Shore Foam Depth", ref shoreFoamDepth, 0.1f, 10.0f))
                    {
                        water.ShoreFoamDepth = shoreFoamDepth;
                        changed = true;
                    }
                    ImGui.TextDisabled("Maximum depth for shore foam appearance");

                    float shoreFoamIntensity = water.ShoreFoamIntensity;
                    if (ImGui.SliderFloat("Shore Foam Intensity", ref shoreFoamIntensity, 0.0f, 3.0f))
                    {
                        water.ShoreFoamIntensity = shoreFoamIntensity;
                        changed = true;
                    }

                    var shoreFoamColor = new Numerics.Vector4(
                        water.ShoreFoamColor.X, water.ShoreFoamColor.Y,
                        water.ShoreFoamColor.Z, water.ShoreFoamColor.W);
                    if (ImGui.ColorEdit4("Shore Foam Color", ref shoreFoamColor))
                    {
                        water.ShoreFoamColor = new Vector4(
                            shoreFoamColor.X, shoreFoamColor.Y, shoreFoamColor.Z, shoreFoamColor.W);
                        changed = true;
                    }

                    float shoreFoamScale = water.ShoreFoamScale;
                    if (ImGui.DragFloat("Shore Foam Scale", ref shoreFoamScale, 0.1f, 0.1f, 50.0f))
                    {
                        water.ShoreFoamScale = shoreFoamScale;
                        changed = true;
                    }

                    float shoreFoamSpeed = water.ShoreFoamSpeed;
                    if (ImGui.DragFloat("Shore Foam Speed", ref shoreFoamSpeed, 0.001f, 0.0f, 0.5f))
                    {
                        water.ShoreFoamSpeed = shoreFoamSpeed;
                        changed = true;
                    }

                    float shoreFoamFade = water.ShoreFoamFade;
                    if (ImGui.SliderFloat("Shore Foam Fade", ref shoreFoamFade, 0.1f, 2.0f))
                    {
                        water.ShoreFoamFade = shoreFoamFade;
                        changed = true;
                    }
                    ImGui.TextDisabled("Fade curve: higher = sharper transition");

                    float shoreFoamEdgeSharpness = water.ShoreFoamEdgeSharpness;
                    if (ImGui.SliderFloat("Edge Sharpness", ref shoreFoamEdgeSharpness, 0.5f, 5.0f))
                    {
                        water.ShoreFoamEdgeSharpness = shoreFoamEdgeSharpness;
                        changed = true;
                    }
                    ImGui.TextDisabled("Emphasize very shallow areas");

                    ImGui.TextDisabled("Uses same foam texture as Crest Foam if assigned");
                }
            }

            // === REFLECTIONS ===
            if (ThemedImGui.CollapsingHeader("Reflections"))
            {
                bool reflEnabled = water.ReflectionEnabled;
                if (ImGui.Checkbox("Enable Reflections", ref reflEnabled))
                {
                    water.ReflectionEnabled = reflEnabled;
                    changed = true;
                }

                if (water.ReflectionEnabled)
                {
                    bool usePlanar = water.UsePlanarReflection;
                    if (ImGui.Checkbox("Use Planar Reflection", ref usePlanar))
                    {
                        water.UsePlanarReflection = usePlanar;
                        changed = true;
                    }
                    ImGui.TextDisabled("Real-time planar reflections (more accurate)");

                    float reflIntensity = water.ReflectionIntensity;
                    if (ImGui.SliderFloat("Reflection Intensity", ref reflIntensity, 0.0f, 2.0f))
                    {
                        water.ReflectionIntensity = reflIntensity;
                        changed = true;
                    }

                    float reflDistortion = water.ReflectionDistortion;
                    if (ImGui.SliderFloat("Reflection Distortion", ref reflDistortion, 0.0f, 0.3f))
                    {
                        water.ReflectionDistortion = reflDistortion;
                        changed = true;
                    }

                    if (water.UsePlanarReflection)
                    {
                        int reflRes = water.ReflectionResolution;
                        string[] resOptions = { "256", "512", "1024", "2048" };
                        int[] resValues = { 256, 512, 1024, 2048 };
                        int currentIndex = Array.IndexOf(resValues, reflRes);
                        if (currentIndex < 0) currentIndex = 2;
                        if (ImGui.Combo("Reflection Resolution", ref currentIndex, resOptions, resOptions.Length))
                        {
                            water.ReflectionResolution = resValues[currentIndex];
                            changed = true;
                        }
                    }
                }
            }

            // === SPECULAR ===
            if (ThemedImGui.CollapsingHeader("Specular"))
            {
                float specIntensity = water.SpecularIntensity;
                if (ImGui.SliderFloat("Specular Intensity", ref specIntensity, 0.0f, 10.0f))
                {
                    water.SpecularIntensity = specIntensity;
                    changed = true;
                }

                float specPower = water.SpecularPower;
                if (ImGui.DragFloat("Specular Power", ref specPower, 10.0f, 1.0f, 2000.0f))
                {
                    water.SpecularPower = specPower;
                    changed = true;
                }
                ImGui.TextDisabled("Higher = sharper sun reflection");

                float roughness = water.Roughness;
                if (ImGui.SliderFloat("Roughness", ref roughness, 0.0f, 1.0f))
                {
                    water.Roughness = roughness;
                    changed = true;
                }
            }

            // === REFRACTION ===
            if (ThemedImGui.CollapsingHeader("Refraction"))
            {
                bool refrEnabled = water.RefractionEnabled;
                if (ImGui.Checkbox("Enable Refraction", ref refrEnabled))
                {
                    water.RefractionEnabled = refrEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("See-through effect with distortion");

                if (water.RefractionEnabled)
                {
                    float refrStrength = water.RefractionStrength;
                    if (ImGui.SliderFloat("Refraction Strength", ref refrStrength, 0.0f, 0.5f))
                    {
                        water.RefractionStrength = refrStrength;
                        changed = true;
                    }

                    float refrChromatic = water.RefractionChromatic;
                    if (ImGui.SliderFloat("Chromatic Aberration", ref refrChromatic, 0.0f, 0.1f))
                    {
                        water.RefractionChromatic = refrChromatic;
                        changed = true;
                    }
                    ImGui.TextDisabled("RGB color separation");
                }
            }

            // === CAUSTICS ===
            if (ThemedImGui.CollapsingHeader("Caustics"))
            {
                bool causticsEnabled = water.CausticsEnabled;
                if (ImGui.Checkbox("Enable Caustics", ref causticsEnabled))
                {
                    water.CausticsEnabled = causticsEnabled;
                    changed = true;
                }
                ImGui.TextDisabled("Light patterns on underwater surfaces");

                if (water.CausticsEnabled)
                {
                    float causticsIntensity = water.CausticsIntensity;
                    if (ImGui.SliderFloat("Caustics Intensity", ref causticsIntensity, 0.0f, 2.0f))
                    {
                        water.CausticsIntensity = causticsIntensity;
                        changed = true;
                    }

                    float causticsScale = water.CausticsScale;
                    if (ImGui.DragFloat("Caustics Scale", ref causticsScale, 0.1f, 0.1f, 10.0f))
                    {
                        water.CausticsScale = causticsScale;
                        changed = true;
                    }

                    float causticsSpeed = water.CausticsSpeed;
                    if (ImGui.DragFloat("Caustics Speed", ref causticsSpeed, 0.1f, 0.0f, 5.0f))
                    {
                        water.CausticsSpeed = causticsSpeed;
                        changed = true;
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Text("Advanced Caustics (GPU Gems)");
                    ImGui.Spacing();

                    int causticsOctaves = water.CausticsOctaves;
                    if (ImGui.SliderInt("Octaves", ref causticsOctaves, 1, 6))
                    {
                        water.CausticsOctaves = causticsOctaves;
                        changed = true;
                    }
                    ImGui.TextDisabled("Number of caustic layers (quality vs performance)");

                    float causticsBrightness = water.CausticsBrightness;
                    if (ImGui.SliderFloat("Brightness", ref causticsBrightness, 0.0f, 3.0f))
                    {
                        water.CausticsBrightness = causticsBrightness;
                        changed = true;
                    }
                    ImGui.TextDisabled("Overall brightness multiplier");

                    float causticsSharpness = water.CausticsSharpness;
                    if (ImGui.SliderFloat("Sharpness", ref causticsSharpness, 1.0f, 10.0f))
                    {
                        water.CausticsSharpness = causticsSharpness;
                        changed = true;
                    }
                    ImGui.TextDisabled("Focus/sharpness (higher = sharper caustics)");

                    float causticsDistortion = water.CausticsDistortion;
                    if (ImGui.SliderFloat("Distortion", ref causticsDistortion, 0.0f, 2.0f))
                    {
                        water.CausticsDistortion = causticsDistortion;
                        changed = true;
                    }
                    ImGui.TextDisabled("Wave-based distortion strength");

                    float causticsDepthFalloff = water.CausticsDepthFalloff;
                    if (ImGui.SliderFloat("Depth Falloff", ref causticsDepthFalloff, 0.0f, 1.0f))
                    {
                        water.CausticsDepthFalloff = causticsDepthFalloff;
                        changed = true;
                    }
                    ImGui.TextDisabled("How quickly caustics fade with depth");

                    float causticsChromatic = water.CausticsChromatic;
                    if (ImGui.SliderFloat("Chromatic Separation", ref causticsChromatic, 0.0f, 0.2f))
                    {
                        water.CausticsChromatic = causticsChromatic;
                        changed = true;
                    }
                    ImGui.TextDisabled("RGB color separation for realism");
                }
            }

            // === ABSORPTION ===
            if (ThemedImGui.CollapsingHeader("Absorption"))
            {
                var absColor = new Numerics.Vector3(
                    water.AbsorptionColor.X, water.AbsorptionColor.Y, water.AbsorptionColor.Z);
                if (ImGui.ColorEdit3("Absorption Color", ref absColor))
                {
                    water.AbsorptionColor = new Vector3(absColor.X, absColor.Y, absColor.Z);
                    changed = true;
                }
                ImGui.TextDisabled("Color absorbed by water with depth");

                float absStrength = water.AbsorptionStrength;
                if (ImGui.SliderFloat("Absorption Strength", ref absStrength, 0.0f, 2.0f))
                {
                    water.AbsorptionStrength = absStrength;
                    changed = true;
                }
            }

            // === NORMAL DETAIL ===
            if (ThemedImGui.CollapsingHeader("Normal Detail"))
            {
                float normalStrength = water.NormalStrength;
                if (ImGui.SliderFloat("Normal Strength", ref normalStrength, 0.0f, 2.0f))
                {
                    water.NormalStrength = normalStrength;
                    changed = true;
                }

                int normalIter = water.NormalIterations;
                if (ImGui.SliderInt("Normal Iterations", ref normalIter, 4, 64))
                {
                    water.NormalIterations = normalIter;
                    changed = true;
                }
                ImGui.TextDisabled("Quality of normal calculation (affects performance)");

                float normalEpsilon = water.NormalEpsilon;
                if (ImGui.DragFloat("Normal Epsilon", ref normalEpsilon, 0.001f, 0.001f, 0.1f, "%.4f"))
                {
                    water.NormalEpsilon = normalEpsilon;
                    changed = true;
                }
                ImGui.TextDisabled("Sample distance for normal calculation");
            }

            return changed;
        }
    }
}
