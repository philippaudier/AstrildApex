using System;
using ImGuiNET;
using Engine.Components;
using Engine.Scene;
using Engine.Assets;

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

            int meshRes = terrain.MeshResolution;
            if (ImGui.SliderInt("Resolution", ref meshRes, 32, 1024))
            {
                terrain.MeshResolution = meshRes;
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"({meshRes}x{meshRes} vertices)");

            // Quick preset buttons
            if (ImGui.Button("Low (128)"))
            {
                terrain.MeshResolution = 128;
            }
            ImGui.SameLine();
            if (ImGui.Button("Med (256)"))
            {
                terrain.MeshResolution = 256;
            }
            ImGui.SameLine();
            if (ImGui.Button("High (512)"))
            {
                terrain.MeshResolution = 512;
            }

            ImGui.Separator();

            // === HEIGHTMAP TEXTURE ===
            ImGui.Text("Heightmap Texture");
            ImGui.TextDisabled("16-bit grayscale PNG recommended");

            // Display current heightmap with drag-drop zone
            if (terrain.HeightmapTextureGuid.HasValue)
            {
                if (AssetDatabase.TryGet(terrain.HeightmapTextureGuid.Value, out var record))
                {
                        ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), $"\u2713 {System.IO.Path.GetFileName(record.Path)}");

                        // Show a small preview of the heightmap texture
                        try
                        {
                            Engine.Rendering.TextureCache.Initialize();
                            int handle = Engine.Rendering.TextureCache.GetOrLoad(terrain.HeightmapTextureGuid.Value, g => AssetDatabase.TryGet(g, out var r) ? r.Path : null);
                            var size = new System.Numerics.Vector2(200, 120);
                            // If texture not yet uploaded, handle may be White1x1 - that's OK
                            ImGui.Image((IntPtr)handle, size, new System.Numerics.Vector2(0, 1), new System.Numerics.Vector2(1, 0));
                        }
                        catch { }
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), "Heightmap asset not found!");
                }

                if (ImGui.Button("Clear Heightmap", new System.Numerics.Vector2(-1, 0)))
                {
                    terrain.HeightmapTextureGuid = null;
                }
            }
            else
            {
                // Large clickable drag-drop zone
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.4f, 1f));
                ImGui.Button("Drag & Drop Heightmap Here", new System.Numerics.Vector2(-1, 50));
                ImGui.PopStyleColor(2);

                // Drag-drop target on the button
                if (ImGui.BeginDragDropTarget())
                {
                    // Accept the multi-asset payload produced by the Assets panel and take the first GUID
                    unsafe
                    {
                        var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                        if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                        {
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
                            var assetGuid = new Guid(span.Slice(0, 16));

                            if (AssetDatabase.TryGet(assetGuid, out var rec) && rec.Type == "Texture2D")
                            {
                                terrain.HeightmapTextureGuid = assetGuid;
                                Console.WriteLine($"[TerrainInspector] Heightmap texture assigned: {rec.Path}");
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
            }

            ImGui.Separator();

            // === MATERIAL ===
            ImGui.Text("Material");
            ImGui.TextDisabled("Use TerrainForward shader");

            // Display current material with drag-drop zone
            if (terrain.TerrainMaterialGuid.HasValue)
            {
                if (AssetDatabase.TryGet(terrain.TerrainMaterialGuid.Value, out var record))
                {
                    ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), $"✓ {System.IO.Path.GetFileName(record.Path)}");
                }
                else
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), "Material asset not found!");
                }

                if (ImGui.Button("Clear Material", new System.Numerics.Vector2(-1, 0)))
                {
                    terrain.TerrainMaterialGuid = null;
                }
            }
            else
            {
                // Large clickable drag-drop zone
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.3f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.4f, 1f));
                ImGui.Button("Drag & Drop Material Here", new System.Numerics.Vector2(-1, 50));
                ImGui.PopStyleColor(2);

                // Drag-drop target on the button
                if (ImGui.BeginDragDropTarget())
                {
                    // Accept the multi-asset payload produced by the Assets panel and take the first GUID
                    unsafe
                    {
                        var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                        if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                        {
                            var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
                            var assetGuid = new Guid(span.Slice(0, 16));

                            if (AssetDatabase.TryGet(assetGuid, out var rec) && rec.Type == "Material")
                            {
                                terrain.TerrainMaterialGuid = assetGuid;
                                Console.WriteLine($"[TerrainInspector] Material assigned: {rec.Path}");
                            }
                        }
                    }
                    ImGui.EndDragDropTarget();
                }
            }

            ImGui.Separator();

            // === WATER ===
            ImGui.Text("Water");
            ImGui.TextDisabled("Add a water plane to the terrain");

            bool enableWater = terrain.EnableWater;
            if (ImGui.Checkbox("Enable Water", ref enableWater))
            {
                terrain.EnableWater = enableWater;
                terrain.UpdateWaterPlane();
            }

            if (terrain.EnableWater)
            {
                // Water height slider
                float waterHeight = terrain.WaterHeight;
                if (ImGui.SliderFloat("Water Height", ref waterHeight, 0f, terrain.TerrainHeight))
                {
                    terrain.WaterHeight = waterHeight;
                    terrain.UpdateWaterPlane();
                }

                // Water material drag-drop
                ImGui.Text("Water Material");
                if (terrain.WaterMaterialGuid.HasValue)
                {
                    if (AssetDatabase.TryGet(terrain.WaterMaterialGuid.Value, out var record))
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), $"✓ {System.IO.Path.GetFileName(record.Path)}");
                    }
                    else
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), "Material asset not found!");
                    }

                    if (ImGui.Button("Clear Water Material", new System.Numerics.Vector2(-1, 0)))
                    {
                        terrain.WaterMaterialGuid = null;
                    }
                }
                else
                {
                    // Large clickable drag-drop zone
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.3f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.4f, 1f));
                    ImGui.Button("Drag & Drop Water Material", new System.Numerics.Vector2(-1, 40));
                    ImGui.PopStyleColor(2);

                    // Drag-drop target on the button
                    if (ImGui.BeginDragDropTarget())
                    {
                        // Accept the multi-asset payload produced by the Assets panel and take the first GUID
                        unsafe
                        {
                            var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                            if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                            {
                                var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
                                var assetGuid = new Guid(span.Slice(0, 16));

                                if (AssetDatabase.TryGet(assetGuid, out var rec) && rec.Type == "Material")
                                {
                                    terrain.WaterMaterialGuid = assetGuid;
                                    Console.WriteLine($"[TerrainInspector] Water material assigned: {rec.Path}");
                                }
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }
                }
            }

            ImGui.Separator();

            // === TERRAIN LAYERS ===
            TerrainLayersUI.DrawTerrainLayers(terrain);

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
                        Console.WriteLine("[TerrainInspector] Generating terrain...");
                        terrain.GenerateTerrain();
                        Console.WriteLine("[TerrainInspector] Terrain generated successfully!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TerrainInspector] Failed to generate terrain: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
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
                Console.WriteLine("[TerrainInspector] Terrain cleared");
            }

            ImGui.Separator();

            // === INFO ===
            if (ImGui.TreeNode("Terrain Info"))
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
    }
}
