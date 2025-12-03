using System;
using System.Numerics;
using ImGuiNET;
using Engine.Components;
using Engine.Assets;
using Editor.Logging;

namespace Editor.Inspector
{
    /// <summary>
    /// UI helper for terrain layers with material-based system
    /// </summary>
    public static class TerrainLayersUI
    {
        // GUID of the material currently being edited via the Terrain Layers UI (persist across frames)
        private static Guid? _editingMaterial = null;

        public static void DrawTerrainLayers(Terrain terrain)
        {
            if (!ImGui.CollapsingHeader("Terrain Layers", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            ImGui.TextDisabled("Materials will be blended based on height and slope (layers live on the Terrain component)");
            ImGui.Spacing();

            // Ensure TerrainLayers exist on the Terrain component
            terrain.TerrainLayers ??= Array.Empty<TerrainLayer>();

            // Add layer button (max 8)
            if (ImGui.Button("Add Layer") && terrain.TerrainLayers.Length < 8)
            {
                var newLayers = new TerrainLayer[terrain.TerrainLayers.Length + 1];
                Array.Copy(terrain.TerrainLayers, newLayers, terrain.TerrainLayers.Length);
                newLayers[terrain.TerrainLayers.Length] = new TerrainLayer
                {
                    Name = $"Layer {terrain.TerrainLayers.Length}",
                    Priority = terrain.TerrainLayers.Length,
                    Strength = 1.0f
                };
                terrain.TerrainLayers = newLayers;
            }

            ImGui.SameLine();
            ImGui.TextDisabled($"({terrain.TerrainLayers.Length}/8 layers)");

            ImGui.SameLine();
            var terrainMatGuid = terrain.TerrainMaterialGuid;
            if (ImGui.Button("Import From Material") && terrainMatGuid.HasValue)
            {
                // Material-side terrain layers are deprecated; treat them as a legacy import source.
                // Silence the obsolete-member warning for an explicit, localized migration/import.
#pragma warning disable CS0618
                try
                {
                    var matGuid = terrainMatGuid.Value;
                    var mat = AssetDatabase.LoadMaterial(matGuid);
                    if (mat != null && mat.TerrainLayers != null)
                    {
                        // Copy layers into the component (clamped to 8)
                        int copyCount = Math.Min(mat.TerrainLayers.Length, 8);
                        var newLayers = new TerrainLayer[copyCount];
                        for (int i = 0; i < copyCount; i++)
                        {
                            newLayers[i] = mat.TerrainLayers[i];
                        }
                        terrain.TerrainLayers = newLayers;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"[TerrainLayersUI] Failed to import layers from material: {ex.Message}");
                }
#pragma warning restore CS0618
            }

            ImGui.Spacing();

            // Draw each layer from the Terrain component
            for (int i = 0; i < terrain.TerrainLayers.Length; i++)
            {
                ImGui.PushID(i);
                var layer = terrain.TerrainLayers[i];
                bool changed = false;

                bool nodeOpen = ImGui.TreeNodeEx($"{layer.Name}##layer{i}", ImGuiTreeNodeFlags.DefaultOpen);

                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 100);
                if (ImGui.SmallButton("Delete"))
                {
                    var newLayers = new TerrainLayer[terrain.TerrainLayers.Length - 1];
                    int destIndex = 0;
                    for (int j = 0; j < terrain.TerrainLayers.Length; j++)
                    {
                        if (j != i)
                        {
                            newLayers[destIndex++] = terrain.TerrainLayers[j];
                        }
                    }
                    terrain.TerrainLayers = newLayers;
                    ImGui.PopID();
                    break;
                }

                if (nodeOpen)
                {
                    // Name
                    string name = layer.Name ?? "";
                    if (ImGui.InputText("Name", ref name, 256))
                    {
                        layer.Name = name;
                        changed = true;
                    }

                    // Material selection + Edit button
                    ImGui.Text("Material");
                    ImGui.SameLine();
                    var layerMat = layer.Material;
                    if (layerMat.HasValue && AssetDatabase.TryGet(layerMat.Value, out var matRec))
                    {
                        ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), $"✓ {System.IO.Path.GetFileName(matRec.Path)}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton("Edit Material"))
                        {
                            _editingMaterial = layerMat.Value;
                        }
                        ImGui.SameLine();
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

                    // NOTE: UV tiling/offset are handled by the MaterialAsset now.
                    // The terrain layer should not expose tiling/offset to avoid
                    // duplication of responsibility. If you need layer-specific
                    // UV adjustments in future, add a dedicated override flag.

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

                // Save if changed (write back to terrain array)
                if (changed)
                {
                    var copy = terrain.TerrainLayers;
                    copy[i] = layer;
                    terrain.TerrainLayers = copy;
                }

                ImGui.PopID();
            }

            // If a material is being edited, render its full material inspector below
            if (_editingMaterial.HasValue)
            {
                var editingMat = _editingMaterial.Value;
                ImGui.Separator();
                ImGui.Text($"Editing material: {editingMat}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Close Material Editor"))
                {
                    _editingMaterial = null;
                }

                try
                {
                    Editor.Inspector.MaterialAssetInspector.Draw(editingMat);
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"[TerrainLayersUI] Failed to draw material inspector: {ex.Message}");
                }
            }
        }

        // Note: layer management now lives on the Terrain component; material-side layers are deprecated.
    }
}
