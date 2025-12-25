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

            // === GENERATE BUTTON ===
            ImGui.Spacing();
            ImGui.Spacing();

            bool canGenerate = terrain.HeightmapTextureGuid.HasValue;

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
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), "Assign a heightmap texture first!");
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
                        var files = System.IO.Directory.GetFiles(cacheDir, "*.cache");
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
