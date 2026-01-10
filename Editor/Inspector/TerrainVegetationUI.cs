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

                // Slope & Height Constraints
                if (ImGui.TreeNode("Slope & Height"))
                {
                    ImGui.Text("Slope Range (degrees)");
                    
                    float slopeMin = grass.MinSlope;
                    if (ImGui.SliderFloat("Min Slope", ref slopeMin, 0.0f, 89.0f))
                    {
                        grass.MinSlope = slopeMin;
                        if (grass.MaxSlope < slopeMin) grass.MaxSlope = slopeMin;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Minimum slope angle (0 = flat ground)");

                    float slopeMax = grass.MaxSlope;
                    if (ImGui.SliderFloat("Max Slope", ref slopeMax, 0.0f, 89.0f))
                    {
                        grass.MaxSlope = slopeMax;
                        if (grass.MinSlope > slopeMax) grass.MinSlope = slopeMax;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Maximum slope angle (90 = vertical cliff)");

                    ImGui.Separator();
                    ImGui.Text("Height Range (world units)");

                    float heightMin = grass.MinHeight;
                    if (ImGui.DragFloat("Min Height", ref heightMin, 1.0f, -1000.0f, 1000.0f))
                    {
                        grass.MinHeight = heightMin;
                        if (grass.MaxHeight < heightMin) grass.MaxHeight = heightMin;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Minimum world Y for grass placement");

                    float heightMax = grass.MaxHeight;
                    if (ImGui.DragFloat("Max Height", ref heightMax, 1.0f, -1000.0f, 1000.0f))
                    {
                        grass.MaxHeight = heightMax;
                        if (grass.MinHeight > heightMax) grass.MinHeight = heightMax;
                        grassChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Maximum world Y for grass placement");

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

            // === GPU Rock Layer (optional) ===
            bool isRockLayer = layer.IsRockLayer;
            if (ImGui.Checkbox("Enable Rock Coverage (GPU)", ref isRockLayer))
            {
                layer.IsRockLayer = isRockLayer;
                if (isRockLayer && layer.RockProperties == null)
                {
                    layer.RockProperties = new Engine.Assets.RockProperties();
                }
                else if (!isRockLayer)
                {
                    layer.RockProperties = null;
                }
                RegenerateVegetation(terrain);
            }

            if (layer.IsRockLayer && layer.RockProperties != null)
            {
                ImGui.Indent();
                ImGui.TextDisabled("GPU-generated procedural rocks from terrain mesh");

                var rock = layer.RockProperties;
                bool rockChanged = false;

                // Density & Distribution
                if (ImGui.TreeNode("Distribution##rock"))
                {
                    float rDensity = rock.Density;
                    if (ImGui.SliderFloat("Density##rock", ref rDensity, 0.05f, 2.0f))
                    {
                        rock.Density = rDensity;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Rocks per terrain triangle (0.05-2)");

                    float clustering = rock.ClusteringStrength;
                    if (ImGui.SliderFloat("Clustering", ref clustering, 0.0f, 1.0f))
                    {
                        rock.ClusteringStrength = clustering;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("0 = uniform, 1 = highly clustered");

                    float clusterScale = rock.ClusterNoiseScale;
                    if (ImGui.SliderFloat("Cluster Scale", ref clusterScale, 0.001f, 0.1f, "%.3f"))
                    {
                        rock.ClusterNoiseScale = clusterScale;
                        rockChanged = true;
                    }

                    float threshold = rock.PlacementThreshold;
                    if (ImGui.SliderFloat("Sparseness", ref threshold, 0.0f, 0.8f))
                    {
                        rock.PlacementThreshold = threshold;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Higher = sparser rock placement");

                    ImGui.TreePop();
                }

                // Slope & Height
                if (ImGui.TreeNode("Slope & Height##rock"))
                {
                    float rMinSlope = rock.MinSlope;
                    if (ImGui.SliderFloat("Min Slope##rock", ref rMinSlope, 0.0f, 89.0f))
                    {
                        rock.MinSlope = rMinSlope;
                        if (rock.MaxSlope < rMinSlope) rock.MaxSlope = rMinSlope;
                        rockChanged = true;
                    }

                    float rMaxSlope = rock.MaxSlope;
                    if (ImGui.SliderFloat("Max Slope##rock", ref rMaxSlope, 0.0f, 89.0f))
                    {
                        rock.MaxSlope = rMaxSlope;
                        if (rock.MinSlope > rMaxSlope) rock.MinSlope = rMaxSlope;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Rocks can appear on steeper slopes than grass");

                    ImGui.Separator();

                    float rMinHeight = rock.MinHeight;
                    if (ImGui.DragFloat("Min Height##rock", ref rMinHeight, 1.0f, -1000.0f, 1000.0f))
                    {
                        rock.MinHeight = rMinHeight;
                        rockChanged = true;
                    }

                    float rMaxHeight = rock.MaxHeight;
                    if (ImGui.DragFloat("Max Height##rock", ref rMaxHeight, 1.0f, -1000.0f, 1000.0f))
                    {
                        rock.MaxHeight = rMaxHeight;
                        rockChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Rock Size
                if (ImGui.TreeNode("Size##rock"))
                {
                    float minSize = rock.MinSize;
                    if (ImGui.SliderFloat("Min Size", ref minSize, 0.1f, 5.0f))
                    {
                        rock.MinSize = minSize;
                        if (rock.MaxSize < minSize) rock.MaxSize = minSize;
                        rockChanged = true;
                    }

                    float maxSize = rock.MaxSize;
                    if (ImGui.SliderFloat("Max Size", ref maxSize, 0.1f, 5.0f))
                    {
                        rock.MaxSize = maxSize;
                        if (rock.MinSize > maxSize) rock.MinSize = maxSize;
                        rockChanged = true;
                    }

                    float sizeVar = rock.SizeVariation;
                    if (ImGui.SliderFloat("Size Variation", ref sizeVar, 0.0f, 1.0f))
                    {
                        rock.SizeVariation = sizeVar;
                        rockChanged = true;
                    }

                    float flatten = rock.FlattenY;
                    if (ImGui.SliderFloat("Flatten (Y)", ref flatten, 0.0f, 0.8f))
                    {
                        rock.FlattenY = flatten;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Squash rocks vertically (0=round, 0.8=flat boulders)");

                    ImGui.TreePop();
                }

                // Rock Shape - Noise
                if (ImGui.TreeNode("Shape (Noise)##rock"))
                {
                    float noiseFreq = rock.NoiseFrequency;
                    if (ImGui.SliderFloat("Noise Frequency", ref noiseFreq, 0.5f, 8.0f))
                    {
                        rock.NoiseFrequency = noiseFreq;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Higher = more bumpy detail");

                    float noiseAmp = rock.NoiseAmplitude;
                    if (ImGui.SliderFloat("Noise Amplitude", ref noiseAmp, 0.0f, 0.8f))
                    {
                        rock.NoiseAmplitude = noiseAmp;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Displacement strength");

                    int octaves = rock.NoiseOctaves;
                    if (ImGui.SliderInt("Octaves", ref octaves, 1, 5))
                    {
                        rock.NoiseOctaves = octaves;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("FBM detail layers (more = finer detail)");

                    float lacunarity = rock.NoiseLacunarity;
                    if (ImGui.SliderFloat("Lacunarity", ref lacunarity, 1.5f, 4.0f))
                    {
                        rock.NoiseLacunarity = lacunarity;
                        rockChanged = true;
                    }

                    float persistence = rock.NoisePersistence;
                    if (ImGui.SliderFloat("Persistence", ref persistence, 0.2f, 0.8f))
                    {
                        rock.NoisePersistence = persistence;
                        rockChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Rock Shape - Features
                if (ImGui.TreeNode("Shape (Features)##rock"))
                {
                    float sharpness = rock.Sharpness;
                    if (ImGui.SliderFloat("Sharpness", ref sharpness, 0.0f, 1.0f))
                    {
                        rock.Sharpness = sharpness;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edge sharpness (0=smooth, 1=jagged)");

                    float facet = rock.FacetStrength;
                    if (ImGui.SliderFloat("Facet Strength", ref facet, 0.0f, 1.0f))
                    {
                        rock.FacetStrength = facet;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Flat faces emphasis (0=round, 1=very faceted)");

                    float crackDepth = rock.CrackDepth;
                    if (ImGui.SliderFloat("Crack Depth", ref crackDepth, 0.0f, 0.5f))
                    {
                        rock.CrackDepth = crackDepth;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Voronoi-based crevice depth");

                    float crackScale = rock.CrackScale;
                    if (ImGui.SliderFloat("Crack Scale", ref crackScale, 0.5f, 8.0f))
                    {
                        rock.CrackScale = crackScale;
                        rockChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Colors
                if (ImGui.TreeNode("Colors##rock"))
                {
                    var baseCol = new System.Numerics.Vector3(rock.BaseColor[0], rock.BaseColor[1], rock.BaseColor[2]);
                    if (ImGui.ColorEdit3("Base Color##rock", ref baseCol))
                    {
                        rock.BaseColor = new float[] { baseCol.X, baseCol.Y, baseCol.Z, 1.0f };
                        rockChanged = true;
                    }

                    var darkCol = new System.Numerics.Vector3(rock.DarkColor[0], rock.DarkColor[1], rock.DarkColor[2]);
                    if (ImGui.ColorEdit3("Dark Color (Crevices)", ref darkCol))
                    {
                        rock.DarkColor = new float[] { darkCol.X, darkCol.Y, darkCol.Z, 1.0f };
                        rockChanged = true;
                    }

                    var highlightCol = new System.Numerics.Vector3(rock.HighlightColor[0], rock.HighlightColor[1], rock.HighlightColor[2]);
                    if (ImGui.ColorEdit3("Highlight Color", ref highlightCol))
                    {
                        rock.HighlightColor = new float[] { highlightCol.X, highlightCol.Y, highlightCol.Z, 1.0f };
                        rockChanged = true;
                    }

                    float colorVar = rock.ColorVariation;
                    if (ImGui.SliderFloat("Color Variation##rock", ref colorVar, 0.0f, 0.4f))
                    {
                        rock.ColorVariation = colorVar;
                        rockChanged = true;
                    }

                    ImGui.Separator();

                    float roughness = rock.Roughness;
                    if (ImGui.SliderFloat("Roughness##rock", ref roughness, 0.0f, 1.0f))
                    {
                        rock.Roughness = roughness;
                        rockChanged = true;
                    }

                    float metallic = rock.Metallic;
                    if (ImGui.SliderFloat("Metallic##rock", ref metallic, 0.0f, 1.0f))
                    {
                        rock.Metallic = metallic;
                        rockChanged = true;
                    }

                    ImGui.TreePop();
                }

                // Moss
                if (ImGui.TreeNode("Moss/Lichen##rock"))
                {
                    float mossAmt = rock.MossAmount;
                    if (ImGui.SliderFloat("Moss Amount", ref mossAmt, 0.0f, 1.0f))
                    {
                        rock.MossAmount = mossAmt;
                        rockChanged = true;
                    }

                    var mossCol = new System.Numerics.Vector3(rock.MossColor[0], rock.MossColor[1], rock.MossColor[2]);
                    if (ImGui.ColorEdit3("Moss Color", ref mossCol))
                    {
                        rock.MossColor = new float[] { mossCol.X, mossCol.Y, mossCol.Z, 1.0f };
                        rockChanged = true;
                    }

                    float mossBias = rock.MossTopBias;
                    if (ImGui.SliderFloat("Top Bias", ref mossBias, 0.0f, 1.0f))
                    {
                        rock.MossTopBias = mossBias;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Moss prefers upward-facing surfaces");

                    ImGui.TreePop();
                }

                // Embedding & Orientation
                if (ImGui.TreeNode("Placement##rock"))
                {
                    float embed = rock.EmbedDepth;
                    if (ImGui.SliderFloat("Embed Depth", ref embed, 0.0f, 0.8f))
                    {
                        rock.EmbedDepth = embed;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("How deep rocks sink into terrain");

                    float align = rock.AlignToTerrain;
                    if (ImGui.SliderFloat("Align To Terrain", ref align, 0.0f, 1.0f))
                    {
                        rock.AlignToTerrain = align;
                        rockChanged = true;
                    }

                    float rotRand = rock.RotationRandomness;
                    if (ImGui.SliderFloat("Rotation Random", ref rotRand, 0.0f, 1.0f))
                    {
                        rock.RotationRandomness = rotRand;
                        rockChanged = true;
                    }

                    ImGui.TreePop();
                }

                // LOD & Culling
                if (ImGui.TreeNode("LOD & Culling##rock"))
                {
                    float maxDist = rock.MaxRenderDistance;
                    if (ImGui.SliderFloat("Max Distance##rock", ref maxDist, 50f, 500f))
                    {
                        rock.MaxRenderDistance = maxDist;
                        rockChanged = true;
                    }

                    float fadeRange = rock.FadeRange;
                    if (ImGui.SliderFloat("Fade Range##rock", ref fadeRange, 10f, 100f))
                    {
                        rock.FadeRange = fadeRange;
                        rockChanged = true;
                    }

                    float lodBias = rock.LodBias;
                    if (ImGui.SliderFloat("LOD Bias", ref lodBias, 0.3f, 2.0f))
                    {
                        rock.LodBias = lodBias;
                        rockChanged = true;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Detail level multiplier");

                    ImGui.TreePop();
                }

                // Action buttons
                ImGui.Spacing();
                if (ImGui.Button("🗑️ Disable Rock Layer", new Vector2(-1, 0)))
                {
                    layer.IsRockLayer = false;
                    layer.RockProperties = null;
                    rockChanged = true;
                }

                if (rockChanged)
                {
                    // GPU rock parameters are updated every frame
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
