using System;
using System.Numerics;
using ImGuiNET;
using Engine.Components;
using Engine.Assets;

namespace Editor.Inspector
{
    /// <summary>
    /// UI helper for terrain layers with material-based system
    /// </summary>
    public static class TerrainLayersUI
    {
        public static void DrawTerrainLayers(Terrain terrain)
        {
            if (!ImGui.CollapsingHeader("Terrain Layers", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            ImGui.TextDisabled("Materials will be blended based on height and slope");
            ImGui.Spacing();

            // Get terrain material to access layers (using AssetDatabase for cache consistency)
            MaterialAsset? terrainMat = null;
            if (terrain.TerrainMaterialGuid.HasValue)
            {
                try
                {
                    terrainMat = AssetDatabase.LoadMaterial(terrain.TerrainMaterialGuid.Value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TerrainLayersUI] Failed to load terrain material: {ex.Message}");
                }
            }

            if (terrainMat == null)
            {
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.7f, 0.3f, 1f), 
                    "Assign a TerrainForward material to configure layers");
                return;
            }

            // Ensure TerrainLayers array exists
            if (terrainMat.TerrainLayers == null)
            {
                terrainMat.TerrainLayers = Array.Empty<TerrainLayer>();
            }

            // Add layer button
            if (ImGui.Button("Add Layer") && terrainMat.TerrainLayers.Length < 8)
            {
                var newLayers = new TerrainLayer[terrainMat.TerrainLayers.Length + 1];
                Array.Copy(terrainMat.TerrainLayers, newLayers, terrainMat.TerrainLayers.Length);
                newLayers[terrainMat.TerrainLayers.Length] = new TerrainLayer
                {
                    Name = $"Layer {terrainMat.TerrainLayers.Length}",
                    Priority = terrainMat.TerrainLayers.Length,
                    Strength = 1.0f
                };
                terrainMat.TerrainLayers = newLayers;
                
                // Save the material
                SaveMaterial(terrain, terrainMat);
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"({terrainMat.TerrainLayers.Length}/8 layers)");

            ImGui.Spacing();

            // Draw each layer
            for (int i = 0; i < terrainMat.TerrainLayers.Length; i++)
            {
                ImGui.PushID(i);
                var layer = terrainMat.TerrainLayers[i];
                bool changed = false;

                // Layer header with delete button
                bool nodeOpen = ImGui.TreeNodeEx($"{layer.Name}##layer{i}", ImGuiTreeNodeFlags.DefaultOpen);
                
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 60);
                if (ImGui.SmallButton("Delete"))
                {
                    var newLayers = new TerrainLayer[terrainMat.TerrainLayers.Length - 1];
                    int destIndex = 0;
                    for (int j = 0; j < terrainMat.TerrainLayers.Length; j++)
                    {
                        if (j != i)
                        {
                            newLayers[destIndex++] = terrainMat.TerrainLayers[j];
                        }
                    }
                    terrainMat.TerrainLayers = newLayers;
                    SaveMaterial(terrain, terrainMat);
                    
                    ImGui.PopID();
                    break;
                }

                if (nodeOpen)
                {
                    // Layer name
                    string name = layer.Name ?? "";
                    if (ImGui.InputText("Name", ref name, 256))
                    {
                        layer.Name = name;
                        changed = true;
                    }

                    // Material selection
                    ImGui.Text("Material");
                    if (layer.Material.HasValue)
                    {
                        if (AssetDatabase.TryGet(layer.Material.Value, out var matRec))
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), 
                                $"✓ {System.IO.Path.GetFileName(matRec.Path)}");
                        }
                        else
                        {
                            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), 
                                "Material not found!");
                        }
                        
                        if (ImGui.SmallButton("Clear##material"))
                        {
                            layer.Material = null;
                            changed = true;
                        }
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.3f, 1f));
                        ImGui.Button("Drag Material Here", new System.Numerics.Vector2(-1, 30));
                        ImGui.PopStyleColor();
                        
                        // Drag-drop for material
                        if (ImGui.BeginDragDropTarget())
                        {
                            unsafe
                            {
                                var payload = ImGui.AcceptDragDropPayload("ASSET_MULTI");
                                if (payload.NativePtr != null && payload.Data != IntPtr.Zero && payload.DataSize >= 16)
                                {
                                    var span = new ReadOnlySpan<byte>((void*)payload.Data, (int)payload.DataSize);
                                    var assetGuid = new Guid(span.Slice(0, 16));

                                    if (AssetDatabase.TryGet(assetGuid, out var rec) && rec.Type == "Material")
                                    {
                                        layer.Material = assetGuid;
                                        changed = true;
                                    }
                                }
                            }
                            ImGui.EndDragDropTarget();
                        }
                    }

                    // UV Tiling
                    var tiling = layer.Tiling ?? new float[] { 1f, 1f };
                    var tilingVec = new Vector2(tiling[0], tiling[1]);
                    if (ImGui.DragFloat2("Tiling", ref tilingVec, 0.1f, 0.1f, 100f))
                    {
                        layer.Tiling = new float[] { tilingVec.X, tilingVec.Y };
                        changed = true;
                    }

                    // UV Offset
                    var offset = layer.Offset ?? new float[] { 0f, 0f };
                    var offsetVec = new Vector2(offset[0], offset[1]);
                    if (ImGui.DragFloat2("Offset", ref offsetVec, 0.01f))
                    {
                        layer.Offset = new float[] { offsetVec.X, offsetVec.Y };
                        changed = true;
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Text("Blending");

                    // Height range
                    float heightMin = layer.HeightMin;
                    float heightMax = layer.HeightMax;
                    if (ImGui.DragFloatRange2("Height Range", ref heightMin, ref heightMax, 1f, -1000f, 1000f))
                    {
                        layer.HeightMin = heightMin;
                        layer.HeightMax = heightMax;
                        changed = true;
                    }

                    float heightBlend = layer.HeightBlendDistance;
                    if (ImGui.DragFloat("Height Blend", ref heightBlend, 0.1f, 0f, 50f))
                    {
                        layer.HeightBlendDistance = heightBlend;
                        changed = true;
                    }

                    // Slope range
                    float slopeMin = layer.SlopeMinDeg;
                    float slopeMax = layer.SlopeMaxDeg;
                    if (ImGui.DragFloatRange2("Slope Range (°)", ref slopeMin, ref slopeMax, 1f, 0f, 90f))
                    {
                        layer.SlopeMinDeg = slopeMin;
                        layer.SlopeMaxDeg = slopeMax;
                        changed = true;
                    }

                    float slopeBlend = layer.SlopeBlendDistance;
                    if (ImGui.DragFloat("Slope Blend", ref slopeBlend, 0.1f, 0f, 45f))
                    {
                        layer.SlopeBlendDistance = slopeBlend;
                        changed = true;
                    }

                    // Strength
                    float strength = layer.Strength;
                    if (ImGui.SliderFloat("Strength", ref strength, 0f, 1f))
                    {
                        layer.Strength = strength;
                        changed = true;
                    }

                    // Priority
                    int priority = layer.Priority;
                    if (ImGui.DragInt("Priority", ref priority, 0.1f, 0, 100))
                    {
                        layer.Priority = priority;
                        changed = true;
                    }

                    // Blend mode
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Text("Blend Mode");
                    
                    var blendMode = (int)layer.BlendMode;
                    string[] blendModes = new[] { "Height And Slope", "Height", "Slope", "Height Or Slope" };
                    if (ImGui.Combo("Mode", ref blendMode, blendModes, blendModes.Length))
                    {
                        layer.BlendMode = (TerrainLayerBlendMode)blendMode;
                        changed = true;
                    }

                    // Underwater settings
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Text("Underwater");
                    
                    bool isUnderwater = layer.IsUnderwater;
                    if (ImGui.Checkbox("Enable Underwater Mode", ref isUnderwater))
                    {
                        layer.IsUnderwater = isUnderwater;
                        changed = true;
                    }

                    if (layer.IsUnderwater)
                    {
                        ImGui.Indent();
                        ImGui.TextDisabled("Layer will only appear underwater");

                        float uwHeightMax = layer.UnderwaterHeightMax;
                        if (ImGui.DragFloat("Max Height", ref uwHeightMax, 0.1f, -1000f, 1000f))
                        {
                            layer.UnderwaterHeightMax = uwHeightMax;
                            changed = true;
                        }

                        float uwBlendDist = layer.UnderwaterBlendDistance;
                        if (ImGui.DragFloat("Blend Distance", ref uwBlendDist, 0.1f, 0f, 50f))
                        {
                            layer.UnderwaterBlendDistance = uwBlendDist;
                            changed = true;
                        }

                        float uwSlopeMin = layer.UnderwaterSlopeMin;
                        float uwSlopeMax = layer.UnderwaterSlopeMax;
                        if (ImGui.DragFloatRange2("Slope Range (°)", ref uwSlopeMin, ref uwSlopeMax, 1f, 0f, 90f))
                        {
                            layer.UnderwaterSlopeMin = uwSlopeMin;
                            layer.UnderwaterSlopeMax = uwSlopeMax;
                            changed = true;
                        }

                        float uwBlendWithOthers = layer.UnderwaterBlendWithOthers;
                        if (ImGui.SliderFloat("Blend With Others", ref uwBlendWithOthers, 0f, 1f))
                        {
                            layer.UnderwaterBlendWithOthers = uwBlendWithOthers;
                            changed = true;
                        }
                        ImGui.TextDisabled("0 = Pure underwater, 1 = Blend with surface");

                        ImGui.Unindent();
                    }

                    ImGui.TreePop();
                }

                // Save if changed
                if (changed)
                {
                    SaveMaterial(terrain, terrainMat);
                }

                ImGui.PopID();
            }
        }

        private static void SaveMaterial(Terrain terrain, MaterialAsset material)
        {
            if (terrain.TerrainMaterialGuid.HasValue)
            {
                try
                {
                    // Use AssetDatabase.SaveMaterial to trigger MaterialSaved event for cache invalidation
                    AssetDatabase.SaveMaterial(material);
                    Console.WriteLine($"[TerrainLayersUI] Saved material with layers - cache will be invalidated");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TerrainLayersUI] Failed to save material: {ex.Message}");
                }
            }
        }
    }
}
