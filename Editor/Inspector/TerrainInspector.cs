using System;
using ImGuiNET;
using Engine.Components;
using Editor.Logging;
using Engine.Scene;
using Engine.Assets;
using Editor.UI;
using Editor.Themes;

namespace Editor.Inspector
{
    /// <summary>
    /// Minimal terrain inspector with Unity-style workflow.
    /// </summary>
    public static class TerrainInspector
    {
        private static TerrainPreview? _preview = null;

        public static void Draw(Entity entity, Terrain terrain)
        {
            ImGui.PushID(terrain.GetHashCode());

            ImGui.Text("Terrain Component");
            ImGui.Separator();

            // === TERRAIN DIMENSIONS ===
            ImGui.Text("Dimensions");

            float width = terrain.TerrainWidth;
            if (ImGui.DragFloat("Width", ref width, 1f, 1f, 10000f))
            {
                terrain.TerrainWidth = width;
            }

            float length = terrain.TerrainLength;
            if (ImGui.DragFloat("Length", ref length, 1f, 1f, 10000f))
            {
                terrain.TerrainLength = length;
            }

            float height = terrain.TerrainHeight;
            if (ImGui.DragFloat("Height", ref height, 0.1f, 0.1f, 10000f))
            {
                terrain.TerrainHeight = height;
            }

            ImGui.Separator();

            // === TERRAIN MODE ===
            ImGui.Text("Terrain Mode");

            int modeIndex = (int)terrain.Mode;
            string[] modeLabels = { "Single Terrain", "Infinite Streaming" };
            int previousMode = modeIndex;

            if (ImGui.Combo("Mode", ref modeIndex, modeLabels, modeLabels.Length))
            {
                var newMode = (Engine.Components.TerrainMode)modeIndex;
                var oldMode = terrain.Mode;

                Console.WriteLine($"");
                Console.WriteLine($"[TerrainInspector] ╔════════════════════════════════════════════════╗");
                Console.WriteLine($"[TerrainInspector] ║   USER CHANGING MODE: {oldMode} → {newMode}");
                Console.WriteLine($"[TerrainInspector] ╚════════════════════════════════════════════════╝");

                // CRITICAL: Call ViewportRenderer to handle the complete mode switch
                // This ensures proper cleanup and regeneration
                var renderer = Editor.Panels.EditorUI.MainViewport?.Renderer;
                if (renderer != null)
                {
                    Console.WriteLine($"[TerrainInspector] Calling ViewportRenderer.HandleTerrainModeChange()");

                    // Set the new mode first
                    terrain.Mode = newMode;

                    // Call the robust mode change handler
                    renderer.HandleTerrainModeChange(terrain);

                    LogManager.LogInfo($"✓ Mode changed to {newMode} - terrain and vegetation regenerated", "TerrainInspector");
                }
                else
                {
                    Console.WriteLine($"[TerrainInspector] ✗ WARNING: ViewportRenderer is null - mode change may be incomplete!");
                    terrain.Mode = newMode;
                    LogManager.LogWarning($"ViewportRenderer not available - terrain may need manual regeneration", "TerrainInspector");
                }
            }

            // Mode-specific help text
            if (terrain.Mode == Engine.Components.TerrainMode.InfiniteStreaming)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f));
                ImGui.TextWrapped("Infinite streaming: Terrain tiles are generated/loaded around camera automatically. Width/Length/Height are templates for tile generation.");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.TextDisabled("Single terrain: Classic mode with fixed bounds.");
            }

            ImGui.Separator();

            // === STREAMING SETTINGS (only for InfiniteStreaming mode) ===
            if (terrain.Mode == Engine.Components.TerrainMode.InfiniteStreaming)
            {
                ImGui.Text("Streaming Settings");

                float tileSize = terrain.StreamingTileSize;
                if (ImGui.DragFloat("Tile Size (m)", ref tileSize, 1f, 50f, 500f))
                {
                    terrain.StreamingTileSize = tileSize;
                }
                ImGui.TextDisabled("World size of each terrain tile");

                int radius = terrain.StreamingRadius;
                if (ImGui.DragInt("Streaming Radius", ref radius, 0.1f, 1, 10))
                {
                    terrain.StreamingRadius = radius;
                }
                int gridSize = (radius * 2 + 1);
                ImGui.TextDisabled($"Loads {gridSize}x{gridSize} = {gridSize * gridSize} tiles around camera");

                int maxLOD = terrain.StreamingMaxLOD;
                if (ImGui.DragInt("Max LOD Levels", ref maxLOD, 0.1f, 1, 5))
                {
                    terrain.StreamingMaxLOD = maxLOD;
                }
                ImGui.TextDisabled($"LOD 0 (highest) to LOD {maxLOD} (lowest)");

                // TODO: Display streaming stats if available
                // (Requires access to ViewportRenderer instance - could be added via inspector parameter)

                ImGui.Separator();
            }

            // === RENDERING SETTINGS ===
            ImGui.Text("Rendering Settings");

            // Render Mode dropdown
            int renderModeIndex = (int)terrain.RenderMode;
            string[] renderModeLabels = { "Fill", "Line (Wireframe)", "Point" };
            if (ImGui.Combo("Render Mode", ref renderModeIndex, renderModeLabels, renderModeLabels.Length))
            {
                terrain.RenderMode = (OpenTK.Graphics.OpenGL4.PolygonMode)renderModeIndex;
            }

            // Cull Mode dropdown
            int cullModeIndex = terrain.CullMode switch
            {
                Engine.Components.TerrainCullingMode.Back => 0,
                Engine.Components.TerrainCullingMode.Front => 1,
                Engine.Components.TerrainCullingMode.FrontAndBack => 2,
                Engine.Components.TerrainCullingMode.None => 3,
                _ => 0
            };
            string[] cullModeLabels = { "Back", "Front", "Front and Back", "None" };
            if (ImGui.Combo("Cull Mode", ref cullModeIndex, cullModeLabels, cullModeLabels.Length))
            {
                terrain.CullMode = cullModeIndex switch
                {
                    0 => Engine.Components.TerrainCullingMode.Back,
                    1 => Engine.Components.TerrainCullingMode.Front,
                    2 => Engine.Components.TerrainCullingMode.FrontAndBack,
                    3 => Engine.Components.TerrainCullingMode.None,
                    _ => Engine.Components.TerrainCullingMode.Back
                };
            }

            // Closed mesh option
            bool closedMesh = terrain.ClosedMesh;
            if (ImGui.Checkbox("Closed Mesh", ref closedMesh))
            {
                terrain.ClosedMesh = closedMesh;
                terrain.GenerateTerrain(); // Regenerate mesh with new setting
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Generate side walls and bottom to create a closed volume");

            // Skirt depth (only show if closed mesh is enabled)
            if (terrain.ClosedMesh)
            {
                float skirtDepth = terrain.SkirtDepth;
                if (ImGui.DragFloat("Skirt Depth", ref skirtDepth, 0.5f, 0.1f, 1000f))
                {
                    terrain.SkirtDepth = skirtDepth;
                    terrain.GenerateTerrain(); // Regenerate mesh with new depth
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Depth of side walls below the lowest terrain point");
            }

            ImGui.Separator();

            // === MESH RESOLUTION ===
            ImGui.Text("Mesh Resolution");
            ImGui.TextDisabled("Higher = smoother but slower");

            // Dropdown with power-of-2 resolutions
            string[] resolutionLabels = { "128", "256", "512", "1024", "2048", "4096" };
            int[] resolutionValues = { 128, 256, 512, 1024, 2048, 4096 };

            int currentMeshRes = terrain.MeshResolution;
            int currentIndex = Array.IndexOf(resolutionValues, currentMeshRes);
            if (currentIndex == -1) currentIndex = 2; // Default to 512 if not found

            string preview = resolutionLabels[currentIndex];
            if (ImGui.BeginCombo("Resolution", preview))
            {
                for (int i = 0; i < resolutionLabels.Length; i++)
                {
                    bool isSelected = (currentIndex == i);
                    if (ImGui.Selectable(resolutionLabels[i], isSelected))
                    {
                        terrain.MeshResolution = resolutionValues[i];
                        currentIndex = i;
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"({terrain.MeshResolution}x{terrain.MeshResolution} = {terrain.MeshResolution * terrain.MeshResolution:N0} vertices)");

            ImGui.Separator();

            // === HEIGHTMAP SOURCE ===
            ImGui.Text("Heightmap Source");

            bool useProcedural = terrain.UseProceduralGeneration;
            if (ImGui.Checkbox("Use Procedural Generation", ref useProcedural))
            {
                terrain.UseProceduralGeneration = useProcedural;
            }

            ImGui.Separator();

            if (terrain.UseProceduralGeneration)
            {
                // === PROCEDURAL GENERATION PARAMETERS ===
                DrawProceduralParameters(terrain);
            }
            else
            {
                // === HEIGHTMAP TEXTURE ===
                var newHeightmap = EditorWidgets.AssetField(
                    "Heightmap Texture",
                    terrain.HeightmapTextureGuid,
                    "Texture2D",
                    "16-bit grayscale PNG recommended",
                    showPreview: true,
                    dragDropHeight: ThemeManager.UI.DragDropLargeHeight);

                if (newHeightmap != terrain.HeightmapTextureGuid)
                {
                    terrain.HeightmapTextureGuid = newHeightmap;
                    if (newHeightmap.HasValue)
                    {
                        LogManager.LogInfo($"Heightmap texture assigned: {AssetDatabase.GetName(newHeightmap.Value)}", "TerrainInspector");
                    }
                }
            }

            ImGui.Separator();

            // === MATERIAL ===
            var newMaterial = EditorWidgets.AssetField(
                "Material",
                terrain.TerrainMaterialGuid,
                "Material",
                "Use TerrainForward shader",
                showPreview: false);
            
            if (newMaterial != terrain.TerrainMaterialGuid)
            {
                terrain.TerrainMaterialGuid = newMaterial;
                if (newMaterial.HasValue)
                {
                    LogManager.LogInfo($"Material assigned: {AssetDatabase.GetName(newMaterial.Value)}", "TerrainInspector");
                }
            }

            ImGui.Separator();

            // Water feature removed: terrain no longer manages a water plane.

            ImGui.Separator();

            // === TERRAIN LAYERS ===
            TerrainLayersUI.DrawTerrainLayers(terrain);

            ImGui.Separator();

            // === VEGETATION ===
            TerrainVegetationUI.DrawVegetationSection(terrain);

            ImGui.Separator();

            // === WEATHER SYSTEM ===
            DrawWeatherSection(terrain);

            ImGui.Separator();

            // === GENERATE/CLEAR BUTTONS (MODE-SPECIFIC) ===
            ImGui.Spacing();
            ImGui.Spacing();

            bool canGenerate = terrain.UseProceduralGeneration || terrain.HeightmapTextureGuid.HasValue;

            if (terrain.Mode == Engine.Components.TerrainMode.InfiniteStreaming)
            {
                // INFINITE STREAMING MODE - different controls
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f));
                ImGui.TextWrapped("Infinite streaming terrain generates tiles automatically around the camera.");
                ImGui.PopStyleColor();

                ImGui.Spacing();

                // Button to clear and regenerate all tiles
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.4f, 0.2f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.9f, 0.5f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.7f, 0.3f, 0.1f, 1f));

                if (ImGui.Button("Clear & Regenerate All Tiles", new System.Numerics.Vector2(-1, 40)))
                {
                    var renderer = Editor.Panels.EditorUI.MainViewport?.Renderer;
                    if (renderer != null)
                    {
                        // Use HandleTerrainModeChange to properly clear and regenerate everything
                        // This ensures tiles, terrain mesh, and vegetation are all properly reset
                        Console.WriteLine("[TerrainInspector] User clicked 'Clear & Regenerate All Tiles' - forcing mode refresh");
                        renderer.HandleTerrainModeChange(terrain);
                        LogManager.LogInfo("All tiles cleared and regenerated", "TerrainInspector");
                    }
                    else
                    {
                        LogManager.LogWarning("ViewportRenderer not available", "TerrainInspector");
                    }
                }

                ImGui.PopStyleColor(3);

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Delete all cached tiles and force immediate regeneration with current parameters.");
                }
            }
            else
            {
                // SINGLE TERRAIN MODE - traditional controls
                if (!canGenerate)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 1f));
                }
                else
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.6f, 0.2f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.7f, 0.3f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.1f, 0.5f, 0.1f, 1f));
                }

                if (ImGui.Button("Generate Terrain", new System.Numerics.Vector2(-1, 40)))
                {
                    if (canGenerate)
                    {
                        try
                        {
                            LogManager.LogInfo("Generating terrain...", "TerrainInspector");
                            terrain.GenerateTerrain();
                            LogManager.LogInfo("Terrain generated successfully!", "TerrainInspector");
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogWarning($"Failed to generate terrain: {ex.Message}", "TerrainInspector");
                            LogManager.LogVerbose(ex.StackTrace ?? "", "TerrainInspector");
                        }
                    }
                }

                ImGui.PopStyleColor(3);

                if (!canGenerate)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), "Enable procedural generation or assign a heightmap texture first!");
                }

                ImGui.Spacing();

                // === CLEAR BUTTON ===
                if (ImGui.Button("Clear Terrain", new System.Numerics.Vector2(-1, 0)))
                {
                    terrain.ClearTerrain();
                    LogManager.LogInfo("Terrain cleared", "TerrainInspector");
                }

                // === CLEAR CACHE BUTTON ===
                if (ImGui.Button("Clear Cache & Regenerate", new System.Numerics.Vector2(-1, 0)))
                {
                    try
                    {
                        // Delete all cache files
                        string cacheDir = System.IO.Path.Combine("Cache", "Terrain");
                        if (System.IO.Directory.Exists(cacheDir))
                        {
                            var files = System.IO.Directory.GetFiles(cacheDir, "*.cache", System.IO.SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                try { System.IO.File.Delete(file); } catch { }
                            }
                            LogManager.LogInfo($"Deleted {files.Length} cache files", "TerrainInspector");
                        }

                        // Clear and regenerate
                        terrain.ClearTerrain();
                        if (canGenerate)
                        {
                            terrain.GenerateTerrain();
                            LogManager.LogInfo("Terrain regenerated with fresh cache!", "TerrainInspector");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"Failed to clear cache: {ex.Message}", "TerrainInspector");
                    }
                }
            }

            ImGui.Separator();

            // === INFO ===
            if (ThemedImGui.TreeNode("Terrain Info"))
            {
                ImGui.TextDisabled($"Width: {terrain.TerrainWidth}m");
                ImGui.TextDisabled($"Length: {terrain.TerrainLength}m");
                ImGui.TextDisabled($"Height: {terrain.TerrainHeight}m");
                ImGui.TextDisabled($"Mesh Resolution: {terrain.MeshResolution}x{terrain.MeshResolution}");
                ImGui.TextDisabled($"Total Vertices: {terrain.MeshResolution * terrain.MeshResolution:N0}");
                ImGui.TextDisabled($"Total Triangles: {(terrain.MeshResolution - 1) * (terrain.MeshResolution - 1) * 2:N0}");
                ImGui.TreePop();
            }

            ImGui.PopID();
        }

        private static void DrawProceduralParameters(Terrain terrain)
        {
            ImGui.PushID("ProceduralParams");

            // === PRESETS ===
            if (ThemedImGui.CollapsingHeader("Terrain Presets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.TextWrapped("Quick start with common terrain types:");
                ImGui.Spacing();

                var presets = Engine.Rendering.Terrain.TerrainPresets.GetAllPresets();
                int presetsPerRow = 3;

                for (int i = 0; i < presets.Length; i++)
                {
                    var preset = presets[i];

                    if (ImGui.Button(preset.Name, new System.Numerics.Vector2(120, 30)))
                    {
                        Engine.Rendering.Terrain.TerrainPresets.ApplyPreset(terrain, preset);
                        LogManager.LogInfo($"Applied preset: {preset.Name}", "TerrainInspector");
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(preset.Description);
                    }

                    // Arrange buttons in rows
                    if ((i + 1) % presetsPerRow != 0 && i < presets.Length - 1)
                    {
                        ImGui.SameLine();
                    }
                }

                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // === 2D PREVIEW ===
            if (ThemedImGui.CollapsingHeader("2D Preview"))
            {
                ImGui.Indent();

                // Preview texture display
                if (_preview != null && _preview.TextureId != 0)
                {
                    ImGui.Image(
                        (IntPtr)_preview.TextureId,
                        new System.Numerics.Vector2(256, 256),
                        new System.Numerics.Vector2(0, 1),  // UV start (flip Y)
                        new System.Numerics.Vector2(1, 0)); // UV end
                }
                else
                {
                    ImGui.TextDisabled("No preview generated yet");
                }

                ImGui.Spacing();

                if (ImGui.Button("Generate Preview", new System.Numerics.Vector2(-1, 0)))
                {
                    try
                    {
                        // Generate heightmap for preview
                        var parameters = new Engine.Rendering.Terrain.ProceduralHeightmapParams
                        {
                            Seed = terrain.ProceduralSeed,
                            NoiseScale = terrain.NoiseScale,
                            Octaves = terrain.Octaves,
                            Persistence = terrain.Persistence,
                            Lacunarity = terrain.Lacunarity,
                            OffsetX = terrain.NoiseOffsetX,
                            OffsetY = terrain.NoiseOffsetY,
                            NoiseType = terrain.NoiseType,
                            IslandMode = terrain.IslandMode,
                            IslandFalloff = terrain.IslandFalloff,
                            EnableTerracing = terrain.EnableTerracing,
                            TerraceCount = terrain.TerraceCount,
                            HeightMultiplier = terrain.HeightMultiplier,
                            HeightPower = terrain.HeightPower,
                            UseDomainWarping = terrain.UseDomainWarping,
                            DomainWarpStrength = terrain.DomainWarpStrength,
                            ApplyErosion = terrain.ApplyErosion,
                            HydraulicIterations = terrain.HydraulicIterations,
                            HydraulicStrength = terrain.HydraulicStrength,
                            ThermalIterations = terrain.ThermalIterations,
                            ThermalTalusAngle = terrain.ThermalTalusAngle,
                            ThermalStrength = terrain.ThermalStrength
                        };

                        // Generate at preview resolution (512x512)
                        var heightmap = Engine.Rendering.Terrain.ProceduralHeightmapGenerator.Generate(
                            512, 512, parameters);

                        // Handle blending if enabled
                        if (terrain.BlendWithTexture && terrain.HeightmapTextureGuid.HasValue)
                        {
                            var textureMap = Engine.Rendering.HeightmapLoader.LoadHeightmapFromTexture(
                                terrain.HeightmapTextureGuid.Value);

                            if (textureMap != null)
                            {
                                heightmap = Engine.Rendering.Terrain.HeightmapBlending.Blend(
                                    textureMap, heightmap, terrain.BlendMode, terrain.BlendStrength);
                            }
                        }

                        // Create or update preview
                        if (_preview == null)
                        {
                            _preview = new TerrainPreview();
                        }

                        _preview.GeneratePreview(heightmap, 256);
                        LogManager.LogInfo("Preview generated successfully!", "TerrainInspector");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"Failed to generate preview: {ex.Message}", "TerrainInspector");
                    }
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Generate a 2D preview of the heightmap (512x512 resolution)");

                ImGui.Unindent();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // === SEED ===
            int seed = terrain.ProceduralSeed;
            if (ImGui.DragInt("Seed", ref seed))
            {
                terrain.ProceduralSeed = seed;
            }
            ImGui.SameLine();
            if (ImGui.Button("Randomize"))
            {
                terrain.ProceduralSeed = new Random().Next();
            }

            ImGui.Spacing();

            // === NOISE TYPE ===
            ImGui.Text("Noise Type");
            string[] noiseTypes = { "Fractal", "Ridged", "Billow" };
            int currentNoiseType = (int)terrain.NoiseType;
            if (ImGui.Combo("##NoiseType", ref currentNoiseType, noiseTypes, noiseTypes.Length))
            {
                terrain.NoiseType = (Engine.Rendering.Terrain.NoiseType)currentNoiseType;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Fractal: Natural hills and valleys");
                ImGui.Text("Ridged: Mountain ridges");
                ImGui.Text("Billow: Cloud-like formations");
                ImGui.EndTooltip();
            }

            ImGui.Spacing();

            // === BASIC PARAMETERS ===
            if (ThemedImGui.CollapsingHeader("Basic Parameters", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                float noiseScale = terrain.NoiseScale;
                if (ImGui.DragFloat("Scale", ref noiseScale, 0.5f, 1f, 500f))
                {
                    terrain.NoiseScale = noiseScale;
                }
                if (ImGui.IsItemHovered())
                {
                    if (terrain.Mode == Engine.Components.TerrainMode.InfiniteStreaming)
                    {
                        ImGui.SetTooltip($"Zoom level of the noise (lower = zoomed in)\n\nFor streaming tiles of {terrain.StreamingTileSize}m, use Scale 50-200 for good results.\nYour current scale ({noiseScale:F1}) might be {(noiseScale < 20 ? "too small (spiky)" : "good")}.");
                    }
                    else
                    {
                        ImGui.SetTooltip("Zoom level of the noise (lower = zoomed in)");
                    }
                }

                int octaves = terrain.Octaves;
                if (ImGui.SliderInt("Octaves", ref octaves, 1, 8))
                {
                    terrain.Octaves = octaves;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Number of detail layers (more = more detail)");

                float persistence = terrain.Persistence;
                if (ImGui.SliderFloat("Persistence", ref persistence, 0.1f, 1f))
                {
                    terrain.Persistence = persistence;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Amplitude falloff per octave (lower = smoother)");

                float lacunarity = terrain.Lacunarity;
                if (ImGui.SliderFloat("Lacunarity", ref lacunarity, 1f, 4f))
                {
                    terrain.Lacunarity = lacunarity;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Frequency multiplier per octave (higher = more variation)");

                ImGui.Unindent();
            }

            // === OFFSET ===
            if (ThemedImGui.CollapsingHeader("Offset"))
            {
                ImGui.Indent();

                float offsetX = terrain.NoiseOffsetX;
                if (ImGui.DragFloat("Offset X", ref offsetX, 0.1f))
                {
                    terrain.NoiseOffsetX = offsetX;
                }

                float offsetY = terrain.NoiseOffsetY;
                if (ImGui.DragFloat("Offset Y", ref offsetY, 0.1f))
                {
                    terrain.NoiseOffsetY = offsetY;
                }

                ImGui.Unindent();
            }

            // === BLEND WITH TEXTURE ===
            if (ThemedImGui.CollapsingHeader("Blend with Texture Heightmap"))
            {
                ImGui.Indent();

                bool blendWithTexture = terrain.BlendWithTexture;
                if (ImGui.Checkbox("Enable Blending", ref blendWithTexture))
                {
                    terrain.BlendWithTexture = blendWithTexture;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Combine procedural generation with a texture heightmap");
                    ImGui.Text("Useful for adding procedural details to existing terrain");
                    ImGui.EndTooltip();
                }

                if (terrain.BlendWithTexture)
                {
                    // Show texture selector
                    var heightmapGuid = EditorWidgets.AssetField(
                        "Base Heightmap",
                        terrain.HeightmapTextureGuid,
                        "Texture2D",
                        "Base terrain to blend with procedural",
                        showPreview: true,
                        dragDropHeight: 80);

                    if (heightmapGuid != terrain.HeightmapTextureGuid)
                    {
                        terrain.HeightmapTextureGuid = heightmapGuid;
                    }

                    ImGui.Spacing();

                    // Blend mode selector
                    string[] blendModes = { "Replace", "Add", "Multiply", "Overlay", "Screen", "Min", "Max", "Average" };
                    int currentBlendMode = (int)terrain.BlendMode;
                    if (ImGui.Combo("Blend Mode", ref currentBlendMode, blendModes, blendModes.Length))
                    {
                        terrain.BlendMode = (Engine.Rendering.Terrain.HeightmapBlendMode)currentBlendMode;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Add: Base + Procedural");
                        ImGui.Text("Multiply: Base * Procedural");
                        ImGui.Text("Overlay: Photoshop-style overlay");
                        ImGui.Text("Screen: Brightening blend");
                        ImGui.EndTooltip();
                    }

                    // Blend strength
                    float blendStrength = terrain.BlendStrength;
                    if (ImGui.SliderFloat("Blend Strength", ref blendStrength, 0f, 1f))
                    {
                        terrain.BlendStrength = blendStrength;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How much to blend (0=base only, 1=full blend)");
                }

                ImGui.Unindent();
            }

            // === DOMAIN WARPING ===
            if (ThemedImGui.CollapsingHeader("Domain Warping"))
            {
                ImGui.Indent();

                bool useDomainWarping = terrain.UseDomainWarping;
                if (ImGui.Checkbox("Enable Domain Warping", ref useDomainWarping))
                {
                    terrain.UseDomainWarping = useDomainWarping;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Distorts noise space for more organic patterns");
                    ImGui.Text("Highly recommended for natural-looking terrain!");
                    ImGui.EndTooltip();
                }

                if (terrain.UseDomainWarping)
                {
                    float warpStrength = terrain.DomainWarpStrength;
                    if (ImGui.SliderFloat("Warp Strength", ref warpStrength, 0f, 2f))
                    {
                        terrain.DomainWarpStrength = warpStrength;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How much to distort the noise (0=none, 2=extreme)");
                }

                ImGui.Unindent();
            }

            // === HEIGHT ADJUSTMENT ===
            if (ThemedImGui.CollapsingHeader("Height Adjustment"))
            {
                ImGui.Indent();

                float heightMultiplier = terrain.HeightMultiplier;
                if (ImGui.SliderFloat("Multiplier", ref heightMultiplier, 0.1f, 2f))
                {
                    terrain.HeightMultiplier = heightMultiplier;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Overall height scale");

                float heightPower = terrain.HeightPower;
                if (ImGui.SliderFloat("Power Curve", ref heightPower, 0.5f, 3f))
                {
                    terrain.HeightPower = heightPower;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("< 1.0: More plateaus");
                    ImGui.Text("= 1.0: Linear");
                    ImGui.Text("> 1.0: More valleys");
                    ImGui.EndTooltip();
                }

                ImGui.Unindent();
            }

            // === ISLAND MODE ===
            if (ThemedImGui.CollapsingHeader("Island Mode"))
            {
                ImGui.Indent();

                bool islandMode = terrain.IslandMode;
                if (ImGui.Checkbox("Enable Island Mode", ref islandMode))
                {
                    terrain.IslandMode = islandMode;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Creates circular falloff for island generation");

                if (terrain.IslandMode)
                {
                    float falloff = terrain.IslandFalloff;
                    if (ImGui.SliderFloat("Falloff", ref falloff, 1f, 10f))
                    {
                        terrain.IslandFalloff = falloff;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How quickly the terrain drops off at edges");
                }

                ImGui.Unindent();
            }

            // === EROSION SIMULATION ===
            if (ThemedImGui.CollapsingHeader("Erosion Simulation"))
            {
                ImGui.Indent();

                bool applyErosion = terrain.ApplyErosion;
                if (ImGui.Checkbox("Enable Erosion", ref applyErosion))
                {
                    terrain.ApplyErosion = applyErosion;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Simulate water and gravity erosion for realistic terrain");
                    ImGui.Text("WARNING: Can be slow with high iterations!");
                    ImGui.EndTooltip();
                }

                if (terrain.ApplyErosion)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.8f, 0.2f, 1f), "Hydraulic Erosion (Water Flow)");

                    int hydraulicIterations = terrain.HydraulicIterations;
                    if (ImGui.SliderInt("Hydraulic Iterations", ref hydraulicIterations, 1000, 200000))
                    {
                        terrain.HydraulicIterations = hydraulicIterations;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Number of water droplets to simulate (more = more detail, slower)");

                    float hydraulicStrength = terrain.HydraulicStrength;
                    if (ImGui.SliderFloat("Hydraulic Strength", ref hydraulicStrength, 0.1f, 1f))
                    {
                        terrain.HydraulicStrength = hydraulicStrength;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How much erosion to apply per droplet");

                    ImGui.Spacing();
                    ImGui.TextColored(new System.Numerics.Vector4(0.8f, 1f, 0.8f, 1f), "Thermal Erosion (Gravity Slides)");

                    int thermalIterations = terrain.ThermalIterations;
                    if (ImGui.SliderInt("Thermal Iterations", ref thermalIterations, 1, 20))
                    {
                        terrain.ThermalIterations = thermalIterations;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Number of thermal erosion passes (creates natural slopes)");

                    float talusAngle = terrain.ThermalTalusAngle;
                    if (ImGui.SliderFloat("Talus Angle", ref talusAngle, 0.01f, 0.2f))
                    {
                        terrain.ThermalTalusAngle = talusAngle;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Maximum stable slope angle (lower = gentler slopes)");

                    float thermalStrength = terrain.ThermalStrength;
                    if (ImGui.SliderFloat("Thermal Strength", ref thermalStrength, 0.1f, 1f))
                    {
                        terrain.ThermalStrength = thermalStrength;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("How much material slides down per iteration");

                    ImGui.Spacing();
                    ImGui.TextWrapped("Tip: Use thermal erosion for smooth slopes, hydraulic for valleys and rivers.");
                }

                ImGui.Unindent();
            }

            // === TERRACING ===
            if (ThemedImGui.CollapsingHeader("Terracing"))
            {
                ImGui.Indent();

                bool enableTerracing = terrain.EnableTerracing;
                if (ImGui.Checkbox("Enable Terracing", ref enableTerracing))
                {
                    terrain.EnableTerracing = enableTerracing;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Creates step-like layers for stylized look");

                if (terrain.EnableTerracing)
                {
                    int terraceCount = terrain.TerraceCount;
                    if (ImGui.SliderInt("Terrace Count", ref terraceCount, 2, 20))
                    {
                        terrain.TerraceCount = terraceCount;
                    }
                }

                ImGui.Unindent();
            }

            // === EXPORT BUTTON ===
            ImGui.Spacing();
            if (ImGui.Button("Export to PNG", new System.Numerics.Vector2(-1, 0)))
            {
                try
                {
                    // Generate heightmap
                    var parameters = new Engine.Rendering.Terrain.ProceduralHeightmapParams
                    {
                        Seed = terrain.ProceduralSeed,
                        NoiseScale = terrain.NoiseScale,
                        Octaves = terrain.Octaves,
                        Persistence = terrain.Persistence,
                        Lacunarity = terrain.Lacunarity,
                        OffsetX = terrain.NoiseOffsetX,
                        OffsetY = terrain.NoiseOffsetY,
                        NoiseType = terrain.NoiseType,
                        IslandMode = terrain.IslandMode,
                        IslandFalloff = terrain.IslandFalloff,
                        EnableTerracing = terrain.EnableTerracing,
                        TerraceCount = terrain.TerraceCount,
                        HeightMultiplier = terrain.HeightMultiplier,
                        HeightPower = terrain.HeightPower,
                        UseDomainWarping = terrain.UseDomainWarping,
                        DomainWarpStrength = terrain.DomainWarpStrength,
                        ApplyErosion = terrain.ApplyErosion,
                        HydraulicIterations = terrain.HydraulicIterations,
                        HydraulicStrength = terrain.HydraulicStrength,
                        ThermalIterations = terrain.ThermalIterations,
                        ThermalTalusAngle = terrain.ThermalTalusAngle,
                        ThermalStrength = terrain.ThermalStrength
                    };

                    var heightmap = Engine.Rendering.Terrain.ProceduralHeightmapGenerator.Generate(
                        terrain.MeshResolution, terrain.MeshResolution, parameters);

                    // Save to file
                    string exportPath = $"Assets/Heightmaps/procedural_{terrain.ProceduralSeed}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string? directory = System.IO.Path.GetDirectoryName(exportPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }

                    Engine.Rendering.Terrain.ProceduralHeightmapGenerator.ExportToPng(heightmap, exportPath);
                    LogManager.LogInfo($"Exported procedural heightmap to {exportPath}", "TerrainInspector");
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to export heightmap: {ex.Message}", "TerrainInspector");
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Export current procedural heightmap to 16-bit PNG file");

            ImGui.PopID();
        }

        private static void DrawWeatherSection(Terrain terrain)
        {
            // Weather has been moved to the WeatherComponent
            // Show a helpful message to guide users
            bool weatherOpen = ThemedImGui.CollapsingHeader("🌤️ Weather & Environment (MOVED)");
            if (!weatherOpen) return;

            ImGui.Indent();
            ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.8f, 0.2f, 1.0f), "⚠️ Weather system has been migrated!");
            ImGui.Spacing();
            ImGui.TextWrapped("Weather parameters (wind, rain, snow) have been moved to the new WeatherComponent system.");
            ImGui.Spacing();
            ImGui.TextWrapped("To control weather:");
            ImGui.BulletText("Create a new Entity");
            ImGui.BulletText("Add WeatherComponent to it");
            ImGui.BulletText("Configure weather in the Inspector");
            ImGui.Spacing();
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f), "This provides better separation of concerns and allows scene-wide weather control.");
            ImGui.Unindent();
        }
    }
}
