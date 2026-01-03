using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Editor.State;
using Editor.UI;
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
            CullingMode = m.CullingMode,
            AlphaClippingEnabled = m.AlphaClippingEnabled,
            AlphaClipThreshold = m.AlphaClipThreshold,
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
            ,WaterProperties = m.WaterProperties != null ? new Engine.Assets.WaterProperties
            {
                // Phase 1: Base Color
                WaterColor = m.WaterProperties.WaterColor != null ? (float[])m.WaterProperties.WaterColor.Clone() : new float[] { 0.0f, 0.4f, 0.6f, 1.0f },
                DeepWaterColor = m.WaterProperties.DeepWaterColor != null ? (float[])m.WaterProperties.DeepWaterColor.Clone() : new float[] { 0.0f, 0.1f, 0.3f, 1.0f },
                Transparency = m.WaterProperties.Transparency,
                // Wave Animation
                WaveSpeed = m.WaterProperties.WaveSpeed,
                WaveAmplitude = m.WaterProperties.WaveAmplitude,
                WaveHeight = m.WaterProperties.WaveHeight,
                WaveFrequency = m.WaterProperties.WaveFrequency,
                WaveDirection = m.WaterProperties.WaveDirection != null ? (float[])m.WaterProperties.WaveDirection.Clone() : new float[] { 1.0f, 0.0f },
                // Phase 2: Normal Mapping
                NormalStrength = m.WaterProperties.NormalStrength,
                NormalStrength2 = m.WaterProperties.NormalStrength2,
                NormalBlend = m.WaterProperties.NormalBlend,
                NormalLayer1Speed = m.WaterProperties.NormalLayer1Speed,
                NormalLayer2Speed = m.WaterProperties.NormalLayer2Speed,
                NormalLayer1Direction = m.WaterProperties.NormalLayer1Direction != null ? (float[])m.WaterProperties.NormalLayer1Direction.Clone() : new float[] { 1.0f, 0.0f },
                NormalLayer2Direction = m.WaterProperties.NormalLayer2Direction != null ? (float[])m.WaterProperties.NormalLayer2Direction.Clone() : new float[] { 0.0f, 1.0f },
                // Phase 2: Depth & Refraction
                DepthFadeDistance = m.WaterProperties.DepthFadeDistance,
                RefractionStrength = m.WaterProperties.RefractionStrength,
                UseRefraction = m.WaterProperties.UseRefraction,
                // Phase 3: PBR & Lighting
                Roughness = m.WaterProperties.Roughness,
                Metallic = m.WaterProperties.Metallic,
                Fresnel = m.WaterProperties.Fresnel,
                SpecularStrength = m.WaterProperties.SpecularStrength,
                // Legacy
                Reflectivity = m.WaterProperties.Reflectivity,
                FresnelPower = m.WaterProperties.FresnelPower,
                DistortionStrength = m.WaterProperties.DistortionStrength,
                SpecularPower = m.WaterProperties.SpecularPower,
                SpecularColor = m.WaterProperties.SpecularColor != null ? (float[])m.WaterProperties.SpecularColor.Clone() : new float[] { 1f, 1f, 1f },
                // Phase 4: Color Absorption
                AbsorptionColor = m.WaterProperties.AbsorptionColor != null ? (float[])m.WaterProperties.AbsorptionColor.Clone() : new float[] { 0.4f, 0.8f, 1.0f },
                AbsorptionStrength = m.WaterProperties.AbsorptionStrength,
                // Phase 5: Foam
                FoamAmount = m.WaterProperties.FoamAmount,
                FoamCutoff = m.WaterProperties.FoamCutoff,
                FoamColor = m.WaterProperties.FoamColor != null ? (float[])m.WaterProperties.FoamColor.Clone() : new float[] { 1.0f, 1.0f, 1.0f, 1.0f },
                EdgeFadeDistance = m.WaterProperties.EdgeFadeDistance
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

                            // CRITICAL FIX: Invalidate this material in BOTH caches (AssetDatabase + MaterialRuntime)
                            // This ensures the new shader is picked up when rendering
                            Engine.Assets.AssetDatabase.InvalidateMaterial(guid);

                            // Force shader reload to ensure it's available
                            try
                            {
                                Engine.Rendering.ShaderLibrary.ReloadShader(mat.Shader);
                            }
                            catch { }

                            // Save with overwriteShader=true to ensure the shader field is saved
                            SaveAndApplyImmediate(guid, mat, "Shader", overwriteShader: true);
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No shaders found");
                    }
                }
                catch { }
            }

            // If terrain shader, show simplified UI
            if (string.Equals(mat.Shader, "TerrainForward", StringComparison.OrdinalIgnoreCase))
            {
                ImGui.Separator();
                ImGui.Spacing();
                
                // Info box
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.2f, 0.3f, 0.5f, 0.3f));
                ImGui.BeginChild("TerrainInfo", new Vector2(-1, 0), ImGuiChildFlags.AutoResizeY | ImGuiChildFlags.Borders);
                
                ImGui.TextUnformatted("ℹ️ Terrain Material");
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 0.8f, 1f));
                ImGui.TextWrapped("Terrain layers, textures, and material properties are managed directly on the Terrain component.");
                ImGui.TextWrapped("Use the Terrain inspector to edit:");
                ImGui.BulletText("Layer textures (albedo, normal)");
                ImGui.BulletText("Layer properties (metallic, smoothness, tiling)");
                ImGui.BulletText("Layer blending (height, slope)");
                ImGui.BulletText("Triplanar mapping settings");
                ImGui.PopStyleColor();
                
                ImGui.Spacing();
                
                // Quick link to select terrain entity
                if (ImGui.Button("Select Terrain Entity", new Vector2(-1, 0)))
                {
                    // Find first entity with Terrain component
                    var scene = Panels.EditorUI.MainViewport.Renderer?.Scene;
                    if (scene != null)
                    {
                        foreach (var entity in scene.Entities)
                        {
                            if (entity.GetComponent<Engine.Components.Terrain>() != null)
                            {
                                Selection.SetSingle(entity.Id);
                                break;
                            }
                        }
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Select terrain entity to edit layers in the Inspector");
                
                ImGui.EndChild();
                ImGui.PopStyleColor();
                
                // Only show shader selection for terrain
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                ImGui.TextUnformatted("Shader: TerrainForward");
                ImGui.TextDisabled("(Fixed shader for terrain rendering)");
                
                return; // Skip all other material properties
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

            // If WaterForward shader, show water properties
            if (string.Equals(mat.Shader, "WaterForward", StringComparison.OrdinalIgnoreCase))
            {
                if (mat.WaterProperties == null)
                {
                    mat.WaterProperties = new Engine.Assets.WaterProperties();
                    mat.TransparencyMode = 1; // Water is transparent
                    mat.CullingMode = 2; // None - water should be visible from both sides
                    SaveAndApplyImmediate(guid, mat, "Initialize Water");
                }
                // Always enforce CullingMode = None for water (visible from both sides)
                if (mat.CullingMode != 2)
                {
                    mat.CullingMode = 2;
                    SaveAndApplyImmediate(guid, mat, "Fix Water Culling");
                }
                WaterForwardInspector.DrawWaterForwardProperties(mat);
                return;
            }

            // NOTE: Legacy Water shader support retained but planar reflection controls
            // have been removed as planar reflection system was purged.

            // === TEXTURES SECTION ===
            {
                var newAlbedo = EditorWidgets.AssetField("Albedo Texture", mat.AlbedoTexture, "Texture", "Assign Albedo", showPreview: true);
                if (newAlbedo != mat.AlbedoTexture)
                {
                    BeginEdit("Assign Albedo");
                    mat.AlbedoTexture = newAlbedo;
                    SaveAndApplyImmediate(guid, mat, "Assign Albedo");
                }

                var newNormal = EditorWidgets.AssetField("Normal Texture", mat.NormalTexture, "Texture", "Assign Normal", showPreview: false);
                if (newNormal != mat.NormalTexture)
                {
                    BeginEdit("Assign Normal");
                    mat.NormalTexture = newNormal;
                    SaveAndApplyImmediate(guid, mat, "Assign Normal");
                }
            }

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
            {
                var newMetallic = EditorWidgets.AssetField("Metallic Texture", mat.MetallicTexture, "Texture", "Assign Metallic", showPreview: false);
                if (newMetallic != mat.MetallicTexture)
                {
                    BeginEdit("Assign Metallic");
                    mat.MetallicTexture = newMetallic;
                    SaveAndApplyImmediate(guid, mat, "Assign Metallic");
                }

                var newRoughness = EditorWidgets.AssetField("Roughness Texture", mat.RoughnessTexture, "Texture", "Assign Roughness", showPreview: false);
                if (newRoughness != mat.RoughnessTexture)
                {
                    BeginEdit("Assign Roughness");
                    mat.RoughnessTexture = newRoughness;
                    SaveAndApplyImmediate(guid, mat, "Assign Roughness");
                }

                var newMR = EditorWidgets.AssetField("Metallic-Roughness (GLTF)", mat.MetallicRoughnessTexture, "Texture", "GLTF combined texture (G=roughness, B=metallic)", showPreview: false);
                if (newMR != mat.MetallicRoughnessTexture)
                {
                    BeginEdit("Assign Metallic-Roughness");
                    mat.MetallicRoughnessTexture = newMR;
                    SaveAndApplyImmediate(guid, mat, "Assign Metallic-Roughness");
                }
            }

            var newOcclusion = EditorWidgets.AssetField("Occlusion Texture", mat.OcclusionTexture, "Texture", "Assign Occlusion", showPreview: false);
            if (newOcclusion != mat.OcclusionTexture)
            {
                BeginEdit("Assign Occlusion");
                mat.OcclusionTexture = newOcclusion;
                SaveAndApplyImmediate(guid, mat, "Assign Occlusion");
            }
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

            var newEmissive = EditorWidgets.AssetField("Emissive Texture", mat.EmissiveTexture, "Texture", "Assign Emissive", showPreview: false);
            if (newEmissive != mat.EmissiveTexture)
            {
                BeginEdit("Assign Emissive");
                mat.EmissiveTexture = newEmissive;
                SaveAndApplyImmediate(guid, mat, "Assign Emissive");
            }
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

            var newHeight = EditorWidgets.AssetField("Height Texture", mat.HeightTexture, "Texture", "Assign Height", showPreview: false);
            if (newHeight != mat.HeightTexture)
            {
                BeginEdit("Assign Height");
                mat.HeightTexture = newHeight;
                SaveAndApplyImmediate(guid, mat, "Assign Height");
            }
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
            if (ThemedImGui.CollapsingHeader("Detail Textures (Advanced)"))
            {
                var newDetailMask = EditorWidgets.AssetField("Detail Mask", mat.DetailMaskTexture, "Texture", "Assign Detail Mask", showPreview: false);
                if (newDetailMask != mat.DetailMaskTexture) { BeginEdit("Assign Detail Mask"); mat.DetailMaskTexture = newDetailMask; SaveAndApplyImmediate(guid, mat, "Assign Detail Mask"); }

                var newDetailAlbedo = EditorWidgets.AssetField("Detail Albedo", mat.DetailAlbedoTexture, "Texture", "Assign Detail Albedo", showPreview: false);
                if (newDetailAlbedo != mat.DetailAlbedoTexture) { BeginEdit("Assign Detail Albedo"); mat.DetailAlbedoTexture = newDetailAlbedo; SaveAndApplyImmediate(guid, mat, "Assign Detail Albedo"); }

                var newDetailNormal = EditorWidgets.AssetField("Detail Normal", mat.DetailNormalTexture, "Texture", "Assign Detail Normal", showPreview: false);
                if (newDetailNormal != mat.DetailNormalTexture) { BeginEdit("Assign Detail Normal"); mat.DetailNormalTexture = newDetailNormal; SaveAndApplyImmediate(guid, mat, "Assign Detail Normal"); }
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

                    // CRITICAL FIX: Invalidate material cache to ensure new TransparencyMode is picked up
                    Engine.Assets.AssetDatabase.InvalidateMaterial(guid);

                    SaveAndApplyImmediate(guid, mat, "Render Mode");
                }
            }

            // Alpha Clipping (essential for foliage - discard pixels below threshold)
            {
                bool alphaClip = mat.AlphaClippingEnabled;
                if (ImGui.Checkbox("Alpha Clipping", ref alphaClip))
                {
                    BeginEdit("Alpha Clipping");
                    mat.AlphaClippingEnabled = alphaClip;

                    // CRITICAL FIX: Invalidate material cache to ensure new AlphaClippingEnabled is picked up
                    Engine.Assets.AssetDatabase.InvalidateMaterial(guid);

                    SaveAndApplyImmediate(guid, mat, "Alpha Clipping");
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Discard pixels below threshold (essential for leaves/foliage).\nKeeps Back culling for performance while eliminating transparent pixels.");

                if (mat.AlphaClippingEnabled)
                {
                    float threshold = mat.AlphaClipThreshold;
                    if (ImGui.SliderFloat("Clip Threshold", ref threshold, 0.0f, 1.0f, "%.2f"))
                    {
                        BeginEdit("Alpha Clip Threshold");
                        mat.AlphaClipThreshold = threshold;
                        ApplyLiveUpdate(guid, mat);
                    }
                    CheckEndEdit();
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pixels with alpha below this value are discarded.\n0.5 is typical for foliage.");
                }
            }

            // Culling Mode (which faces to cull during rendering)
            {
                int cullMode = mat.CullingMode;
                var cullModes = new[] { "Back", "Front", "None (Both)" };
                if (ImGui.Combo("Culling Mode", ref cullMode, cullModes, cullModes.Length))
                {
                    BeginEdit("Culling Mode");
                    mat.CullingMode = Math.Clamp(cullMode, 0, 2);

                    // CRITICAL FIX: Invalidate material cache to ensure new CullingMode is picked up
                    Engine.Assets.AssetDatabase.InvalidateMaterial(guid);

                    SaveAndApplyImmediate(guid, mat, "Culling Mode");
                }
                if (ImGui.IsItemHovered())
                {
                    string tooltip = cullMode switch
                    {
                        0 => "Back: Cull back faces (default for solid objects)\nBest performance, most common.",
                        1 => "Front: Cull front faces (for inside-out geometry)\nSpecial use case only.",
                        2 => "None: Render both sides (for thin objects like leaves)\nDouble-sided rendering, performance cost.",
                        _ => "Which faces to cull during rendering"
                    };
                    ImGui.SetTooltip(tooltip);
                }
            }

            ImGui.Spacing();

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
            ImGui.Spacing();
            if (ThemedImGui.CollapsingHeader("✨ Stylization", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

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
                
                // Visual indicator if emission is active
                if (mat.Emission > 0.0f)
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.8f, 0.2f, 1f));
                    ImGui.TextUnformatted("✨");
                    ImGui.PopStyleColor();
                }
                
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("0.0 = no emission, >0.0 = emissive/glow strength");
                    if (mat.EmissiveTexture.HasValue && mat.EmissiveTexture.Value != Guid.Empty)
                    {
                        ImGui.TextUnformatted("Emissive texture detected - emission will be multiplied with it");
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.3f, 1f));
                        ImGui.TextUnformatted("⚠ Assign an Emissive Texture above for visible glow effect");
                        ImGui.PopStyleColor();
                    }
                    ImGui.EndTooltip();
                }
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
                
                ImGui.Unindent();
            }
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
                _beforeEdit = Clone(_current!);
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

        private static void ApplyLiveUpdate(Guid guid, MaterialAsset? mat)
        {
            try
            {
                if (mat == null) return;
                Editor.Panels.EditorUI.MainViewport.Renderer?.ApplyLiveMaterialUpdate(guid, mat);
            }
            catch { }
        }

        private static void SaveAndApplyImmediate(Guid guid, MaterialAsset mat, string undoLabel, bool overwriteShader = false)
        {
            try
            {
                Console.WriteLine($"[MaterialAssetInspector] SaveAndApplyImmediate: {undoLabel}");
                Console.WriteLine($"[MaterialAssetInspector] BEFORE SAVE - AlbedoColor: [{mat.AlbedoColor[0]:F3}, {mat.AlbedoColor[1]:F3}, {mat.AlbedoColor[2]:F3}, {mat.AlbedoColor[3]:F3}]");

                // Save synchronously for immediate operations (texture changes, render mode, etc.)
                // This triggers MaterialSaved event and allows us to reload fresh values
                if (AssetDatabase.TryGet(guid, out var rec))
                {
                    // Save synchronously so we can reload immediately
                    AssetDatabase.SaveMaterial(mat, overwriteShader);

                    // CRITICAL: Reload from disk to ensure inspector uses saved values
                    // This prevents stale in-memory values from overwriting the cache on next edit
                    try
                    {
                        _current = AssetDatabase.LoadMaterial(guid);
                        Console.WriteLine($"[MaterialAssetInspector] AFTER RELOAD - AlbedoColor: [{_current.AlbedoColor[0]:F3}, {_current.AlbedoColor[1]:F3}, {_current.AlbedoColor[2]:F3}, {_current.AlbedoColor[3]:F3}]");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MaterialAssetInspector] Failed to reload after immediate save: {ex.Message}");
                    }

                    // Update live preview with reloaded values
                    ApplyLiveUpdate(guid, _current);

                    // Push undo
                    if (_beforeEdit != null)
                    {
                        var after = Clone(_current!);
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
                Console.WriteLine($"[MaterialAssetInspector] ProcessAutoSave: {_pendingUndoLabel}");
                Console.WriteLine($"[MaterialAssetInspector] BEFORE AUTO-SAVE - AlbedoColor: [{_current.AlbedoColor[0]:F3}, {_current.AlbedoColor[1]:F3}, {_current.AlbedoColor[2]:F3}, {_current.AlbedoColor[3]:F3}]");

                // CRITICAL: Use AssetDatabase.SaveMaterial instead of MaterialAsset.Save
                // This ensures MaterialSaved event is fired and all caches are invalidated
                AssetDatabase.SaveMaterial(_current);

                // CRITICAL: Reload from AssetDatabase cache to get fresh copy with all properties
                // This ensures inspector displays the actual saved values, not stale in-memory data
                try
                {
                    _current = AssetDatabase.LoadMaterial(_currentGuid);
                    Console.WriteLine($"[MaterialAssetInspector] AFTER AUTO-SAVE RELOAD - AlbedoColor: [{_current.AlbedoColor[0]:F3}, {_current.AlbedoColor[1]:F3}, {_current.AlbedoColor[2]:F3}, {_current.AlbedoColor[3]:F3}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MaterialAssetInspector] Failed to reload after save: {ex.Message}");
                }

                // Push undo
                if (_beforeEdit != null)
                {
                    var after = Clone(_current!);
                    UndoRedo.Push(new MaterialEditAction(_pendingUndoLabel, _currentGuid, _beforeEdit, after));
                    UndoRedo.RaiseAfterChange();
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
