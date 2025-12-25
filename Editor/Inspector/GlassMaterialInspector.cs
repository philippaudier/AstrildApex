using System;
using ImGuiNET;
using Engine.Assets;
using Editor.UI;
using Numerics = System.Numerics;

namespace Editor.Inspector
{
    public static class GlassMaterialInspector
    {
        public static void DrawGlassProperties(MaterialAsset mat)
        {
            if (mat.GlassProperties == null)
            {
                mat.GlassProperties = new GlassMaterialProperties();
            }

            var glass = mat.GlassProperties;

            ImGui.Separator();
            ImGui.Text("🔷 Glass Shader Properties");
            ImGui.Spacing();

            // === PRESETS ===
            if (ThemedImGui.CollapsingHeader("Presets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Quick presets for common glass types");
                ImGui.Spacing();

                if (ImGui.Button("Window Glass"))
                {
                    mat.GlassProperties = GlassMaterialProperties.CreateWindow();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Frosted Glass"))
                {
                    mat.GlassProperties = GlassMaterialProperties.CreateFrostedGlass();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Glass Sphere"))
                {
                    mat.GlassProperties = GlassMaterialProperties.CreateSphere();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }

                if (ImGui.Button("Diamond"))
                {
                    mat.GlassProperties = GlassMaterialProperties.CreateDiamond();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Stained Glass"))
                {
                    mat.GlassProperties = GlassMaterialProperties.CreateStainedGlass();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
                ImGui.SameLine();
                if (ImGui.Button("Bottle"))
                {
                    mat.GlassProperties = GlassMaterialProperties.CreateBottle();
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
            }

            // === REFRACTION ===
            if (ThemedImGui.CollapsingHeader("Refraction", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool changed = false;

                // Refractive Index
                float refractiveIndex = glass.RefractiveIndex;
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragFloat("Refractive Index", ref refractiveIndex, 0.01f, 1.0f, 3.0f))
                {
                    glass.RefractiveIndex = refractiveIndex;
                    changed = true;
                }
                ImGui.TextDisabled("1.0 = air, 1.33 = water, 1.5 = glass, 2.42 = diamond");

                // Distortion Strength
                float distortion = glass.DistortionStrength;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Distortion Strength", ref distortion, 0f, 1f))
                {
                    glass.DistortionStrength = distortion;
                    changed = true;
                }
                ImGui.TextDisabled("0.0 = flat window (no distortion), 1.0 = full refraction (sphere)");

                // Chromatic Aberration
                float chromatic = glass.ChromaticAberration;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Chromatic Aberration", ref chromatic, 0f, 1f))
                {
                    glass.ChromaticAberration = chromatic;
                    changed = true;
                }
                ImGui.TextDisabled("RGB color separation (0 = none, 1 = rainbow prism effect)");

                if (changed)
                {
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
            }

            // === APPEARANCE ===
            if (ThemedImGui.CollapsingHeader("Appearance", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool changed = false;

                // Roughness
                float roughness = glass.Roughness;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Roughness", ref roughness, 0f, 1f))
                {
                    glass.Roughness = roughness;
                    changed = true;
                }
                ImGui.TextDisabled("0 = smooth glass, 1 = frosted/rough glass");

                // Opacity
                float opacity = glass.Opacity;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Opacity", ref opacity, 0f, 1f))
                {
                    glass.Opacity = opacity;
                    changed = true;
                }
                ImGui.TextDisabled("Base transparency (0 = fully transparent, 1 = opaque)");

                // Thickness
                float thickness = glass.Thickness;
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragFloat("Thickness", ref thickness, 0.01f, 0.01f, 1f))
                {
                    glass.Thickness = thickness;
                    changed = true;
                }
                ImGui.TextDisabled("Glass thickness (affects color absorption)");

                // Tint Color
                var tint = new Numerics.Vector3(glass.Tint[0], glass.Tint[1], glass.Tint[2]);
                ImGui.SetNextItemWidth(200);
                if (ImGui.ColorEdit3("Tint Color", ref tint))
                {
                    glass.Tint[0] = tint.X;
                    glass.Tint[1] = tint.Y;
                    glass.Tint[2] = tint.Z;
                    changed = true;
                }
                ImGui.TextDisabled("Glass color tint (absorption color)");

                if (changed)
                {
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
            }

            // === REFLECTIONS ===
            if (ThemedImGui.CollapsingHeader("Reflections (Fresnel)", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool changed = false;

                // Fresnel Power
                float fresnelPower = glass.FresnelPower;
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragFloat("Fresnel Power", ref fresnelPower, 0.1f, 1f, 10f))
                {
                    glass.FresnelPower = fresnelPower;
                    changed = true;
                }
                ImGui.TextDisabled("Controls reflection falloff (5 = default, higher = sharper edge)");

                // Reflection Strength
                float reflectionStrength = glass.ReflectionStrength;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Reflection Strength", ref reflectionStrength, 0f, 2f))
                {
                    glass.ReflectionStrength = reflectionStrength;
                    changed = true;
                }
                ImGui.TextDisabled("Base reflection intensity");

                if (changed)
                {
                    try { Engine.Assets.AssetDatabase.SaveMaterialAsync(mat); } catch { }
                }
            }

            // === INFO BOX ===
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(new Numerics.Vector4(0.6f, 0.8f, 1f, 1f), "ℹ Info:");
            ImGui.TextWrapped("The Glass shader simulates realistic glass with refraction, reflections, and Fresnel effects. " +
                              "Use the Distortion Strength to control how much the background is distorted:\n" +
                              "• Window/Flat surface: 0.0-0.1 (minimal distortion)\n" +
                              "• Bottle/Curved object: 0.3-0.6 (medium distortion)\n" +
                              "• Sphere/Lens: 0.8-1.0 (full physical refraction - image inverts!)");
        }
    }
}
