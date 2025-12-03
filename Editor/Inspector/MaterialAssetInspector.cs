using System;
using System.Collections.Generic;
using ImGuiNET;
using Editor.State;
using Engine.Assets;

namespace Editor.Inspector
{
    public static class MaterialAssetInspector
    {
        // CRITICAL FIX: Single source of truth - the in-memory material being edited
        // This material is loaded ONCE when opening the inspector and kept in sync
        // No more constant reloading from disk which causes value resets!
        private static Guid _currentGuid = Guid.Empty;
        private static MaterialAsset? _current = null;
        private static MaterialAsset? _beforeEdit = null;

        // Track if we're currently editing (any slider active)
        private static bool _isEditing = false;

        // Debounced save timer
        private static DateTime _lastEditTime = DateTime.MinValue;
        private static bool _needsSave = false;
        private static string _pendingUndoLabel = "";

        // Threshold for auto-save after last edit
        private const double AutoSaveDelayMs = 300;

        static MaterialAsset Clone(MaterialAsset m) => new MaterialAsset
        {
            Guid = m.Guid,
            Name = m.Name,
            Shader = m.Shader,
            AlbedoTexture = m.AlbedoTexture,
            AlbedoColor = (float[])m.AlbedoColor.Clone(),
            NormalTexture = m.NormalTexture,
            NormalStrength = m.NormalStrength,
            MetallicTexture = m.MetallicTexture,
            RoughnessTexture = m.RoughnessTexture,
            MetallicRoughnessTexture = m.MetallicRoughnessTexture,
            OcclusionTexture = m.OcclusionTexture,
            EmissiveTexture = m.EmissiveTexture,
            HeightTexture = m.HeightTexture,
            DetailMaskTexture = m.DetailMaskTexture,
            DetailAlbedoTexture = m.DetailAlbedoTexture,
            DetailNormalTexture = m.DetailNormalTexture,
            Metallic = m.Metallic,
            Roughness = m.Roughness,
            OcclusionStrength = m.OcclusionStrength,
            EmissiveColor = m.EmissiveColor != null ? (float[])m.EmissiveColor.Clone() : new float[] { 1f, 1f, 1f },
            HeightScale = m.HeightScale,
            TextureTiling = (float[])m.TextureTiling.Clone(),
            TextureOffset = (float[])m.TextureOffset.Clone(),
            UseTriplanar = m.UseTriplanar,
            TriplanarScale = m.TriplanarScale,
            TriplanarBlendSharpness = m.TriplanarBlendSharpness,
            Saturation = m.Saturation,
            Brightness = m.Brightness,
            Contrast = m.Contrast,
            Hue = m.Hue,
            Emission = m.Emission,
            TransparencyMode = m.TransparencyMode,
            Opacity = m.Opacity,
            GlassProperties = m.GlassProperties != null ? new Engine.Assets.GlassMaterialProperties
            {
                RefractiveIndex = m.GlassProperties.RefractiveIndex,
                DistortionStrength = m.GlassProperties.DistortionStrength,
                ChromaticAberration = m.GlassProperties.ChromaticAberration,
                Roughness = m.GlassProperties.Roughness,
                Thickness = m.GlassProperties.Thickness,
                Tint = m.GlassProperties.Tint != null ? (float[])m.GlassProperties.Tint.Clone() : new float[] { 1f, 1f, 1f },
                Opacity = m.GlassProperties.Opacity,
                FresnelPower = m.GlassProperties.FresnelPower,
                ReflectionStrength = m.GlassProperties.ReflectionStrength
            } : null
        };

        public static void Draw(Guid guid)
        {
            // Process pending auto-save
            ProcessAutoSave();

            // Load material if different GUID or first load
            if (_currentGuid != guid || _current == null)
            {
                try
                {
                    _current = AssetDatabase.LoadMaterial(guid);
                    _currentGuid = guid;
                    _beforeEdit = null;
                    _isEditing = false;
                    _needsSave = false;
                }
                catch
                {
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), "Material introuvable.");
                    return;
                }
            }

            var mat = _current;

            // --- Shader selection ---
            {
                try
                {
                    var names = Engine.Rendering.ShaderLibrary.GetAvailableShaderNames();
                    var nameList = new System.Collections.Generic.List<string>(names ?? Array.Empty<string>());
                    bool hasCurrent = !string.IsNullOrEmpty(mat.Shader) && nameList.Exists(s => string.Equals(s, mat.Shader, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(mat.Shader) && !hasCurrent)
                    {
                        nameList.Insert(0, mat.Shader!);
                    }

                    int curIndex = 0;
                    if (!string.IsNullOrEmpty(mat.Shader))
                    {
                        for (int i = 0; i < nameList.Count; i++)
                        {
                            if (string.Equals(nameList[i], mat.Shader, StringComparison.OrdinalIgnoreCase)) { curIndex = i; break; }
                        }
                    }

                    if (nameList.Count > 0)
                    {
                        var arr = nameList.ToArray();
                        if (ImGui.Combo("Shader", ref curIndex, arr, arr.Length))
                        {
                            BeginEdit("Shader");
                            mat.Shader = arr[Math.Clamp(curIndex, 0, arr.Length - 1)];
                            SaveAndApplyImmediate(guid, mat, "Shader");
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No shaders found");
                    }
                }
                catch { }
            }

            // If terrain shader, show readonly hint
            if (string.Equals(mat.Shader, "TerrainForward", StringComparison.OrdinalIgnoreCase))
            {
                ImGui.Separator();
                ImGui.Text("Terrain Layers");
                ImGui.TextDisabled("Legacy: Terrain layers are now managed on the Terrain component (use the Terrain inspector to edit layers).\nThis view is read-only for migration/compatibility.");
            }

            // If glass shader, show glass properties
            if (string.Equals(mat.Shader, "Glass", StringComparison.OrdinalIgnoreCase))
            {
                if (mat.GlassProperties == null)
                {
                    mat.GlassProperties = new Engine.Assets.GlassMaterialProperties();
                    mat.TransparencyMode = 1;
                    SaveAndApplyImmediate(guid, mat, "Initialize Glass");
                }
                GlassMaterialInspector.DrawGlassProperties(mat);
                return;
            }

            // === TEXTURES SECTION ===
            mat.AlbedoTexture = DrawTextureField(guid, mat, "Albedo Texture", mat.AlbedoTexture, "Assign Albedo");
            mat.NormalTexture = DrawTextureField(guid, mat, "Normal Texture", mat.NormalTexture, "Assign Normal");

            if (mat.NormalTexture.HasValue && mat.NormalTexture.Value != Guid.Empty)
            {
                float ns = mat.NormalStrength;
                if (ImGui.SliderFloat("Normal Strength", ref ns, 0.0f, 10.0f, "%.2f"))
                {
                    BeginEdit("Normal Strength");
                    mat.NormalStrength = ns;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
            }

            // PBR Textures
            ImGui.Separator();
            ImGui.Text("PBR Textures");
            mat.MetallicTexture = DrawTextureField(guid, mat, "Metallic Texture", mat.MetallicTexture, "Assign Metallic");
            mat.RoughnessTexture = DrawTextureField(guid, mat, "Roughness Texture", mat.RoughnessTexture, "Assign Roughness");

            ImGui.Text("Metallic-Roughness (GLTF):");
            ImGui.SameLine();
            var btnMR = mat.MetallicRoughnessTexture.HasValue && mat.MetallicRoughnessTexture.Value != Guid.Empty
                ? AssetDatabase.GetName(mat.MetallicRoughnessTexture.Value)
                : "<none>";
            ImGui.Button(btnMR + "##MetallicRoughnessBtn");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("GLTF 2.0 combined texture (G=roughness, B=metallic)");
            if (ImGui.BeginDragDropTarget())
            {
                if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                    AssetDatabase.GetTypeName(dropped) == "Texture2D")
                {
                    BeginEdit("Assign Metallic-Roughness");
                    mat.MetallicRoughnessTexture = dropped;
                    SaveAndApplyImmediate(guid, mat, "Assign Metallic-Roughness");
                }
                ImGui.EndDragDropTarget();
            }
            if (mat.MetallicRoughnessTexture.HasValue && mat.MetallicRoughnessTexture.Value != Guid.Empty)
            {
                ImGui.SameLine();
                if (ImGui.Button("X##ClearMetallicRoughness"))
                {
                    BeginEdit("Clear Metallic-Roughness");
                    mat.MetallicRoughnessTexture = null;
                    SaveAndApplyImmediate(guid, mat, "Clear Metallic-Roughness");
                }
            }

            mat.OcclusionTexture = DrawTextureField(guid, mat, "Occlusion Texture", mat.OcclusionTexture, "Assign Occlusion");
            if (mat.OcclusionTexture.HasValue && mat.OcclusionTexture.Value != Guid.Empty)
            {
                float occStr = mat.OcclusionStrength;
                if (ImGui.SliderFloat("Occlusion Strength", ref occStr, 0.0f, 1.0f, "%.2f"))
                {
                    BeginEdit("Occlusion Strength");
                    mat.OcclusionStrength = occStr;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
            }

            mat.EmissiveTexture = DrawTextureField(guid, mat, "Emissive Texture", mat.EmissiveTexture, "Assign Emissive");
            if (mat.EmissiveTexture.HasValue && mat.EmissiveTexture.Value != Guid.Empty)
            {
                mat.EmissiveColor ??= new float[] { 1f, 1f, 1f };
                var emCol = new System.Numerics.Vector3(mat.EmissiveColor[0], mat.EmissiveColor[1], mat.EmissiveColor[2]);
                if (ImGui.ColorEdit3("Emissive Color", ref emCol, ImGuiColorEditFlags.DisplayRGB))
                {
                    BeginEdit("Emissive Color");
                    mat.EmissiveColor = new[] { emCol.X, emCol.Y, emCol.Z };
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
            }

            mat.HeightTexture = DrawTextureField(guid, mat, "Height Texture", mat.HeightTexture, "Assign Height");
            if (mat.HeightTexture.HasValue && mat.HeightTexture.Value != Guid.Empty)
            {
                float heightScale = mat.HeightScale;
                if (ImGui.DragFloat("Height Scale", ref heightScale, 0.001f, 0.0f, 0.5f, "%.3f"))
                {
                    BeginEdit("Height Scale");
                    mat.HeightScale = heightScale;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
            }

            // Detail Textures
            if (ImGui.CollapsingHeader("Detail Textures (Advanced)"))
            {
                mat.DetailMaskTexture = DrawTextureField(guid, mat, "Detail Mask", mat.DetailMaskTexture, "Assign Detail Mask");
                mat.DetailAlbedoTexture = DrawTextureField(guid, mat, "Detail Albedo", mat.DetailAlbedoTexture, "Assign Detail Albedo");
                mat.DetailNormalTexture = DrawTextureField(guid, mat, "Detail Normal", mat.DetailNormalTexture, "Assign Detail Normal");
            }

            // === MATERIAL PROPERTIES ===
            ImGui.Separator();

            // Albedo Color
            {
                var c = new System.Numerics.Vector4(mat.AlbedoColor[0], mat.AlbedoColor[1], mat.AlbedoColor[2], mat.AlbedoColor[3]);
                if (ImGui.ColorEdit4("Albedo Color", ref c, ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.AlphaBar))
                {
                    BeginEdit("Albedo Color");
                    mat.AlbedoColor = new[] { c.X, c.Y, c.Z, c.W };
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
            }

            // Render Mode
            {
                int mode = mat.TransparencyMode;
                var modes = new[] { "Opaque", "Transparent" };
                if (ImGui.Combo("Render Mode", ref mode, modes, modes.Length))
                {
                    BeginEdit("Render Mode");
                    mat.TransparencyMode = Math.Clamp(mode, 0, 1);
                    SaveAndApplyImmediate(guid, mat, "Render Mode");
                    try { Editor.Panels.EditorUI.MainViewport.Renderer?.UpdateMaterialTransparency(guid, mat.TransparencyMode); } catch { }
                }
            }

            // PBR sliders
            {
                float m = mat.Metallic;
                if (ImGui.SliderFloat("Metallic", ref m, 0, 1))
                {
                    BeginEdit("Metallic");
                    mat.Metallic = m;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();

                float s = 1.0f - mat.Roughness;
                if (ImGui.SliderFloat("Smoothness", ref s, 0, 1))
                {
                    BeginEdit("Smoothness");
                    mat.Roughness = 1.0f - s;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
            }

            // Texture Coordinates
            ImGui.Separator();
            ImGui.Text("Texture Coordinates");

            mat.TextureTiling ??= new float[] { 1f, 1f };
            mat.TextureOffset ??= new float[] { 0f, 0f };

            bool isTriplanar = mat.UseTriplanar == 1;
            if (isTriplanar) ImGui.BeginDisabled();

            var tiling = new System.Numerics.Vector2(mat.TextureTiling[0], mat.TextureTiling[1]);
            if (ImGui.DragFloat2("Tiling", ref tiling, 0.01f, 0.01f, 10f))
            {
                BeginEdit("Texture Tiling");
                mat.TextureTiling[0] = tiling.X;
                mat.TextureTiling[1] = tiling.Y;
                ApplyLiveUpdate(guid, mat);
            }
            CheckEndEdit();

            var offset = new System.Numerics.Vector2(mat.TextureOffset[0], mat.TextureOffset[1]);
            if (ImGui.DragFloat2("Offset", ref offset, 0.01f, -10f, 10f))
            {
                BeginEdit("Texture Offset");
                mat.TextureOffset[0] = offset.X;
                mat.TextureOffset[1] = offset.Y;
                ApplyLiveUpdate(guid, mat);
            }
            CheckEndEdit();

            if (isTriplanar)
            {
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.TextDisabled("(Disabled when Triplanar enabled)");
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset##UVReset"))
            {
                BeginEdit("Reset UV Coordinates");
                mat.TextureTiling[0] = 1f;
                mat.TextureTiling[1] = 1f;
                mat.TextureOffset[0] = 0f;
                mat.TextureOffset[1] = 0f;
                SaveAndApplyImmediate(guid, mat, "Reset UV Coordinates");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset tiling to (1,1) and offset to (0,0)");

            // Triplanar Mapping
            ImGui.Separator();
            ImGui.Text("Triplanar Mapping");

            bool useTriplanar = mat.UseTriplanar == 1;
            if (ImGui.Checkbox("Enable Triplanar", ref useTriplanar))
            {
                BeginEdit("Toggle Triplanar Mapping");
                mat.UseTriplanar = useTriplanar ? 1 : 0;
                ApplyLiveUpdate(guid, mat);
            }
            CheckEndEdit();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Use world-space triplanar projection instead of UV mapping.\nPrevents texture stretching on non-uniform meshes.");

            if (useTriplanar)
            {
                float triScale = mat.TriplanarScale;
                if (ImGui.DragFloat("World Scale", ref triScale, 0.01f, 0.01f, 10f))
                {
                    BeginEdit("Triplanar Scale");
                    mat.TriplanarScale = triScale;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Texture scale in world space units.\nLower values = larger texture repetition.");

                float triBlend = mat.TriplanarBlendSharpness;
                if (ImGui.SliderFloat("Blend Sharpness", ref triBlend, 1f, 10f))
                {
                    BeginEdit("Triplanar Blend");
                    mat.TriplanarBlendSharpness = triBlend;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Controls how sharply the three projections blend.\nLower = smooth blend, Higher = sharp transitions.");
            }

            // Stylization
            ImGui.Separator();
            ImGui.Text("Stylization");

            {
                float sat = mat.Saturation;
                if (ImGui.SliderFloat("Saturation", ref sat, 0, 2))
                {
                    BeginEdit("Saturation");
                    mat.Saturation = sat;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("0.0 = grayscale, 1.0 = normal, 2.0 = oversaturated");
            }

            {
                float bright = mat.Brightness;
                if (ImGui.SliderFloat("Brightness", ref bright, 0, 2))
                {
                    BeginEdit("Brightness");
                    mat.Brightness = bright;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("0.0 = black, 1.0 = normal, 2.0 = brighter");
            }

            {
                float cont = mat.Contrast;
                if (ImGui.SliderFloat("Contrast", ref cont, 0, 2))
                {
                    BeginEdit("Contrast");
                    mat.Contrast = cont;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("0.0 = flat gray, 1.0 = normal, 2.0 = high contrast");
            }

            {
                float hue = mat.Hue;
                if (ImGui.SliderFloat("Hue Shift", ref hue, -1, 1))
                {
                    BeginEdit("Hue Shift");
                    mat.Hue = hue;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("-1.0 to 1.0, shifts hue on the color wheel");
            }

            {
                float emission = mat.Emission;
                if (ImGui.SliderFloat("Emission", ref emission, 0, 5))
                {
                    BeginEdit("Emission");
                    mat.Emission = emission;
                    ApplyLiveUpdate(guid, mat);
                }
                CheckEndEdit();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("0.0 = no emission, >0.0 = emissive/glow strength");
            }

            ImGui.Spacing();
            if (ImGui.Button("Reset Stylization"))
            {
                BeginEdit("Reset Stylization");
                mat.Saturation = 1.0f;
                mat.Brightness = 1.0f;
                mat.Contrast = 1.0f;
                mat.Hue = 0.0f;
                mat.Emission = 0.0f;
                SaveAndApplyImmediate(guid, mat, "Reset Stylization");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset all stylization parameters to default values");
        }

        private static Guid? DrawTextureField(Guid guid, MaterialAsset mat, string label, Guid? textureGuid, string undoLabel)
        {
            ImGui.Text(label + ":");
            ImGui.SameLine();
            var btn = textureGuid.HasValue && textureGuid.Value != Guid.Empty
                ? AssetDatabase.GetName(textureGuid.Value)
                : "<none>";
            ImGui.Button(btn + $"##{label}Btn");

            if (ImGui.BeginDragDropTarget())
            {
                if (Editor.Panels.AssetsPanel.TryConsumeDraggedAsset(out var dropped) &&
                    AssetDatabase.GetTypeName(dropped) == "Texture2D")
                {
                    BeginEdit(undoLabel);
                    textureGuid = dropped;
                    SaveAndApplyImmediate(guid, mat, undoLabel);
                    return textureGuid;
                }
                ImGui.EndDragDropTarget();
            }

            if (textureGuid.HasValue && textureGuid.Value != Guid.Empty)
            {
                ImGui.SameLine();
                if (ImGui.Button($"X##Clear{label}"))
                {
                    BeginEdit($"Clear {label}");
                    textureGuid = null;
                    SaveAndApplyImmediate(guid, mat, $"Clear {label}");
                    return textureGuid;
                }
            }

            return textureGuid;
        }

        private static void BeginEdit(string label)
        {
            if (!_isEditing && _current != null)
            {
                _beforeEdit = Clone(_current);
                _isEditing = true;
                _pendingUndoLabel = label;
            }
        }

        private static void CheckEndEdit()
        {
            if (_isEditing && ImGui.IsItemDeactivatedAfterEdit())
            {
                _lastEditTime = DateTime.UtcNow;
                _needsSave = true;
                _isEditing = false;
                UndoRedo.TouchEdit();
            }
        }

        private static void ApplyLiveUpdate(Guid guid, MaterialAsset mat)
        {
            try
            {
                Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat);
            }
            catch { }
        }

        private static void SaveAndApplyImmediate(Guid guid, MaterialAsset mat, string undoLabel)
        {
            try
            {
                // Save synchronously for immediate operations (texture changes, etc.)
                if (AssetDatabase.TryGet(guid, out var rec))
                {
                    MaterialAsset.Save(rec.Path, mat);
                    ApplyLiveUpdate(guid, mat);

                    // Push undo
                    if (_beforeEdit != null)
                    {
                        var after = Clone(mat);
                        UndoRedo.Push(new MaterialEditAction(undoLabel, guid, _beforeEdit, after));
                        UndoRedo.RaiseAfterChange();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaterialAssetInspector] Save error: {ex.Message}");
            }
            finally
            {
                _beforeEdit = null;
                _isEditing = false;
                _needsSave = false;
            }
        }

        private static void ProcessAutoSave()
        {
            if (!_needsSave || _current == null) return;

            var elapsed = (DateTime.UtcNow - _lastEditTime).TotalMilliseconds;
            if (elapsed < AutoSaveDelayMs) return;

            try
            {
                if (AssetDatabase.TryGet(_currentGuid, out var rec))
                {
                    MaterialAsset.Save(rec.Path, _current);

                    // Push undo
                    if (_beforeEdit != null)
                    {
                        var after = Clone(_current);
                        UndoRedo.Push(new MaterialEditAction(_pendingUndoLabel, _currentGuid, _beforeEdit, after));
                        UndoRedo.RaiseAfterChange();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaterialAssetInspector] Auto-save error: {ex.Message}");
            }
            finally
            {
                _beforeEdit = null;
                _needsSave = false;
            }
        }
    }
}
