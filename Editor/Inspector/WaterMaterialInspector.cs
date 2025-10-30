using System;
using ImGuiNET;
using Engine.Assets;
using Numerics = System.Numerics;

namespace Editor.Inspector
{
    public static class WaterMaterialInspector
    {
        public static void DrawWaterProperties(MaterialAsset mat)
        {
            if (mat.WaterProperties == null)
            {
                mat.WaterProperties = new WaterMaterialProperties();
            }

            var water = mat.WaterProperties;

            // Initialize default values if needed
            bool needsSave = false;
            if (water.WaterColor == null || (water.WaterColor[0] == 1f && water.WaterColor[1] == 1f && water.WaterColor[2] == 1f))
            {
                water.WaterColor = new float[] { 0.1f, 0.3f, 0.5f, 0.8f };
                needsSave = true;
            }
            if (water.FresnelColor == null)
            {
                water.FresnelColor = new float[] { 0.8f, 0.9f, 1.0f, 1.0f };
                needsSave = true;
            }
            if (needsSave)
            {
                Engine.Assets.AssetDatabase.SaveMaterial(mat);
            }

            ImGui.Separator();
            ImGui.Text("🌊 Water Shader Properties");
            ImGui.Spacing();

            // === APPEARANCE ===
            if (ImGui.CollapsingHeader("Appearance", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool changed = false;

                // Water Color
                var waterCol = new Numerics.Vector4(water.WaterColor[0], water.WaterColor[1], water.WaterColor[2], water.WaterColor[3]);
                ImGui.SetNextItemWidth(200);
                if (ImGui.ColorEdit4("Water Color", ref waterCol))
                {
                    water.WaterColor[0] = waterCol.X;
                    water.WaterColor[1] = waterCol.Y;
                    water.WaterColor[2] = waterCol.Z;
                    water.WaterColor[3] = waterCol.W;
                    changed = true;
                }

                // Opacity
                float opacity = water.Opacity;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Opacity", ref opacity, 0f, 1f))
                {
                    water.Opacity = opacity;
                    changed = true;
                }

                // Metallic
                float metallic = water.Metallic;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Metallic", ref metallic, 0f, 1f))
                {
                    water.Metallic = metallic;
                    changed = true;
                }
                ImGui.TextDisabled("How reflective/shiny the water is");

                // Smoothness
                float smoothness = water.Smoothness;
                ImGui.SetNextItemWidth(200);
                if (ImGui.SliderFloat("Smoothness", ref smoothness, 0f, 1f))
                {
                    water.Smoothness = smoothness;
                    changed = true;
                }
                ImGui.TextDisabled("Surface smoothness (higher = sharper specular)");

                if (changed)
                {
                    Engine.Assets.AssetDatabase.SaveMaterial(mat);
                }
            }

            // === FRESNEL ===
            if (ImGui.CollapsingHeader("Fresnel Effect", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool changed = false;

                // Fresnel Power
                float fresnelPow = water.FresnelPower;
                ImGui.SetNextItemWidth(200);
                if (ImGui.DragFloat("Fresnel Power", ref fresnelPow, 0.1f, 0.1f, 10f))
                {
                    water.FresnelPower = fresnelPow;
                    changed = true;
                }
                ImGui.TextDisabled("Controls edge brightness (3.0 = default)");

                if (changed)
                {
                    Engine.Assets.AssetDatabase.SaveMaterial(mat);
                }
            }

            // === PLANAR REFLECTION ===
            if (ImGui.CollapsingHeader("Planar Reflection", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool changed = false;

                // Enable reflection
                bool enableRefl = water.EnableReflection;
                if (ImGui.Checkbox("Enable Reflection", ref enableRefl))
                {
                    water.EnableReflection = enableRefl;
                    changed = true;
                }

                if (water.EnableReflection)
                {
                    ImGui.TextDisabled("Reflection is auto-generated from scene");
                    ImGui.Spacing();

                    // Reflection Strength
                    float reflStr = water.ReflectionStrength;
                    ImGui.SetNextItemWidth(200);
                    if (ImGui.SliderFloat("Reflection Strength", ref reflStr, 0f, 1f))
                    {
                        water.ReflectionStrength = reflStr;
                        changed = true;
                    }
                    ImGui.TextDisabled("How much the reflection is visible");

                    // Reflection update interval
                    int reflInterval = water.ReflectionUpdateInterval;
                    ImGui.SetNextItemWidth(200);
                    if (ImGui.SliderInt("Update Interval (frames)", ref reflInterval, 1, 10))
                    {
                        water.ReflectionUpdateInterval = reflInterval;
                        changed = true;
                    }
                    ImGui.TextDisabled("Higher = better performance, lower = smoother");
                }

                if (changed)
                {
                    Engine.Assets.AssetDatabase.SaveMaterial(mat);
                }
            }
        }
    }
}
