using System;
using System.Numerics;
using ImGuiNET;
using Engine.Components;
using Engine.Assets;
using Editor.Logging;
using Editor.Themes;
using Editor.UI;

namespace Editor.Inspector
{
    /// <summary>
    /// UI for managing terrain vegetation layers in the inspector.
    /// Provides an intuitive workflow similar to Unity's Terrain Vegetation system.
    /// </summary>
    public static class TerrainVegetationUI
    {
        private static int _selectedLayerIndex = -1;

        /// <summary>
        /// Draw the vegetation section in the terrain inspector.
        /// </summary>
        public static void DrawVegetationSection(Terrain terrain)
        {
            ImGui.PushID("VegetationSection");

            // Header with icon
            ImGui.Spacing();
            var headerColor = new Vector4(0.2f, 0.7f, 0.3f, 1f);
            ImGui.PushStyleColor(ImGuiCol.Text, headerColor);
            ImGui.Text("🌲 Vegetation");
            ImGui.PopStyleColor();
            ImGui.Separator();

            // Description
            ImGui.TextDisabled("Procedurally spawn vegetation on terrain");
            ImGui.Spacing();

            // Layer list
            if (terrain.VegetationLayers == null || terrain.VegetationLayers.Length == 0)
            {
                ImGui.TextDisabled("No vegetation layers defined");
                ImGui.Spacing();
            }
            else
            {
                DrawLayerList(terrain);
                ImGui.Spacing();

                // Layer details
                if (_selectedLayerIndex >= 0 && _selectedLayerIndex < terrain.VegetationLayers.Length)
                {
                    DrawLayerDetails(terrain, _selectedLayerIndex);
                    ImGui.Spacing();
                }
            }

            // Buttons
            ImGui.Separator();
            
            if (ImGui.Button("➕ Add Layer", new Vector2(ImGui.GetContentRegionAvail().X * 0.48f, 30)))
            {
                AddLayer(terrain);
            }

            ImGui.SameLine();

            bool hasLayers = terrain.VegetationLayers != null && terrain.VegetationLayers.Length > 0;
            if (!hasLayers) ImGui.BeginDisabled();
            
            if (ImGui.Button("🔄 Regenerate Vegetation", new Vector2(ImGui.GetContentRegionAvail().X, 30)))
            {
                RegenerateVegetation(terrain);
            }

            if (!hasLayers) ImGui.EndDisabled();

            // Clear button
            if (hasLayers)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.3f, 0.3f, 1f));
                
                if (ImGui.Button("🗑️ Clear All Vegetation", new Vector2(-1, 0)))
                {
                    var renderer = Editor.Panels.EditorUI.MainViewport.Renderer;
                    if (renderer != null)
                    {
                        try
                        {
                            Console.WriteLine("[TerrainVegetationUI] Clearing all vegetation instances (keeping layers)");

                            // Get the current scene
                            var scene = renderer.Scene;
                            if (scene != null)
                            {
                                // Clear vegetation WITHOUT setting VegetationCleared flag
                                // This allows regeneration to work afterwards
                                terrain.ClearVegetation(scene, setCleared: false);
                                
                                LogManager.LogInfo("All vegetation instances cleared (layers preserved)", "TerrainVegetationUI");
                                Editor.SceneManagement.SceneManager.MarkSceneAsModified();
                            }
                            else
                            {
                                LogManager.LogError("Cannot clear vegetation - Scene is null", "TerrainVegetationUI");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogError($"Failed to clear vegetation: {ex.Message}", "TerrainVegetationUI");
                        }
                    }
                    else
                    {
                        LogManager.LogError("Cannot clear vegetation - ViewportRenderer not available", "TerrainVegetationUI");
                    }
                }
                
                ImGui.PopStyleColor(2);
            }

            ImGui.PopID();
        }

        /// <summary>
        /// Draw the list of vegetation layers.
        /// </summary>
        private static void DrawLayerList(Terrain terrain)
        {
            ImGui.Text("Layers");
            ImGui.BeginChild("LayerList", new Vector2(-1, 150), ImGuiChildFlags.Borders);

            for (int i = 0; i < terrain.VegetationLayers!.Length; i++)
            {
                var layer = terrain.VegetationLayers[i];
                
                ImGui.PushID(i);

                // Selection highlight
                bool isSelected = (_selectedLayerIndex == i);
                if (isSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.3f, 0.5f, 0.8f, 0.4f));
                }

                // Layer item
                bool clicked = ImGui.Selectable($"##layer_{i}", isSelected, ImGuiSelectableFlags.SpanAllColumns);
                
                if (isSelected)
                {
                    ImGui.PopStyleColor();
                }

                if (clicked)
                {
                    _selectedLayerIndex = i;
                }

                ImGui.SameLine();

                // Enable checkbox
                bool enabled = layer.Enabled;
                if (ImGui.Checkbox("##enabled", ref enabled))
                {
                    layer.Enabled = enabled;
                }

                ImGui.SameLine();

                // Layer name
                ImGui.Text($"{i + 1}. {layer.Name}");

                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 60);

                // Delete button
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 0.5f));
                if (ImGui.SmallButton("Delete"))
                {
                    RemoveLayer(terrain, i);
                    if (_selectedLayerIndex >= terrain.VegetationLayers.Length)
                    {
                        _selectedLayerIndex = -1;
                    }
                    ImGui.PopStyleColor();
                    ImGui.PopID();
                    break;
                }
                ImGui.PopStyleColor();

                ImGui.PopID();
            }

            ImGui.EndChild();
        }

        /// <summary>
        /// Draw detailed settings for a selected layer.
        /// </summary>
        private static void DrawLayerDetails(Terrain terrain, int layerIndex)
        {
            var layer = terrain.VegetationLayers![layerIndex];

            ImGui.PushID($"LayerDetails_{layerIndex}");

            ImGui.Text($"Layer {layerIndex + 1} Settings");
            ImGui.Separator();

            // Name
            string name = layer.Name ?? "";
            if (ImGui.InputText("Name", ref name, 256))
            {
                layer.Name = name;
            }

            ImGui.Spacing();

            // === PREFAB OR MODEL ===
            ImGui.Text("Prefab / Model");

            var newPrefabGuid = EditorWidgets.AssetField(
                "Prefab (Recommended)",
                layer.PrefabGuid,
                "Prefab",
                "Drag & drop a .prefab asset",
                showPreview: false);

            if (newPrefabGuid != layer.PrefabGuid)
            {
                layer.PrefabGuid = newPrefabGuid;
                if (newPrefabGuid.HasValue)
                {
                    LogManager.LogInfo($"Prefab assigned to layer {layerIndex + 1}", "TerrainVegetationUI");
                    // Clear ModelGuid when prefab is assigned (prefab has priority)
                    layer.ModelGuid = null;
                }
            }

            ImGui.TextDisabled("OR");

            var newModelGuid = EditorWidgets.AssetField(
                "Imported Model (Legacy)",
                layer.ModelGuid,
                "Model",  // Matches ModelGLTF, ModelFBX, ModelOBJ
                "Drag & drop a .gltf/.fbx/.obj model",
                showPreview: false);

            if (newModelGuid != layer.ModelGuid)
            {
                layer.ModelGuid = newModelGuid;
                if (newModelGuid.HasValue)
                {
                    LogManager.LogInfo($"Model assigned to layer {layerIndex + 1}", "TerrainVegetationUI");
                    // Clear PrefabGuid when model is assigned
                    layer.PrefabGuid = null;
                }
            }

            // Submesh index (only for models)
            if (!layer.PrefabGuid.HasValue && layer.ModelGuid.HasValue)
            {
                int submeshIndex = layer.SubmeshIndex;
                if (ImGui.DragInt("Submesh Index", ref submeshIndex, 0.1f, -1, 100))
                {
                    layer.SubmeshIndex = Math.Max(-1, submeshIndex);
                }
                ImGui.TextDisabled("-1 = all submeshes, 0+ = specific submesh");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // === GPU Grass Layer (optional) ===
            bool isGrassLayer = layer.IsGrassLayer;
            if (ImGui.Checkbox("Enable Grass Coverage (GPU)", ref isGrassLayer))
            {
                layer.IsGrassLayer = isGrassLayer;
                if (isGrassLayer && layer.GrassProperties == null)
                {
                    layer.GrassProperties = new Engine.Assets.GrassProperties();
                }
                else if (!isGrassLayer)
                {
                    layer.GrassProperties = null;
                }
                // Force regeneration when toggling
                RegenerateVegetation(terrain);
            }

            if (layer.IsGrassLayer && layer.GrassProperties != null)
            {
                ImGui.Indent();
                ImGui.TextDisabled("GPU-generated dense grass from terrain mesh");

                var grass = layer.GrassProperties;
                bool grassChanged = false;

                // Density & Coverage
                if (ImGui.TreeNode("Density & Coverage"))
                {
                    float gDensity = grass.Density;
                    if (ImGui.SliderFloat("Density##grass", ref gDensity, 0.1f, 3.0f))
                    {
                        grass.Density = gDensity;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Multiplier for blade count per triangle");

                    int bladesPerTri = grass.BladesPerVertex;
                    if (ImGui.SliderInt("Blades Per Triangle", ref bladesPerTri, 1, 10))
                    {
                        grass.BladesPerVertex = bladesPerTri;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Base number of grass blades per triangle");

                    float coverageScale = grass.CoverageNoiseScale;
                    if (ImGui.SliderFloat("Coverage Noise Scale", ref coverageScale, 0.001f, 0.5f, "%.3f"))
                    {
                        grass.CoverageNoiseScale = coverageScale;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("World-space noise scale for patchy distribution");

                    float coverageThreshold = grass.CoverageThreshold;
                    if (ImGui.SliderFloat("Coverage Threshold", ref coverageThreshold, 0.0f, 0.8f))
                    {
                        grass.CoverageThreshold = coverageThreshold;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("0 = full coverage, higher = more gaps");

                    ImGui.TreePop();
                }

                // Blade Geometry
                if (ImGui.TreeNode("Blade Geometry"))
                {
                    float height = grass.BladeHeight;
                    if (ImGui.SliderFloat("Blade Height", ref height, 0.05f, 2.0f))
                    {
                        grass.BladeHeight = height;
                        grassChanged = true;
                    }

                    float heightVar = grass.BladeHeightVariation;
                    if (ImGui.SliderFloat("Height Variation", ref heightVar, 0.0f, 0.8f))
                    {
                        grass.BladeHeightVariation = heightVar;
                        grassChanged = true;
                    }

                    float width = grass.BladeWidth;
                    if (ImGui.SliderFloat("Blade Width", ref width, 0.01f, 0.2f))
                    {
                        grass.BladeWidth = width;
                        grassChanged = true;
                    }

                    float curvature = grass.BladeCurvature;
                    if (ImGui.SliderFloat("Curvature", ref curvature, 0.0f, 1.0f))
                    {
                        grass.BladeCurvature = curvature;
                        grassChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Colors & Texture
                if (ImGui.TreeNode("Colors"))
                {
                    var colorTop = new System.Numerics.Vector4(
                        grass.ColorTop[0], grass.ColorTop[1], grass.ColorTop[2], grass.ColorTop[3]);
                    if (ImGui.ColorEdit4("Top Color", ref colorTop))
                    {
                        grass.ColorTop = new float[] { colorTop.X, colorTop.Y, colorTop.Z, colorTop.W };
                        grassChanged = true;
                    }

                    var colorBottom = new System.Numerics.Vector4(
                        grass.ColorBottom[0], grass.ColorBottom[1], grass.ColorBottom[2], grass.ColorBottom[3]);
                    if (ImGui.ColorEdit4("Bottom Color", ref colorBottom))
                    {
                        grass.ColorBottom = new float[] { colorBottom.X, colorBottom.Y, colorBottom.Z, colorBottom.W };
                        grassChanged = true;
                    }

                    float colorVar = grass.ColorVariation;
                    if (ImGui.SliderFloat("Color Variation", ref colorVar, 0.0f, 0.5f))
                    {
                        grass.ColorVariation = colorVar;
                        grassChanged = true;
                    }

                    var newTex = EditorWidgets.AssetField("Albedo Texture", grass.AlbedoTexture, "Texture", "Optional blade texture", showPreview: true);
                    if (newTex != grass.AlbedoTexture)
                    {
                        grass.AlbedoTexture = newTex;
                        grassChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Density Map (for painted coverage)
                if (ImGui.TreeNode("Density Map (Painted)"))
                {
                    ImGui.TextDisabled("Use a grayscale texture to paint grass coverage");
                    
                    var densityTex = EditorWidgets.AssetField("Density Map", grass.DensityMap, "Texture", "R8 texture for coverage painting", showPreview: true);
                    if (densityTex != grass.DensityMap)
                    {
                        grass.DensityMap = densityTex;
                        grassChanged = true;
                    }

                    float densityScale = grass.DensityMapScale;
                    if (ImGui.SliderFloat("Density Map Scale", ref densityScale, 0.001f, 0.1f, "%.4f"))
                    {
                        grass.DensityMapScale = densityScale;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("World-space UV scale for density map");

                    ImGui.TreePop();
                }

                // Wind Animation
                if (ImGui.TreeNode("Wind Animation"))
                {
                    float windStrength = grass.WindStrength;
                    if (ImGui.SliderFloat("Wind Strength", ref windStrength, 0.0f, 1.5f))
                    {
                        grass.WindStrength = windStrength;
                        grassChanged = true;
                    }

                    float windSpeed = grass.WindSpeed;
                    if (ImGui.SliderFloat("Wind Speed", ref windSpeed, 0.0f, 4.0f))
                    {
                        grass.WindSpeed = windSpeed;
                        grassChanged = true;
                    }

                    float windTurb = grass.WindTurbulence;
                    if (ImGui.SliderFloat("Wind Turbulence", ref windTurb, 0.0f, 1.5f))
                    {
                        grass.WindTurbulence = windTurb;
                        grassChanged = true;
                    }

                    ImGui.TreePop();
                }

                // LOD & Culling
                if (ImGui.TreeNode("LOD & Culling"))
                {
                    float maxDist = grass.MaxRenderDistance;
                    if (ImGui.SliderFloat("Max Render Distance", ref maxDist, 20f, 500f))
                    {
                        grass.MaxRenderDistance = maxDist;
                        grassChanged = true;
                    }

                    float fadeRange = grass.FadeRange;
                    if (ImGui.SliderFloat("Fade Range", ref fadeRange, 5f, 100f))
                    {
                        grass.FadeRange = fadeRange;
                        grassChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Action buttons
                ImGui.Spacing();
                if (ImGui.Button("🗑️ Disable Grass Layer", new Vector2(-1, 0)))
                {
                    layer.IsGrassLayer = false;
                    layer.GrassProperties = null;
                    grassChanged = true;
                }

                if (grassChanged)
                {
                    // GPU grass parameters are updated every frame, no regeneration needed
                }

                ImGui.Unindent();
                ImGui.Separator();
            }

            // === DENSITY ===
            ImGui.Text("Density");
            float density = layer.Density;
            if (ImGui.SliderFloat("Instances per 100²", ref density, 0f, 100f, "%.1f"))
            {
                layer.Density = Math.Max(0f, density);
            }
            int estimatedTotal = (int)(layer.Density * (terrain.TerrainWidth / 100f) * (terrain.TerrainLength / 100f));
            ImGui.TextDisabled($"≈ {estimatedTotal} instances estimated");

            ImGui.Spacing();

            // === SEED ===
            ImGui.Text("Random Seed");
            int seed = layer.Seed;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 120);
            if (ImGui.InputInt("##seed", ref seed))
            {
                layer.Seed = seed;
            }
            ImGui.SameLine();
            if (ImGui.Button("🎲 Randomize", new Vector2(110, 0)))
            {
                layer.Seed = new Random().Next();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Generate new random seed for different placement");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // === PLACEMENT RULES ===
            ImGui.Text("Placement Rules");

            // Height range
            ImGui.Text("Height Range (normalized)");
            float minHeight = layer.MinHeight;
            float maxHeight = layer.MaxHeight;
            ImGui.SliderFloat("Min Height", ref minHeight, 0f, 1f);
            ImGui.SliderFloat("Max Height", ref maxHeight, 0f, 1f);
            layer.MinHeight = Math.Min(minHeight, maxHeight);
            layer.MaxHeight = Math.Max(minHeight, maxHeight);

            // Slope range
            ImGui.Text("Slope Range (degrees)");
            float minSlope = layer.MinSlope;
            float maxSlope = layer.MaxSlope;
            ImGui.SliderFloat("Min Slope", ref minSlope, 0f, 90f);
            ImGui.SliderFloat("Max Slope", ref maxSlope, 0f, 90f);
            layer.MinSlope = Math.Min(minSlope, maxSlope);
            layer.MaxSlope = Math.Max(minSlope, maxSlope);

            // Minimum distance between instances
            ImGui.Text("Min Distance Between Instances (m)");
            float minDistance = layer.MinDistance;
            if (ImGui.SliderFloat("Min Distance", ref minDistance, 0f, 20f, "%.1f"))
            {
                layer.MinDistance = Math.Max(0f, minDistance);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Minimum distance between instances\n0 = no spacing constraint (allow overlap)\n>0 = reject instances too close to existing ones");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // === SCALE & VARIATION ===
            ImGui.Text("Scale & Variation");

            float minScale = layer.MinScale;
            float maxScale = layer.MaxScale;
            ImGui.SliderFloat("Min Scale", ref minScale, 0.01f, 10f);
            ImGui.SliderFloat("Max Scale", ref maxScale, 0.01f, 10f);
            layer.MinScale = Math.Min(minScale, maxScale);
            layer.MaxScale = Math.Max(minScale, maxScale);

            bool randomRotation = layer.RandomRotation;
            if (ImGui.Checkbox("Random Y Rotation", ref randomRotation))
            {
                layer.RandomRotation = randomRotation;
            }

            bool alignToNormal = layer.AlignToNormal;
            if (ImGui.Checkbox("Align to Terrain Normal", ref alignToNormal))
            {
                layer.AlignToNormal = alignToNormal;
            }

            if (layer.AlignToNormal)
            {
                float alignmentStrength = layer.AlignmentStrength;
                if (ImGui.SliderFloat("Alignment Strength (%)", ref alignmentStrength, 0f, 100f))
                {
                    layer.AlignmentStrength = Math.Clamp(alignmentStrength, 0f, 100f);
                }
                ImGui.TextDisabled("0% = vertical, 100% = full alignment to surface");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // === CULLING & OPTIMIZATION ===
            ImGui.Text("Culling & Optimization");

            float maxRenderDistance = layer.MaxRenderDistance;
            if (ImGui.SliderFloat("Max Render Distance (m)", ref maxRenderDistance, 0f, 2000f, "%.0f"))
            {
                layer.MaxRenderDistance = Math.Max(0f, maxRenderDistance);
                // Force regeneration to update renderer with new culling params
                RegenerateVegetation(terrain);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Maximum distance from camera to render instances\n0 = infinite (not recommended for dense vegetation)");
            }

            float cullingSphereRadius = layer.CullingSphereRadius;
            if (ImGui.SliderFloat("Culling Radius (m)", ref cullingSphereRadius, 0.1f, 50f, "%.1f"))
            {
                layer.CullingSphereRadius = Math.Max(0.1f, cullingSphereRadius);
                // Force regeneration to update renderer with new culling params
                RegenerateVegetation(terrain);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Bounding sphere radius for frustum culling\nShould match the size of your model");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // Advanced settings removed: vegetation layers no longer expose per-layer draw-distance/LOD/wind here.

            ImGui.PopID();
        }

        /// <summary>
        /// Add a new vegetation layer.
        /// </summary>
        private static void AddLayer(Terrain terrain)
        {
            var newLayer = new VegetationLayer
            {
                Name = $"Vegetation Layer {(terrain.VegetationLayers?.Length ?? 0) + 1}",
                Enabled = true,
                Density = 10f,
                Seed = new Random().Next(),
                MinHeight = 0f,
                MaxHeight = 1f,
                MinSlope = 0f,
                MaxSlope = 30f,
                MinScale = 0.8f,
                MaxScale = 1.2f,
                RandomRotation = true,
                AlignToNormal = false
            };

            if (terrain.VegetationLayers == null)
            {
                terrain.VegetationLayers = new[] { newLayer };
            }
            else
            {
                var newArray = new VegetationLayer[terrain.VegetationLayers.Length + 1];
                Array.Copy(terrain.VegetationLayers, newArray, terrain.VegetationLayers.Length);
                newArray[terrain.VegetationLayers.Length] = newLayer;
                terrain.VegetationLayers = newArray;
            }

            _selectedLayerIndex = terrain.VegetationLayers.Length - 1;
            LogManager.LogInfo($"Added vegetation layer: {newLayer.Name}", "TerrainVegetationUI");
        }

        /// <summary>
        /// Remove a vegetation layer.
        /// </summary>
        private static void RemoveLayer(Terrain terrain, int index)
        {
            if (terrain.VegetationLayers == null || index < 0 || index >= terrain.VegetationLayers.Length)
                return;

            var newArray = new VegetationLayer[terrain.VegetationLayers.Length - 1];
            int writeIndex = 0;
            
            for (int i = 0; i < terrain.VegetationLayers.Length; i++)
            {
                if (i != index)
                {
                    newArray[writeIndex++] = terrain.VegetationLayers[i];
                }
            }

            terrain.VegetationLayers = newArray.Length > 0 ? newArray : null;
            
            if (_selectedLayerIndex >= newArray.Length)
            {
                _selectedLayerIndex = newArray.Length - 1;
            }

            LogManager.LogInfo($"Removed vegetation layer {index + 1}", "TerrainVegetationUI");
        }

        /// <summary>
        /// Regenerate vegetation for the terrain (works for both Single and Infinite Streaming modes).
        /// </summary>
        private static void RegenerateVegetation(Terrain terrain)
        {
            try
            {
                var renderer = Editor.Panels.EditorUI.MainViewport?.Renderer;
                if (renderer == null)
                {
                    LogManager.LogError("Cannot regenerate vegetation - ViewportRenderer not available", "TerrainVegetationUI");
                    return;
                }

                var scene = renderer.Scene;
                if (scene == null)
                {
                    LogManager.LogError("Cannot regenerate vegetation - Scene is null", "TerrainVegetationUI");
                    return;
                }

                // Inspector changes modify terrain layers directly in memory, so no need to save first
                Console.WriteLine($"[TerrainVegetationUI] Regenerating vegetation for terrain: {terrain.Entity?.Name ?? "(unnamed)"}, Mode: {terrain.Mode}");

                // Clear and regenerate vegetation using the same pattern as HandleTerrainModeChange
                // 1. Reset VegetationCleared flag to allow regeneration
                terrain.VegetationCleared = false;

                // 2. Clear old vegetation first
                terrain.ClearVegetation(scene, setCleared: false);

                // 3. Generate new vegetation
                terrain.GenerateVegetation(scene);

                // Mark scene as modified so user knows to save
                Editor.SceneManagement.SceneManager.MarkSceneAsModified();

                LogManager.LogInfo("Vegetation regenerated successfully", "TerrainVegetationUI");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to regenerate vegetation: {ex.Message}", "TerrainVegetationUI");
            }
        }
    }
}
