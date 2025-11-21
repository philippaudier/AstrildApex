using System;
using ImGuiNET;
using Editor.State;
using Engine.Assets;

namespace Editor.Inspector
{
    public static class MaterialAssetInspector
    {
        // Petits verrous "live edit" par champ pour pousser une action Undo cohérente
        static bool _editCol, _editMetal, _editSmooth, _editNormStr, _editTiling, _editOffset;
        static bool _editSat, _editBright, _editContrast, _editHue, _editEmission;
        static bool _editOcclusionStr, _editEmissiveCol, _editHeightScale;
        static MaterialAsset? _before;
        // Cache temporaire pour éviter que l'Inspector recharge depuis le disque
        // immédiatement après avoir sauvegardé (évite écrasements/reverts visibles).
        static Guid _lastSavedGuid = Guid.Empty;
        static MaterialAsset? _lastSavedMaterial = null;
        static DateTime _lastSavedTime = DateTime.MinValue;

        static MaterialAsset Clone(MaterialAsset m) => new MaterialAsset
        {
            Guid = m.Guid,
            Name = m.Name,
            AlbedoTexture = m.AlbedoTexture,
            AlbedoColor = (float[])m.AlbedoColor.Clone(),
            NormalTexture = m.NormalTexture,
            NormalStrength = m.NormalStrength,
            // PBR Textures
            MetallicTexture = m.MetallicTexture,
            RoughnessTexture = m.RoughnessTexture,
            MetallicRoughnessTexture = m.MetallicRoughnessTexture,
            OcclusionTexture = m.OcclusionTexture,
            EmissiveTexture = m.EmissiveTexture,
            HeightTexture = m.HeightTexture,
            DetailMaskTexture = m.DetailMaskTexture,
            DetailAlbedoTexture = m.DetailAlbedoTexture,
            DetailNormalTexture = m.DetailNormalTexture,
            // PBR Parameters
            Metallic = m.Metallic,
            Roughness = m.Roughness,
            OcclusionStrength = m.OcclusionStrength,
            EmissiveColor = m.EmissiveColor != null ? (float[])m.EmissiveColor.Clone() : new float[] { 1f, 1f, 1f },
            HeightScale = m.HeightScale,
            TextureTiling = (float[])m.TextureTiling.Clone(),
            TextureOffset = (float[])m.TextureOffset.Clone(),
            Saturation = m.Saturation,
            Brightness = m.Brightness,
            Contrast = m.Contrast,
            Hue = m.Hue,
            Emission = m.Emission
        };

        public static void Draw(Guid guid)
        {
            MaterialAsset mat;
            
            // If we are currently live-editing ANY property, use the cached _before material
            // to avoid reloading from disk (which would lose unsaved edits)
            bool isLiveEditing = _editCol || _editMetal || _editSmooth || _editNormStr || 
                                 _editTiling || _editOffset || _editSat || _editBright || 
                                 _editContrast || _editHue || _editEmission ||
                                 _editOcclusionStr || _editEmissiveCol || _editHeightScale;
            
            if (isLiveEditing && _before != null)
            {
                // Use the current in-memory state during live editing
                mat = _before;
            }
            // If we recently (<=1s) saved this material from this inspector, prefer the cached copy
            else if (_lastSavedGuid == guid && _lastSavedMaterial != null && (DateTime.UtcNow - _lastSavedTime).TotalMilliseconds < 1000)
            {
                // PERFORMANCE: Disabled log - try { Console.WriteLine($"[MaterialAssetInspector] Using cached saved material for {guid}"); } catch { }
                mat = Clone(_lastSavedMaterial);
            }
            else
            {
                try { mat = AssetDatabase.LoadMaterial(guid); /* PERFORMANCE: Disabled log - try { Console.WriteLine($"[MaterialAssetInspector] Loaded material {guid} from disk: Roughness={mat.Roughness}, Metallic={mat.Metallic}"); } catch{} */ }
                catch
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), "Material introuvable.");
                    return;
                }
            }

            // PERFORMANCE: Disabled log
            // try
            // {
            //     var uiSmooth = 1.0f - mat.Roughness;
            //     Console.WriteLine($"[MaterialAssetInspector] Draw material {guid} UI Smoothness={uiSmooth} (Roughness={mat.Roughness})");
            // }
            // catch { }

            // --- Albedo texture (drag&drop depuis Assets) ---
            // --- Shader selection ---
            {
                try
                {
                    var names = Engine.Rendering.ShaderLibrary.GetAvailableShaderNames();
                    int curIndex = 0;
                    if (!string.IsNullOrEmpty(mat.Shader))
                    {
                        for (int i = 0; i < names.Length; i++) if (string.Equals(names[i], mat.Shader, StringComparison.OrdinalIgnoreCase)) { curIndex = i; break; }
                    }
                    if (names.Length > 0)
                    {
                        if (ImGui.Combo("Shader", ref curIndex, names, names.Length))
                        {
                            _before ??= Clone(mat);
                            mat.Shader = names[Math.Clamp(curIndex, 0, names.Length - 1)];
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Shader", ref _before);
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No shaders found");
                    }
                }
                catch { }
            }

            // If terrain shader, show terrain layers editor
            if (string.Equals(mat.Shader, "TerrainForward", StringComparison.OrdinalIgnoreCase))
            {
                ImGui.Separator();
                ImGui.Text("Terrain Layers");
                mat.TerrainLayers ??= new Engine.Assets.TerrainLayer[0];

                // Add layer
                ImGui.SameLine();
                if (ImGui.Button("Add Layer"))
                {
                    _before ??= Clone(mat);
                    var list = new System.Collections.Generic.List<Engine.Assets.TerrainLayer>(mat.TerrainLayers);
                    list.Add(new Engine.Assets.TerrainLayer { Name = $"Layer {list.Count}" });
                    mat.TerrainLayers = list.ToArray();
                    AssetDatabase.SaveMaterial(mat);
                    UndoRedo.RaiseAfterChange();
                    PushUndoIfNeeded(guid, "Add Terrain Layer", ref _before);
                }

                // Max layers cap
                int max = Engine.Rendering.MaterialRuntime.MAX_LAYERS;
                if (mat.TerrainLayers.Length > max)
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1,0.6f,0.0f,1), $"Warning: only first {max} layers will be used at render time.");
                }

                // Render each layer
                for (int i = 0; i < mat.TerrainLayers.Length; i++)
                {
                    var layer = mat.TerrainLayers[i];
                    ImGui.Separator();
                    ImGui.Text($"Layer {i}: {layer.Name ?? "(unnamed)"}");
                    ImGui.SameLine();
                    if (ImGui.Button($"Remove##layer{i}"))
                    {
                        _before ??= Clone(mat);
                        var list = new System.Collections.Generic.List<Engine.Assets.TerrainLayer>(mat.TerrainLayers);
                        list.RemoveAt(i);
                        mat.TerrainLayers = list.ToArray();
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Remove Terrain Layer", ref _before);
                        break; // changed collection
                    }

                    // Name
                    var name = layer.Name ?? "";
                    if (ImGui.InputText($"Name##layer{i}", ref name, 256))
                    {
                        _before ??= Clone(mat);
                        layer.Name = name;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                    }

#pragma warning disable CS0618 // Legacy texture properties - kept for backward compatibility
                    // Albedo texture slot (drag&drop)
                    ImGui.Text("Albedo:"); ImGui.SameLine();
                    var btn = layer.AlbedoTexture.HasValue && layer.AlbedoTexture.Value != Guid.Empty ? AssetDatabase.GetName(layer.AlbedoTexture.Value) : "<none>";
                    ImGui.Button(btn);
                    if (ImGui.BeginDragDropTarget())
                    {
                        if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) && AssetDatabase.GetTypeName(dropped) == "Texture2D")
                        {
                            _before ??= Clone(mat);
                            layer.AlbedoTexture = dropped;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Assign Layer Albedo", ref _before);
                        }
                        ImGui.EndDragDropTarget();
                    }

                    // Normal
                    ImGui.Text("Normal:"); ImGui.SameLine();
                    var btnn = layer.NormalTexture.HasValue && layer.NormalTexture.Value != Guid.Empty ? AssetDatabase.GetName(layer.NormalTexture.Value) : "<none>";
                    ImGui.Button(btnn);
                    if (ImGui.BeginDragDropTarget())
                    {
                        if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped2) && AssetDatabase.GetTypeName(dropped2) == "Texture2D")
                        {
                            _before ??= Clone(mat);
                            layer.NormalTexture = dropped2;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Assign Layer Normal", ref _before);
                        }
                        ImGui.EndDragDropTarget();
                    }
#pragma warning restore CS0618

                    // Tiling/Offset
                    var til = new System.Numerics.Vector2(layer.Tiling[0], layer.Tiling[1]);
                    if (ImGui.DragFloat2($"Tiling##layer{i}", ref til, 0.01f, 0.01f, 100f))
                    {
                        _before ??= Clone(mat);
                        layer.Tiling[0] = til.X; layer.Tiling[1] = til.Y;
                        UndoRedo.RaiseAfterChange();
                    }
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        AssetDatabase.SaveMaterial(mat);
                        PushUndoIfNeeded(guid, $"Layer {i} Tiling", ref _before);
                    }

                    var off = new System.Numerics.Vector2(layer.Offset[0], layer.Offset[1]);
                    if (ImGui.DragFloat2($"Offset##layer{i}", ref off, 0.01f, -100f, 100f))
                    {
                        _before ??= Clone(mat);
                        layer.Offset[0] = off.X; layer.Offset[1] = off.Y;
                        UndoRedo.RaiseAfterChange();
                    }
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        AssetDatabase.SaveMaterial(mat);
                        PushUndoIfNeeded(guid, $"Layer {i} Offset", ref _before);
                    }

                    // Underwater mode checkbox
                    ImGui.Separator();
                    bool isUnderwater = layer.IsUnderwater;
                    if (ImGui.Checkbox($"Is Underwater##layer{i}", ref isUnderwater))
                    {
                        _before ??= Clone(mat);
                        layer.IsUnderwater = isUnderwater;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                    }

                    if (layer.IsUnderwater)
                    {
                        // Underwater-specific parameters
                        float uwHeight = layer.UnderwaterHeightMax;
                        if (ImGui.DragFloat($"Water Level##layer{i}", ref uwHeight, 0.5f, -10000f, 10000f))
                        {
                            _before ??= Clone(mat);
                            layer.UnderwaterHeightMax = uwHeight;
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Water Level", ref _before);
                        }

                        float uwBlend = layer.UnderwaterBlendDistance;
                        if (ImGui.DragFloat($"Blend Distance##layer{i}", ref uwBlend, 0.1f, 0.1f, 50f))
                        {
                            _before ??= Clone(mat);
                            layer.UnderwaterBlendDistance = uwBlend;
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Blend Distance", ref _before);
                        }

                        float uwSlopeMin = layer.UnderwaterSlopeMin;
                        float uwSlopeMax = layer.UnderwaterSlopeMax;
                        if (ImGui.DragFloatRange2($"Slope Range##layer{i}", ref uwSlopeMin, ref uwSlopeMax, 0.5f, 0f, 90f))
                        {
                            _before ??= Clone(mat);
                            layer.UnderwaterSlopeMin = Math.Clamp(uwSlopeMin, 0f, 90f);
                            layer.UnderwaterSlopeMax = Math.Clamp(uwSlopeMax, 0f, 90f);
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Slope Range", ref _before);
                        }

                        float uwBlendWithOthers = layer.UnderwaterBlendWithOthers;
                        if (ImGui.SliderFloat($"Blend With Others##layer{i}", ref uwBlendWithOthers, 0f, 1f))
                        {
                            _before ??= Clone(mat);
                            layer.UnderwaterBlendWithOthers = uwBlendWithOthers;
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Blend With Others", ref _before);
                        }
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("0 = Full underwater texture only\n1 = Blend with other layers");
                    }
                    else
                    {
                        // Normal blending parameters (only shown when NOT underwater)
                        // Height range
                        float hmin = layer.HeightMin; float hmax = layer.HeightMax;
                        if (ImGui.DragFloatRange2($"Height Min/Max##layer{i}", ref hmin, ref hmax, 0.1f, -10000f, 10000f))
                        {
                            _before ??= Clone(mat);
                            layer.HeightMin = hmin; layer.HeightMax = hmax;
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Height Range", ref _before);
                        }

                        // Slope range in degrees [0..90]
                        float smin = layer.SlopeMinDeg; float smax = layer.SlopeMaxDeg;
                        if (ImGui.DragFloatRange2($"Slope Min/Max (deg)##layer{i}", ref smin, ref smax, 0.5f, 0f, 90f))
                        {
                            _before ??= Clone(mat);
                            layer.SlopeMinDeg = Math.Clamp(smin, 0f, 90f);
                            layer.SlopeMaxDeg = Math.Clamp(smax, 0f, 90f);
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Slope Range", ref _before);
                        }

                        // Strength
                        float str = layer.Strength;
                        if (ImGui.SliderFloat($"Strength##layer{i}", ref str, 0f, 10f))
                        {
                            _before ??= Clone(mat);
                            layer.Strength = str;
                            UndoRedo.RaiseAfterChange();
                        }
                        if (ImGui.IsItemDeactivatedAfterEdit())
                        {
                            AssetDatabase.SaveMaterial(mat);
                            PushUndoIfNeeded(guid, $"Layer {i} Strength", ref _before);
                        }
                    }
                }
            }

            // If water shader, show water properties editor
            if (string.Equals(mat.Shader, "Water", StringComparison.OrdinalIgnoreCase))
            {
                // Initialize water properties if null
                if (mat.WaterProperties == null)
                {
                    mat.WaterProperties = new Engine.Assets.WaterMaterialProperties();
                    AssetDatabase.SaveMaterial(mat);
                }
                WaterMaterialInspector.DrawWaterProperties(mat);
                // Don't show generic material inspector for water shader
                return;
            }

            {
                ImGui.Text("Albedo Texture:");
                ImGui.SameLine();
                var btn = mat.AlbedoTexture.HasValue && mat.AlbedoTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.AlbedoTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##AlbedoBtn");

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.AlbedoTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Albedo", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.AlbedoTexture.HasValue && mat.AlbedoTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearAlbedo"))
                    {
                        _before ??= Clone(mat);
                        mat.AlbedoTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Albedo", ref _before);
                    }
                }
            }

            // --- Normal map + strength ---
            {
                ImGui.Text("Normal Texture:");
                ImGui.SameLine();
                var btn = mat.NormalTexture.HasValue && mat.NormalTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.NormalTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##NormalBtn");

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.NormalTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Normal", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.NormalTexture.HasValue && mat.NormalTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearNormal"))
                    {
                        _before ??= Clone(mat);
                        mat.NormalTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Normal", ref _before);
                    }

                    float ns = mat.NormalStrength;
                    if (ImGui.SliderFloat("Normal Strength", ref ns, 0.0f, 10.0f, "%.2f"))
                    {
                        BeginLive(ref _editNormStr, mat);
                        mat.NormalStrength = ns;
                        try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                        // Don't save during drag - EndLiveIfReleased will save on mouse release
                        UndoRedo.TouchEdit();
                    }
                    EndLiveIfReleased(guid, "Normal Strength", ref _editNormStr, ref _before, mat);
                }
            }

            // === PBR TEXTURES SECTION ===
            ImGui.Separator();
            ImGui.Text("PBR Textures");
            
            // --- Metallic Texture ---
            {
                ImGui.Text("Metallic Texture:");
                ImGui.SameLine();
                var btn = mat.MetallicTexture.HasValue && mat.MetallicTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.MetallicTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##MetallicBtn");

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.MetallicTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Metallic", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.MetallicTexture.HasValue && mat.MetallicTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearMetallic"))
                    {
                        _before ??= Clone(mat);
                        mat.MetallicTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Metallic", ref _before);
                    }
                }
            }

            // --- Roughness Texture ---
            {
                ImGui.Text("Roughness Texture:");
                ImGui.SameLine();
                var btn = mat.RoughnessTexture.HasValue && mat.RoughnessTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.RoughnessTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##RoughnessBtn");

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.RoughnessTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Roughness", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.RoughnessTexture.HasValue && mat.RoughnessTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearRoughness"))
                    {
                        _before ??= Clone(mat);
                        mat.RoughnessTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Roughness", ref _before);
                    }
                }
            }

            // --- Metallic-Roughness Combined (GLTF) ---
            {
                ImGui.Text("Metallic-Roughness (GLTF):");
                ImGui.SameLine();
                var btn = mat.MetallicRoughnessTexture.HasValue && mat.MetallicRoughnessTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.MetallicRoughnessTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##MetallicRoughnessBtn");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("GLTF 2.0 combined texture (G=roughness, B=metallic)");
                }

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.MetallicRoughnessTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Metallic-Roughness", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.MetallicRoughnessTexture.HasValue && mat.MetallicRoughnessTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearMetallicRoughness"))
                    {
                        _before ??= Clone(mat);
                        mat.MetallicRoughnessTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Metallic-Roughness", ref _before);
                    }
                }
            }

            // --- Occlusion Texture ---
            {
                ImGui.Text("Occlusion Texture:");
                ImGui.SameLine();
                var btn = mat.OcclusionTexture.HasValue && mat.OcclusionTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.OcclusionTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##OcclusionBtn");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Ambient occlusion (R channel)");
                }

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.OcclusionTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Occlusion", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.OcclusionTexture.HasValue && mat.OcclusionTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearOcclusion"))
                    {
                        _before ??= Clone(mat);
                        mat.OcclusionTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Occlusion", ref _before);
                    }

                    // Occlusion Strength slider
                    float occStr = mat.OcclusionStrength;
                    if (ImGui.SliderFloat("Occlusion Strength", ref occStr, 0.0f, 1.0f, "%.2f"))
                    {
                        BeginLive(ref _editOcclusionStr, mat);
                        mat.OcclusionStrength = occStr;
                        try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                        UndoRedo.TouchEdit();
                    }
                    EndLiveIfReleased(guid, "Occlusion Strength", ref _editOcclusionStr, ref _before, mat);
                }
            }

            // --- Emissive Texture ---
            {
                ImGui.Text("Emissive Texture:");
                ImGui.SameLine();
                var btn = mat.EmissiveTexture.HasValue && mat.EmissiveTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.EmissiveTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##EmissiveBtn");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Self-illumination texture (RGB)");
                }

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.EmissiveTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Emissive", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.EmissiveTexture.HasValue && mat.EmissiveTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearEmissive"))
                    {
                        _before ??= Clone(mat);
                        mat.EmissiveTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Emissive", ref _before);
                    }

                    // Emissive Color tint
                    mat.EmissiveColor ??= new float[] { 1f, 1f, 1f };
                    var emCol = new System.Numerics.Vector3(mat.EmissiveColor[0], mat.EmissiveColor[1], mat.EmissiveColor[2]);
                    if (ImGui.ColorEdit3("Emissive Color", ref emCol, ImGuiColorEditFlags.DisplayRGB))
                    {
                        BeginLive(ref _editEmissiveCol, mat);
                        mat.EmissiveColor = new[] { emCol.X, emCol.Y, emCol.Z };
                        try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                        UndoRedo.TouchEdit();
                    }
                    EndLiveIfReleased(guid, "Emissive Color", ref _editEmissiveCol, ref _before, mat);
                }
            }

            // --- Height Texture ---
            {
                ImGui.Text("Height Texture:");
                ImGui.SameLine();
                var btn = mat.HeightTexture.HasValue && mat.HeightTexture.Value != Guid.Empty
                    ? AssetDatabase.GetName(mat.HeightTexture.Value)
                    : "<none>";
                ImGui.Button(btn + "##HeightBtn");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Height/Parallax map (R channel)");
                }

                if (ImGui.BeginDragDropTarget())
                {
                    if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                        AssetDatabase.GetTypeName(dropped) == "Texture2D")
                    {
                        _before ??= Clone(mat);
                        mat.HeightTexture = dropped;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Assign Height", ref _before);
                    }
                    ImGui.EndDragDropTarget();
                }

                if (mat.HeightTexture.HasValue && mat.HeightTexture.Value != Guid.Empty)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("X##ClearHeight"))
                    {
                        _before ??= Clone(mat);
                        mat.HeightTexture = null;
                        AssetDatabase.SaveMaterial(mat);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Clear Height", ref _before);
                    }

                    // Height Scale slider
                    float heightScale = mat.HeightScale;
                    if (ImGui.SliderFloat("Height Scale", ref heightScale, 0.0f, 0.2f, "%.3f"))
                    {
                        BeginLive(ref _editHeightScale, mat);
                        mat.HeightScale = heightScale;
                        try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                        UndoRedo.TouchEdit();
                    }
                    EndLiveIfReleased(guid, "Height Scale", ref _editHeightScale, ref _before, mat);
                }
            }

            // --- Detail Textures (Collapsible) ---
            if (ImGui.CollapsingHeader("Detail Textures (Advanced)"))
            {
                // Detail Mask
                {
                    ImGui.Text("Detail Mask:");
                    ImGui.SameLine();
                    var btn = mat.DetailMaskTexture.HasValue && mat.DetailMaskTexture.Value != Guid.Empty
                        ? AssetDatabase.GetName(mat.DetailMaskTexture.Value)
                        : "<none>";
                    ImGui.Button(btn + "##DetailMaskBtn");

                    if (ImGui.BeginDragDropTarget())
                    {
                        if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                            AssetDatabase.GetTypeName(dropped) == "Texture2D")
                        {
                            _before ??= Clone(mat);
                            mat.DetailMaskTexture = dropped;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Assign Detail Mask", ref _before);
                        }
                        ImGui.EndDragDropTarget();
                    }

                    if (mat.DetailMaskTexture.HasValue && mat.DetailMaskTexture.Value != Guid.Empty)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("X##ClearDetailMask"))
                        {
                            _before ??= Clone(mat);
                            mat.DetailMaskTexture = null;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Clear Detail Mask", ref _before);
                        }
                    }
                }

                // Detail Albedo
                {
                    ImGui.Text("Detail Albedo:");
                    ImGui.SameLine();
                    var btn = mat.DetailAlbedoTexture.HasValue && mat.DetailAlbedoTexture.Value != Guid.Empty
                        ? AssetDatabase.GetName(mat.DetailAlbedoTexture.Value)
                        : "<none>";
                    ImGui.Button(btn + "##DetailAlbedoBtn");

                    if (ImGui.BeginDragDropTarget())
                    {
                        if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                            AssetDatabase.GetTypeName(dropped) == "Texture2D")
                        {
                            _before ??= Clone(mat);
                            mat.DetailAlbedoTexture = dropped;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Assign Detail Albedo", ref _before);
                        }
                        ImGui.EndDragDropTarget();
                    }

                    if (mat.DetailAlbedoTexture.HasValue && mat.DetailAlbedoTexture.Value != Guid.Empty)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("X##ClearDetailAlbedo"))
                        {
                            _before ??= Clone(mat);
                            mat.DetailAlbedoTexture = null;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Clear Detail Albedo", ref _before);
                        }
                    }
                }

                // Detail Normal
                {
                    ImGui.Text("Detail Normal:");
                    ImGui.SameLine();
                    var btn = mat.DetailNormalTexture.HasValue && mat.DetailNormalTexture.Value != Guid.Empty
                        ? AssetDatabase.GetName(mat.DetailNormalTexture.Value)
                        : "<none>";
                    ImGui.Button(btn + "##DetailNormalBtn");

                    if (ImGui.BeginDragDropTarget())
                    {
                        if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                            AssetDatabase.GetTypeName(dropped) == "Texture2D")
                        {
                            _before ??= Clone(mat);
                            mat.DetailNormalTexture = dropped;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Assign Detail Normal", ref _before);
                        }
                        ImGui.EndDragDropTarget();
                    }

                    if (mat.DetailNormalTexture.HasValue && mat.DetailNormalTexture.Value != Guid.Empty)
                    {
                        ImGui.SameLine();
                        if (ImGui.Button("X##ClearDetailNormal"))
                        {
                            _before ??= Clone(mat);
                            mat.DetailNormalTexture = null;
                            AssetDatabase.SaveMaterial(mat);
                            UndoRedo.RaiseAfterChange();
                            PushUndoIfNeeded(guid, "Clear Detail Normal", ref _before);
                        }
                    }
                }
            }

            // --- Albedo color ---
            {
                var c = new System.Numerics.Vector4(mat.AlbedoColor[0], mat.AlbedoColor[1], mat.AlbedoColor[2], mat.AlbedoColor[3]);
                if (ImGui.ColorEdit4("Albedo Color", ref c, ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.AlphaBar))
                {
                    BeginLive(ref _editCol, mat);
                    mat.AlbedoColor = new[] { c.X, c.Y, c.Z, c.W };
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    // Don't save during drag - EndLiveIfReleased will save on mouse release
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Albedo Color", ref _editCol, ref _before, mat);
            }

                // --- Render mode (Opaque / Transparent) ---
                {
                    int mode = mat.TransparencyMode;
                    var modes = new[] { "Opaque", "Transparent" };
                    if (ImGui.Combo("Render Mode", ref mode, modes, modes.Length))
                    {
                        _before ??= Clone(mat);
                        mat.TransparencyMode = Math.Clamp(mode, 0, 1);
                        AssetDatabase.SaveMaterial(mat);
                        // Update ONLY TransparencyMode in cached MaterialRuntime (not other properties)
                        Editor.Panels.EditorUI.MainViewport.Renderer?.UpdateMaterialTransparency(guid, mat.TransparencyMode);
                        UndoRedo.RaiseAfterChange();
                        PushUndoIfNeeded(guid, "Render Mode", ref _before);
                    }
                }

            // --- PBR sliders ---
            {
                float m = mat.Metallic;
                if (ImGui.SliderFloat("Metallic", ref m, 0, 1))
                {
                    BeginLive(ref _editMetal, mat);
                    mat.Metallic = m;
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    // Don't save during drag - EndLiveIfReleased will save on mouse release
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Metallic", ref _editMetal, ref _before, mat);

                float s = 1.0f - mat.Roughness;  // Convert Roughness to Smoothness for UI
                if (ImGui.SliderFloat("Smoothness", ref s, 0, 1))
                {
                    BeginLive(ref _editSmooth, mat);
                    mat.Roughness = 1.0f - s;  // Convert back to Roughness for storage
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    // Don't save during drag - EndLiveIfReleased will save on mouse release
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Smoothness", ref _editSmooth, ref _before, mat);
            }

            // --- Texture Tiling & Offset ---
            ImGui.Separator();
            ImGui.Text("Texture Coordinates");
            
            // Ensure tiling and offset arrays are initialized
            mat.TextureTiling ??= new float[] { 1f, 1f };
            mat.TextureOffset ??= new float[] { 0f, 0f };
            
            // Tiling controls
            var tiling = new System.Numerics.Vector2(mat.TextureTiling[0], mat.TextureTiling[1]);
            if (ImGui.DragFloat2("Tiling", ref tiling, 0.01f, 0.01f, 10f))
            {
                BeginLive(ref _editTiling, mat);
                mat.TextureTiling[0] = tiling.X;
                mat.TextureTiling[1] = tiling.Y;
                try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                // Don't save during drag - EndLiveIfReleased will save on mouse release
                UndoRedo.TouchEdit();
            }
            EndLiveIfReleased(guid, "Texture Tiling", ref _editTiling, ref _before, mat);

            // Offset controls
            var offset = new System.Numerics.Vector2(mat.TextureOffset[0], mat.TextureOffset[1]);
            if (ImGui.DragFloat2("Offset", ref offset, 0.01f, -10f, 10f))
            {
                BeginLive(ref _editOffset, mat);
                mat.TextureOffset[0] = offset.X;
                mat.TextureOffset[1] = offset.Y;
                try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                // Don't save during drag - EndLiveIfReleased will save on mouse release
                UndoRedo.TouchEdit();
            }
            EndLiveIfReleased(guid, "Texture Offset", ref _editOffset, ref _before, mat);
            
            // Reset button
            ImGui.SameLine();
            if (ImGui.Button("Reset##UVReset"))
            {
                _before ??= Clone(mat);
                mat.TextureTiling[0] = 1f;
                mat.TextureTiling[1] = 1f;
                mat.TextureOffset[0] = 0f;
                mat.TextureOffset[1] = 0f;
                AssetDatabase.SaveMaterial(mat);
                UndoRedo.RaiseAfterChange();
                PushUndoIfNeeded(guid, "Reset UV Coordinates", ref _before);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Reset tiling to (1,1) and offset to (0,0)");
            }

            // --- Stylization Parameters ---
            ImGui.Separator();
            ImGui.Text("Stylization");
            
            // Saturation slider
            {
                float sat = mat.Saturation;
                if (ImGui.SliderFloat("Saturation", ref sat, 0, 2))
                {
                    BeginLive(ref _editSat, mat);
                    mat.Saturation = sat;
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Saturation", ref _editSat, ref _before, mat);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("0.0 = grayscale, 1.0 = normal, 2.0 = oversaturated");
                }
            }

            // Brightness slider
            {
                float bright = mat.Brightness;
                if (ImGui.SliderFloat("Brightness", ref bright, 0, 2))
                {
                    BeginLive(ref _editBright, mat);
                    mat.Brightness = bright;
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Brightness", ref _editBright, ref _before, mat);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("0.0 = black, 1.0 = normal, 2.0 = brighter");
                }
            }

            // Contrast slider
            {
                float cont = mat.Contrast;
                if (ImGui.SliderFloat("Contrast", ref cont, 0, 2))
                {
                    BeginLive(ref _editContrast, mat);
                    mat.Contrast = cont;
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Contrast", ref _editContrast, ref _before, mat);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("0.0 = flat gray, 1.0 = normal, 2.0 = high contrast");
                }
            }

            // Hue shift slider
            {
                float hue = mat.Hue;
                if (ImGui.SliderFloat("Hue Shift", ref hue, -1, 1))
                {
                    BeginLive(ref _editHue, mat);
                    mat.Hue = hue;
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Hue Shift", ref _editHue, ref _before, mat);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("-1.0 to 1.0, shifts hue on the color wheel");
                }
            }

            // Emission slider
            {
                float emission = mat.Emission;
                if (ImGui.SliderFloat("Emission", ref emission, 0, 5))
                {
                    BeginLive(ref _editEmission, mat);
                    mat.Emission = emission;
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat); } catch { }
                    UndoRedo.TouchEdit();
                }
                EndLiveIfReleased(guid, "Emission", ref _editEmission, ref _before, mat);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("0.0 = no emission, >0.0 = emissive/glow strength");
                }
            }

            // Reset stylization button
            ImGui.Spacing();
            if (ImGui.Button("Reset Stylization"))
            {
                _before ??= Clone(mat);
                mat.Saturation = 1.0f;
                mat.Brightness = 1.0f;
                mat.Contrast = 1.0f;
                mat.Hue = 0.0f;
                mat.Emission = 0.0f;
                AssetDatabase.SaveMaterial(mat);
                UndoRedo.RaiseAfterChange();
                PushUndoIfNeeded(guid, "Reset Stylization", ref _before);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Reset all stylization parameters to default values");
            }
        }

        static void BeginLive(ref bool flag, MaterialAsset current)
        {
            if (!flag) { _before ??= Clone(current); flag = true; /* PERFORMANCE: Disabled log - try { Console.WriteLine($"[MaterialAssetInspector] BeginLive: caching before for {current.Guid} Roughness={current.Roughness}"); } catch { } */ }
        }

        static void EndLiveIfReleased(Guid guid, string label, ref bool flag, ref MaterialAsset? before, MaterialAsset current)
        {
            if (flag && ImGui.IsItemDeactivatedAfterEdit())
            {
                // Save to disk ONLY when user releases the mouse button (not during drag)
                // PERFORMANCE: Disabled log - try { Console.WriteLine($"[MaterialAssetInspector] EndLive SaveMaterial for {guid} label={label}"); } catch { }

                // Defensive: avoid writing if disk already contains same values (prevents overwrite cycles)
                try
                {
                    var onDisk = AssetDatabase.LoadMaterial(guid);
                    // PERFORMANCE: Disabled log - try { Console.WriteLine($"[MaterialAssetInspector] Compare for {guid} label={label} current.Roughness={current.Roughness} current.Metallic={current.Metallic} onDisk.Roughness={onDisk.Roughness} onDisk.Metallic={onDisk.Metallic}"); } catch { }
                    if (onDisk != null && MaterialsEqual(current, onDisk))
                    {
                        // PERFORMANCE: Disabled log - try { Console.WriteLine($"[MaterialAssetInspector] Skip SaveMaterial for {guid} because on-disk equals current"); } catch { }
                        // clear live edit state and don't push undo (no change)
                        flag = false;
                        before = null;
                        return;
                    }
                    
                    // CRITICAL FIX: Preserve TransparencyMode from disk
                    // When live-editing properties like Smoothness, we don't want to accidentally
                    // overwrite TransparencyMode with a stale in-memory value.
                    if (onDisk != null)
                    {
                        current.TransparencyMode = onDisk.TransparencyMode;
                    }
                }
                catch { /* ignore load errors and attempt save */ }

                // Cache the value we are about to save so that subsequent immediate reloads
                // prefer our in-memory copy instead of reading the file (which may race).
                _lastSavedGuid = guid;
                _lastSavedMaterial = Clone(current);
                _lastSavedTime = DateTime.UtcNow;

                AssetDatabase.SaveMaterial(current);
                // Proactively notify the viewport renderer to refresh its cached runtime
                // so changes are visible immediately on mouse release.
                try
                {
                    Editor.Panels.EditorUI.MainViewport.Renderer?.OnMaterialSaved(guid);
                }
                catch { }
                PushUndoIfNeeded(guid, label, ref before);
                flag = false;
            }
        }

        static bool MaterialsEqual(MaterialAsset a, MaterialAsset b)
        {
            if (a == null || b == null) return false;
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;
            if (!string.Equals(a.Shader, b.Shader, StringComparison.Ordinal)) return false;
            if (a.AlbedoTexture != b.AlbedoTexture) return false;
            if (a.NormalTexture != b.NormalTexture) return false;
            if (a.Metallic != b.Metallic) return false;
            if (a.Roughness != b.Roughness) return false;
            if (a.NormalStrength != b.NormalStrength) return false;
            if (a.TransparencyMode != b.TransparencyMode) return false;
            if (a.Saturation != b.Saturation) return false;
            if (a.Brightness != b.Brightness) return false;
            if (a.Contrast != b.Contrast) return false;
            if (a.Hue != b.Hue) return false;
            if (a.Emission != b.Emission) return false;
            if (!ArrayEquals(a.AlbedoColor, b.AlbedoColor)) return false;
            if (!ArrayEquals(a.TextureTiling, b.TextureTiling)) return false;
            if (!ArrayEquals(a.TextureOffset, b.TextureOffset)) return false;
            return true;
        }

        static bool ArrayEquals(float[]? x, float[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (Math.Abs(x[i] - y[i]) > 1e-6f) return false;
            return true;
        }

        static void PushUndoIfNeeded(Guid guid, string label, ref MaterialAsset? before)
        {
            try
            {
                if (before == null) return;
                var after = AssetDatabase.LoadMaterial(guid);
                UndoRedo.Push(new MaterialEditAction(label, guid, before, after));
            }
            finally { before = null; }
        }
    }
}
