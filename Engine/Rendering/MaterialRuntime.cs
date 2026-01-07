using System;
using Engine.Assets;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Engine.Rendering
{
    public sealed class MaterialRuntime
    {
        // Global cache shared across renderers to avoid reloading materials when multiple renderers exist
    private static readonly System.Collections.Generic.Dictionary<Guid, MaterialRuntime> _globalCache = new();
    // THREAD SAFETY: Lock for protecting global cache operations
    private static readonly object _globalCacheLock = new object();
    // Global default used when binding materials. Renderers can override this per-frame if needed.
    public static int DefaultFlipNormalY = 0; // 0 = no flip, 1 = flip

    

        static MaterialRuntime()
        {
            try
            {
                // Subscribe to material saved events to invalidate global cache entries
                Engine.Assets.AssetDatabase.MaterialSaved += OnMaterialSaved;
            }
            catch { }
        }

        private static void OnMaterialSaved(Guid guid)
        {
            try
            {
                // Silently invalidate cache entry for the saved material
                lock (_globalCacheLock)
                {
                    _globalCache.Remove(guid);
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] ✗ Error in OnMaterialSaved: {ex.Message}"); } catch { }
            }
        }

        /// <summary>
        /// Clears the entire global material cache. Call this when loading a new scene
        /// to ensure all materials are reloaded with fresh texture handles.
        /// </summary>
        public static void ClearGlobalCache()
        {
            try
            {
                lock (_globalCacheLock)
                {
                    int count = _globalCache.Count;
                    _globalCache.Clear();
                    // PERFORMANCE: Disabled log
                    // Console.WriteLine($"[MaterialRuntime] ✓ Cleared global cache ({count} materials)");
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] ✗ Error clearing global cache: {ex.Message}"); } catch { }
            }
        }

        /// <summary>
        /// Invalidates a single material cache entry to force reload on next access.
        /// Call this when material properties change in the Inspector.
        /// </summary>
        public static void InvalidateCacheEntry(Guid guid)
        {
            try
            {
                lock (_globalCacheLock)
                {
                    _globalCache.Remove(guid);
                }
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] ✗ Error invalidating cache entry: {ex.Message}"); } catch { }
            }
        }

        /// <summary>
        /// Updates a single material cache entry with the provided material asset.
        /// Call this when material properties change and you want to immediately update the cache.
        /// </summary>
        public static void UpdateCacheEntry(Guid guid, MaterialAsset asset)
        {
            try
            {
                lock (_globalCacheLock)
                {
                    // First invalidate the old entry
                    _globalCache.Remove(guid);
                }

                // Then reload it into the cache with the provided asset
                // Note: FromAsset has its own lock, so we don't need to hold the lock here
                Func<Guid, string?> resolver = g => Engine.Assets.AssetDatabase.TryGet(g, out var rec) ? rec.Path : null;
                var runtime = FromAsset(asset, resolver);
                // FromAsset already adds it to the cache, so we're done
            }
            catch (Exception ex)
            {
                try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] ✗ Error updating cache entry: {ex.Message}"); } catch { }
            }
        }

        public Guid AssetGuid;
        
        // === BASE TEXTURES ===
        public int AlbedoTex = 0;
        public float[] AlbedoColor = new float[] { 1, 1, 1, 1 };
        public int NormalTex = 0;
        public float NormalStrength = 1.0f;
        // Per-material normal green-channel flip flag (0 = no flip, 1 = flip)
        public int FlipNormalY = 0;
        
        // === PBR TEXTURES ===
        public int MetallicTex = 0;        // Metallic map (R channel)
        public int RoughnessTex = 0;       // Roughness map (R channel)
        public int MetallicRoughnessTex = 0; // GLTF 2.0 combined (G=roughness, B=metallic)
        public int OcclusionTex = 0;       // Ambient occlusion (R channel)
        public int EmissiveTex = 0;        // Emissive/Glow texture (RGB)
        public int HeightTex = 0;          // Height/Parallax map (R channel)
        
        // === DETAIL TEXTURES ===
        public int DetailMaskTex = 0;      // Detail mask (R channel)
        public int DetailAlbedoTex = 0;    // Detail albedo (RGB)
        public int DetailNormalTex = 0;    // Detail normal (RGB)
        
        // === PBR PARAMETERS ===
        public float Metallic = 0f;
        public float Smoothness = 0.5f;
        public float OcclusionStrength = 1.0f;
        public float[] EmissiveColor = new float[] { 1f, 1f, 1f }; // RGB tint for emissive
        public float HeightScale = 0.05f;
        
        // Texture tiling and offset
        public float[] TextureTiling = new float[] { 1f, 1f };
        public float[] TextureOffset = new float[] { 0f, 0f };

        // Triplanar mapping parameters
        public int UseTriplanar = 0; // 0 = off, 1 = on
        public float TriplanarScale = 1.0f; // World-space scale factor
        public float TriplanarBlendSharpness = 4.0f; // Controls blend sharpness (1-10, default 4)

        public int TransparencyMode = 0; // 0 = Opaque, 1 = Transparent

        // Alpha clipping parameters
        public int AlphaClippingEnabled = 0; // 0 = off, 1 = on
        public float AlphaClipThreshold = 0.5f; // 0.0-1.0

        // Stylization parameters
        public float Saturation = 1.0f;   // 0.0 = grayscale, 1.0 = normal, >1.0 = oversaturated
        public float Brightness = 1.0f;   // 0.0 = black, 1.0 = normal, >1.0 = brighter
        public float Contrast = 1.0f;     // 0.0 = flat gray, 1.0 = normal, >1.0 = more contrast
        public float Hue = 0.0f;          // -1.0 to 1.0, shifts hue (color wheel rotation)
        public float Emission = 0.0f;     // 0.0 = no emission, >0.0 = emissive/glow strength
    
    public string? ShaderName = null;
        // Terrain layers runtime data
        public const int MAX_LAYERS = 8;
        public int[] LayerAlbedoTex = new int[MAX_LAYERS];
        public int[] LayerNormalTex = new int[MAX_LAYERS];
        public float[,] LayerTilingOffset = new float[MAX_LAYERS, 4]; // tx,ty,ox,oy
        public float[,] LayerHeightSlope = new float[MAX_LAYERS, 4]; // hmin,hmax,smin,smax (slope normalized 0..1)
        public float[] LayerStrength = new float[MAX_LAYERS];
        public int[] LayerIsUnderwater = new int[MAX_LAYERS]; // 0 = normal, 1 = underwater
        public float[,] LayerUnderwaterParams = new float[MAX_LAYERS, 4]; // waterLevel, blendDist, slopeMin, slopeMax (normalized)
        public int LayerCount = 0;

        // Water shader properties - AAA
        public float WaveAmplitude = 0.1f;
        public float WaveFrequency = 1.0f;
        public float WaveSpeed = 1.0f;
        public float[] WaveDirection = new float[] { 1f, 0f };
        public float Wave2Amplitude = 0.05f;
        public float Wave2Frequency = 1.5f;
        public float Wave2Speed = 1.2f;
        public float[] Wave2Direction = new float[] { -0.5f, 0.8f };
        public float[] WaterColor = new float[] { 0.1f, 0.3f, 0.5f, 0.8f };
        public float WaterOpacity = 0.5f;
        public float WaterRefractiveIndex = 1.33f;
        public float WaterDistortionStrength = 0.3f;
        public float WaterChromaticAberration = 0.05f;
        public float WaterRoughness = 0.1f;
        public float[] NormalTiling = new float[] { 1f, 1f };
        public float[] NormalScrollSpeed = new float[] { 0.05f, 0.03f };
        public float WaterFresnelPower = 5.0f;
        public float WaterReflectionStrength = 1.0f;
        public bool UsePlanarReflection = false;

        

        // Glass shader properties
        public float GlassRefractiveIndex = 1.5f;
        public float GlassDistortionStrength = 1.0f;
        public float GlassChromaticAberration = 0.0f;
        public float GlassRoughness = 0.0f;
        public float GlassThickness = 0.1f;
        public float[] GlassTint = new float[] { 1f, 1f, 1f };
        public float GlassOpacity = 0.1f;
        public float GlassFresnelPower = 5.0f;
        public float GlassReflectionStrength = 1.0f;

        // WaterForward shader properties (new advanced water shader)
        // Phase 1: Base Color
        public float[] WaterForwardColor = new float[] { 0.0f, 0.4f, 0.6f, 1.0f };
        public float[] WaterForwardDeepColor = new float[] { 0.0f, 0.1f, 0.3f, 1.0f };
        public float WaterForwardTransparency = 0.3f;
        // Wave Animation
        public float WaterForwardWaveSpeed = 1.0f;
        public float WaterForwardWaveAmplitude = 0.1f;
        public float WaterForwardWaveFrequency = 2.0f;
        public float[] WaterForwardWaveDirection = new float[] { 1.0f, 0.0f };
        // WaterForward: Global/Local/Blend System
        public int WaterForwardWaveMode = 0; // 0=Global, 1=Local, 2=Blend
        public float WaterForwardWaveBlendFactor = 1.0f;
        // Phase 2: Normal Mapping
        public float WaterForwardNormalStrength = 1.0f;
        public float WaterForwardNormalStrength2 = 0.5f;
        public float WaterForwardNormalBlend = 0.5f;
        public float WaterForwardNormalMapScale = 1.0f; // Legacy: use WaterForwardNormalLayer1Scale/WaterForwardNormalLayer2Scale instead
        public float WaterForwardNormalLayer1Scale = 1.0f;
        public float WaterForwardNormalLayer2Scale = 1.3f;
        public float WaterForwardNormalLayer1Speed = 0.05f;
        public float WaterForwardNormalLayer2Speed = -0.03f;
        public float[] WaterForwardNormalLayer1Direction = new float[] { 1.0f, 0.0f };
        public float[] WaterForwardNormalLayer2Direction = new float[] { 0.0f, 1.0f };
        // Phase 2: Depth & Refraction
        public float WaterForwardDepthFadeDistance = 2.0f;
        public float WaterForwardRefractionStrength = 0.05f;
        public bool WaterForwardUseRefraction = true;
        // Phase 3: PBR & Lighting
        public float WaterForwardRoughness = 0.1f;
        public float WaterForwardMetallic = 0.0f;
        public float WaterForwardFresnel = 1.0f;
        public float WaterForwardSpecularStrength = 1.0f;
        // Phase 4: Color Absorption
        public float[] WaterForwardAbsorptionColor = new float[] { 0.4f, 0.8f, 1.0f };
        public float WaterForwardAbsorptionStrength = 0.1f;
        // Phase 5: Foam
        public float WaterForwardFoamAmount = 0.5f;
        public float WaterForwardFoamCutoff = 0.5f;
        public float[] WaterForwardFoamColor = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float WaterForwardFoamTextureScale = 4.0f;
        public float WaterForwardFoamAlphaClipThreshold = 0.1f;
        public float WaterForwardEdgeFadeDistance = 1.0f;
        // Phase 6: AAA Features
        public bool WaterForwardUseCaustics = true;
        public float WaterForwardCausticsStrength = 1.0f;
        public float WaterForwardCausticsScale = 2.0f;
        public float WaterForwardCausticsSpeed = 0.5f;
        public float[] WaterForwardCausticsColor = new float[] { 1.0f, 0.95f, 0.8f };
        public float WaterForwardCausticsSplit = 0.01f;
        public float WaterForwardCausticsDistortion = 1.0f;
        public bool WaterForwardUsePlanarReflections = false;
        public float WaterForwardReflectionBlur = 0.0f;
        public int WaterForwardReflectionResolution = 1024;
        public bool WaterForwardFlipReflectionX = false;
        public bool WaterForwardFlipReflectionY = false;
        public float WaterForwardReflectionClipPlaneOffset = 0.05f;

        // VegetationForward shader properties (wind animation)
        public int VegetationWindMode = 0; // 0=Global, 1=Local, 2=Blend
        public float VegetationWindBlendFactor = 1.0f;
        public float VegetationWindStrength = 0.5f;
        public float[] VegetationWindDirection = new float[] { 1.0f, 0.0f };
        public float VegetationWindSpeed = 1.0f;
        public float VegetationWindGustiness = 0.5f;
        public float VegetationBranchAmplitude = 2.5f;
        public float VegetationBranchSpeed = 4.0f;
        public float VegetationBranchTurbulence = 0.8f;
        public float VegetationTrunkStiffness = 0.85f;
        public float VegetationTrunkBendAmount = 0.3f;
        public float VegetationLeafFlutter = 0.6f;
        public float VegetationLeafFlutterSpeed = 8.0f;

        public static MaterialRuntime FromAsset(MaterialAsset a, Func<Guid, string?> resolvePath)
        {
            TextureCache.Initialize();
            // Return cached runtime if available
            if (a != null && a.Guid != Guid.Empty)
            {
                lock (_globalCacheLock)
                {
                    if (_globalCache.TryGetValue(a.Guid, out var cached))
                    {
                        return cached;
                    }
                }
            }
            var albedoPath = a?.AlbedoTexture.HasValue == true ? resolvePath(a.AlbedoTexture.Value) : null;
            var normalPath = a?.NormalTexture.HasValue == true ? resolvePath(a.NormalTexture.Value) : null;
        // (previous misplaced initialization removed) - Water properties are applied below after mr is created
            // For Water shader, use default white textures initially (will be overridden by WaterProperties)
            bool isWaterShader = string.Equals(a?.Shader, "Water", StringComparison.OrdinalIgnoreCase);
            
            var mr = new MaterialRuntime
            {
                AssetGuid = a?.Guid ?? Guid.Empty,
                
                // === BASE TEXTURES ===
                AlbedoTex = !isWaterShader && a?.AlbedoTexture.HasValue == true ? TextureCache.GetOrLoad(a.AlbedoTexture.Value, resolvePath) : TextureCache.White1x1,
                AlbedoColor = !isWaterShader ? (a?.AlbedoColor ?? new[] { 1f, 1f, 1f, 1f }) : new[] { 1f, 1f, 1f, 1f },
                NormalTex = !isWaterShader && a?.NormalTexture.HasValue == true ? TextureCache.GetOrLoad(a.NormalTexture.Value, resolvePath) : TextureCache.White1x1,
                NormalStrength = !isWaterShader ? Math.Clamp(a?.NormalStrength ?? 1.0f, 0.0f, 10.0f) : 1.0f,
                
                // === PBR TEXTURES ===
                MetallicTex = !isWaterShader && a?.MetallicTexture.HasValue == true ? TextureCache.GetOrLoad(a.MetallicTexture.Value, resolvePath) : TextureCache.White1x1,
                RoughnessTex = !isWaterShader && a?.RoughnessTexture.HasValue == true ? TextureCache.GetOrLoad(a.RoughnessTexture.Value, resolvePath) : TextureCache.White1x1,
                MetallicRoughnessTex = !isWaterShader && a?.MetallicRoughnessTexture.HasValue == true ? TextureCache.GetOrLoad(a.MetallicRoughnessTexture.Value, resolvePath) : TextureCache.White1x1,
                OcclusionTex = !isWaterShader && a?.OcclusionTexture.HasValue == true ? TextureCache.GetOrLoad(a.OcclusionTexture.Value, resolvePath) : TextureCache.White1x1,
                EmissiveTex = !isWaterShader && a?.EmissiveTexture.HasValue == true ? TextureCache.GetOrLoad(a.EmissiveTexture.Value, resolvePath) : TextureCache.White1x1,
                HeightTex = !isWaterShader && a?.HeightTexture.HasValue == true ? TextureCache.GetOrLoad(a.HeightTexture.Value, resolvePath) : TextureCache.White1x1,
                
                // === DETAIL TEXTURES ===
                DetailMaskTex = !isWaterShader && a?.DetailMaskTexture.HasValue == true ? TextureCache.GetOrLoad(a.DetailMaskTexture.Value, resolvePath) : TextureCache.White1x1,
                DetailAlbedoTex = !isWaterShader && a?.DetailAlbedoTexture.HasValue == true ? TextureCache.GetOrLoad(a.DetailAlbedoTexture.Value, resolvePath) : TextureCache.White1x1,
                DetailNormalTex = !isWaterShader && a?.DetailNormalTexture.HasValue == true ? TextureCache.GetOrLoad(a.DetailNormalTexture.Value, resolvePath) : TextureCache.White1x1,
                
                // === PBR PARAMETERS ===
                Metallic = !isWaterShader ? (a?.Metallic ?? 0f) : 0f,
                Smoothness = !isWaterShader ? (a != null ? 1.0f - a.Roughness : 0.5f) : 0.9f,
                OcclusionStrength = !isWaterShader ? (a?.OcclusionStrength ?? 1.0f) : 1.0f,
                EmissiveColor = !isWaterShader ? (a?.EmissiveColor ?? new[] { 1f, 1f, 1f }) : new[] { 1f, 1f, 1f },
                HeightScale = !isWaterShader ? (a?.HeightScale ?? 0.05f) : 0.05f,
                
                TextureTiling = a?.TextureTiling ?? new[] { 1f, 1f },
                TextureOffset = a?.TextureOffset ?? new[] { 0f, 0f },

                // Triplanar mapping parameters
                UseTriplanar = a?.UseTriplanar ?? 0,
                TriplanarScale = a?.TriplanarScale ?? 1.0f,
                TriplanarBlendSharpness = a?.TriplanarBlendSharpness ?? 4.0f,

                // Stylization parameters - use asset values directly, fallback to defaults only if asset is null
                Saturation = a?.Saturation ?? 1.0f,
                Brightness = a?.Brightness ?? 1.0f,
                Contrast = a?.Contrast ?? 1.0f,
                Hue = a?.Hue ?? 0.0f,
                Emission = a?.Emission ?? 0.0f,

                // Alpha clipping parameters
                AlphaClippingEnabled = a?.AlphaClippingEnabled == true ? 1 : 0,
                AlphaClipThreshold = a?.AlphaClipThreshold ?? 0.5f
            };
            // Shader name from asset if present
            try { mr.ShaderName = a?.Shader; } catch { mr.ShaderName = null; }

            // Diagnostic logging removed to reduce verbosity

            // Initialize all layer arrays to zero/defaults
            for (int i = 0; i < MAX_LAYERS; i++)
            {
                mr.LayerIsUnderwater[i] = 0;
                mr.LayerStrength[i] = 0f;
            }

            // Map terrain layers if present (legacy MaterialAsset.TerrainLayers supported as fallback)
            try
            {
#pragma warning disable CS0618 // Legacy MaterialAsset.TerrainLayers: supported here as a localized fallback for old materials
                if (a?.TerrainLayers != null)
                {
                    var arr = a.TerrainLayers;
                    mr.LayerCount = Math.Min(arr.Length, MAX_LAYERS);
                    for (int i = 0; i < mr.LayerCount; i++)
                    {
#pragma warning disable CS0618 // Legacy fallback for backward compatibility
                        var l = arr[i];
                        // Prefer the new Material reference on TerrainLayer. If present, load the
                        // referenced Material asset and use its textures. This avoids using the
                        // deprecated per-layer texture properties.
                        int layerAlbedo = TextureCache.White1x1;
                        int layerNormal = TextureCache.White1x1;
                        if (l.Material.HasValue)
                        {
                            try
                            {
                                var matPath = resolvePath(l.Material.Value);
                                if (!string.IsNullOrEmpty(matPath) && System.IO.File.Exists(matPath))
                                {
                                    var layerMat = Engine.Assets.MaterialAsset.Load(matPath);
                                    if (layerMat != null)
                                    {
                                        if (layerMat.AlbedoTexture.HasValue)
                                            layerAlbedo = TextureCache.GetOrLoad(layerMat.AlbedoTexture.Value, resolvePath);
                                        if (layerMat.NormalTexture.HasValue)
                                            layerNormal = TextureCache.GetOrLoad(layerMat.NormalTexture.Value, resolvePath);
                                    }
                                }
                            }
                            catch { }
                        }
                        else
                        {
                            // Backwards compatibility: use deprecated texture GUIDs if no Material is provided
#pragma warning disable CS0618 // Legacy fallback for backward compatibility
                            if (l.AlbedoTexture.HasValue) layerAlbedo = TextureCache.GetOrLoad(l.AlbedoTexture.Value, resolvePath);
                            if (l.NormalTexture.HasValue) layerNormal = TextureCache.GetOrLoad(l.NormalTexture.Value, resolvePath);
#pragma warning restore CS0618
                        }

                        mr.LayerAlbedoTex[i] = layerAlbedo;
                        mr.LayerNormalTex[i] = layerNormal;
#pragma warning restore CS0618
                        mr.LayerTilingOffset[i, 0] = l.Tiling?[0] ?? 1f;
                        mr.LayerTilingOffset[i, 1] = l.Tiling?[1] ?? 1f;
                        mr.LayerTilingOffset[i, 2] = l.Offset?[0] ?? 0f;
                        mr.LayerTilingOffset[i, 3] = l.Offset?[1] ?? 0f;
                        // convert slope degrees to normalized slope (0..1) by dividing by 90
                        float smin = Math.Clamp((l.SlopeMinDeg) / 90f, 0f, 1f);
                        float smax = Math.Clamp((l.SlopeMaxDeg) / 90f, 0f, 1f);
                        mr.LayerHeightSlope[i, 0] = l.HeightMin;
                        mr.LayerHeightSlope[i, 1] = l.HeightMax;
                        mr.LayerHeightSlope[i, 2] = smin;
                        mr.LayerHeightSlope[i, 3] = smax;
                        mr.LayerStrength[i] = l.Strength;

                        // Underwater parameters
                        mr.LayerIsUnderwater[i] = l.IsUnderwater ? 1 : 0;
                        mr.LayerUnderwaterParams[i, 0] = l.UnderwaterHeightMax;
                        mr.LayerUnderwaterParams[i, 1] = l.UnderwaterBlendDistance;
                        mr.LayerUnderwaterParams[i, 2] = Math.Clamp(l.UnderwaterSlopeMin / 90f, 0f, 1f);
                        mr.LayerUnderwaterParams[i, 3] = Math.Clamp(l.UnderwaterSlopeMax / 90f, 0f, 1f);
                    }
                }
#pragma warning restore CS0618
            }
            catch { }
            // Determine transparency mode from asset if available
            mr.TransparencyMode = a?.TransparencyMode ?? 0;

            // Load glass properties if present
            try
            {
                if (a?.GlassProperties != null)
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Loading Glass properties for material {a.Name}"); } catch { }
                    var g = a.GlassProperties;
                    mr.GlassRefractiveIndex = g.RefractiveIndex;
                    mr.GlassDistortionStrength = g.DistortionStrength;
                    mr.GlassChromaticAberration = g.ChromaticAberration;
                    mr.GlassRoughness = g.Roughness;
                    mr.GlassThickness = g.Thickness;
                    mr.GlassTint = g.Tint ?? new float[] { 1f, 1f, 1f };
                    mr.GlassOpacity = g.Opacity;
                    mr.GlassFresnelPower = g.FresnelPower;
                    mr.GlassReflectionStrength = g.ReflectionStrength;

                    // Force transparency mode ONLY for materials actually using the Glass shader
                    if (string.Equals(a?.Shader, "Glass", StringComparison.OrdinalIgnoreCase))
                    {
                        mr.TransparencyMode = 1;
                    }
                }
            }
            catch { }

            // Load WaterForward properties if present
            try
            {
                if (a != null && a.WaterProperties != null && string.Equals(a.Shader, "WaterForward", StringComparison.OrdinalIgnoreCase))
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Loading WaterForward properties for material {a.Name}"); } catch { }
                    var w = a.WaterProperties;

                    // Phase 1: Base Color
                    mr.WaterForwardColor = w.WaterColor ?? new float[] { 0.0f, 0.4f, 0.6f, 1.0f };
                    mr.WaterForwardDeepColor = w.DeepWaterColor ?? new float[] { 0.0f, 0.1f, 0.3f, 1.0f };
                    mr.WaterForwardTransparency = w.Transparency;

                    // Wave Animation
                    mr.WaterForwardWaveSpeed = w.WaveSpeed;
                    mr.WaterForwardWaveAmplitude = w.WaveAmplitude;
                    mr.WaterForwardWaveFrequency = w.WaveFrequency;
                    mr.WaterForwardWaveDirection = w.WaveDirection ?? new float[] { 1.0f, 0.0f };
                    // Global/Local/Blend system
                    mr.WaterForwardWaveMode = w.WaveMode;
                    mr.WaterForwardWaveBlendFactor = w.WaveBlendFactor;

                    // Phase 2: Normal Mapping
                    mr.WaterForwardNormalStrength = w.NormalStrength;
                    mr.WaterForwardNormalStrength2 = w.NormalStrength2;
                    mr.WaterForwardNormalBlend = w.NormalBlend;
                    mr.WaterForwardNormalMapScale = w.NormalMapScale; // Legacy
                    mr.WaterForwardNormalLayer1Scale = w.NormalLayer1Scale;
                    mr.WaterForwardNormalLayer2Scale = w.NormalLayer2Scale;
                    mr.WaterForwardNormalLayer1Speed = w.NormalLayer1Speed;
                    mr.WaterForwardNormalLayer2Speed = w.NormalLayer2Speed;
                    mr.WaterForwardNormalLayer1Direction = w.NormalLayer1Direction ?? new float[] { 1.0f, 0.0f };
                    mr.WaterForwardNormalLayer2Direction = w.NormalLayer2Direction ?? new float[] { 0.0f, 1.0f };

                    // Phase 2: Depth & Refraction
                    mr.WaterForwardDepthFadeDistance = w.DepthFadeDistance;
                    mr.WaterForwardRefractionStrength = w.RefractionStrength;
                    mr.WaterForwardUseRefraction = w.UseRefraction;

                    // Phase 3: PBR & Lighting
                    mr.WaterForwardRoughness = w.Roughness;
                    mr.WaterForwardMetallic = w.Metallic;
                    mr.WaterForwardFresnel = w.Fresnel;
                    mr.WaterForwardSpecularStrength = w.SpecularStrength;

                    // Phase 4: Color Absorption
                    mr.WaterForwardAbsorptionColor = w.AbsorptionColor ?? new float[] { 0.4f, 0.8f, 1.0f };
                    mr.WaterForwardAbsorptionStrength = w.AbsorptionStrength;

                    // Phase 5: Foam
                    mr.WaterForwardFoamAmount = w.FoamAmount;
                    mr.WaterForwardFoamCutoff = w.FoamCutoff;
                    mr.WaterForwardFoamColor = w.FoamColor ?? new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
                    mr.WaterForwardFoamTextureScale = w.FoamTextureScale;
                    mr.WaterForwardFoamAlphaClipThreshold = w.FoamAlphaClipThreshold;
                    mr.WaterForwardEdgeFadeDistance = w.EdgeFadeDistance;

                    // Phase 6: AAA Features
                    mr.WaterForwardUseCaustics = w.UseCaustics;
                    mr.WaterForwardCausticsStrength = w.CausticsStrength;
                    mr.WaterForwardCausticsScale = w.CausticsScale;
                    mr.WaterForwardCausticsSpeed = w.CausticsSpeed;
                    mr.WaterForwardCausticsColor = w.CausticsColor ?? new float[] { 1.0f, 0.95f, 0.8f };
                    mr.WaterForwardCausticsSplit = w.CausticsSplit;
                    mr.WaterForwardCausticsDistortion = w.CausticsDistortion;
                    mr.WaterForwardUsePlanarReflections = w.UsePlanarReflections;
                    mr.WaterForwardReflectionBlur = w.ReflectionBlur;
                    mr.WaterForwardReflectionResolution = w.ReflectionResolution;
                    mr.WaterForwardFlipReflectionX = w.FlipReflectionX;
                    mr.WaterForwardFlipReflectionY = w.FlipReflectionY;
                    mr.WaterForwardReflectionClipPlaneOffset = w.ReflectionClipPlaneOffset;

                    // Force transparency mode for WaterForward shader
                    mr.TransparencyMode = 1;
                }
            }
            catch { }

            // Load VegetationForward properties if present
            try
            {
                if (a != null && a.VegetationProperties != null && string.Equals(a.Shader, "VegetationForward", StringComparison.OrdinalIgnoreCase))
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Loading VegetationForward properties for material {a.Name}"); } catch { }
                    var v = a.VegetationProperties;

                    mr.VegetationWindMode = v.WindMode;
                    mr.VegetationWindBlendFactor = v.WindBlendFactor;
                    mr.VegetationWindStrength = v.WindStrength;
                    mr.VegetationWindDirection = v.WindDirection ?? new float[] { 1.0f, 0.0f };
                    mr.VegetationWindSpeed = v.WindSpeed;
                    mr.VegetationWindGustiness = v.WindGustiness;
                    mr.VegetationBranchAmplitude = v.BranchAmplitude;
                    mr.VegetationBranchSpeed = v.BranchSpeed;
                    mr.VegetationBranchTurbulence = v.BranchTurbulence;
                    mr.VegetationTrunkStiffness = v.TrunkStiffness;
                    mr.VegetationTrunkBendAmount = v.TrunkBendAmount;
                    mr.VegetationLeafFlutter = v.LeafFlutter;
                    mr.VegetationLeafFlutterSpeed = v.LeafFlutterSpeed;
                }
            }
            catch { }

            // Determine per-material flip flag (flipGreen) by inspecting normal map .meta files
            try
            {
                int flip = DefaultFlipNormalY;
                // Check primary normal texture path if any
                try
                {
                    if (!string.IsNullOrEmpty(normalPath))
                    {
                        var metaPath = normalPath + Engine.Assets.AssetDatabase.MetaExt;
                        if (System.IO.File.Exists(metaPath))
                        {
                            var jm = System.IO.File.ReadAllText(metaPath);
                            using var doc = System.Text.Json.JsonDocument.Parse(jm);
                            if (doc.RootElement.TryGetProperty("flipGreen", out var jg) && jg.ValueKind == System.Text.Json.JsonValueKind.True)
                            {
                                flip = 1;
                            }
                        }
                    }
                }
                catch { }


                // If still not determined, inspect layer normal textures (first found wins)
                try
                {
#pragma warning disable CS0618 // Legacy MaterialAsset.TerrainLayers: localized fallback to inspect legacy layer normal textures
                    if (flip == 0 && a?.TerrainLayers != null)
                    {
                        foreach (var l in a.TerrainLayers)
                        {
                            if (l.NormalTexture.HasValue)
                            {
                                var p = resolvePath(l.NormalTexture.Value);
                                if (!string.IsNullOrEmpty(p))
                                {
                                    var metaPath = p + Engine.Assets.AssetDatabase.MetaExt;
                                    if (System.IO.File.Exists(metaPath))
                                    {
                                        var jm = System.IO.File.ReadAllText(metaPath);
                                        using var doc = System.Text.Json.JsonDocument.Parse(jm);
                                        if (doc.RootElement.TryGetProperty("flipGreen", out var jg) && jg.ValueKind == System.Text.Json.JsonValueKind.True)
                                        {
                                            flip = 1; break;
                                        }
                                    }
                                }
                            }
                        }
                    }
#pragma warning restore CS0618
                }
                catch { }

                mr.FlipNormalY = flip;
            }
            catch { }

            // Store in global cache when possible
            try
            {
                if (a != null && a.Guid != Guid.Empty)
                {
                    lock (_globalCacheLock)
                    {
                        _globalCache[a.Guid] = mr;
                    }
                    // PERFORMANCE: Disabled per-frame cache logs
                    // Console.WriteLine($"[MaterialRuntime] Cached new material {a.Name ?? a.Guid.ToString()} - AlbedoTex={mr.AlbedoTex}, NormalTex={mr.NormalTex}");
                }
            }
            catch { }

            return mr;
        }

        public void Bind(ShaderProgram sh, float time = 0f)
        {
            // Debug: log binding info
            try
            {
                // Only log bind details when verbose logging is enabled to avoid per-frame I/O
                if (Engine.Utils.DebugLogger.EnableVerbose)
                {
                    Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Bind() AlbedoTex={AlbedoTex} NormalTex={NormalTex} AlbedoColor=[{AlbedoColor[0]},{AlbedoColor[1]},{AlbedoColor[2]},{AlbedoColor[3]}]");
                }
            }
            catch { }

            // === BASE TEXTURES ===
            // Albedo sur slot 0
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, AlbedoTex);
            sh.SetInt("u_AlbedoTex", 0);

            // Normal sur slot 1
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, NormalTex);
            sh.SetInt("u_NormalTex", 1);

            // === PBR TEXTURES ===
            // Emissive sur slot 2
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, EmissiveTex);
            sh.SetInt("u_EmissiveTex", 2);
            
            // Metallic sur slot 3
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, MetallicTex);
            sh.SetInt("u_MetallicTex", 3);
            
            // Roughness sur slot 4
            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2D, RoughnessTex);
            sh.SetInt("u_RoughnessTex", 4);
            
            // Metallic-Roughness combined (GLTF) sur slot 5
            GL.ActiveTexture(TextureUnit.Texture5);
            GL.BindTexture(TextureTarget.Texture2D, MetallicRoughnessTex);
            sh.SetInt("u_MetallicRoughnessTex", 5);
            
            // Occlusion sur slot 6
            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2D, OcclusionTex);
            sh.SetInt("u_OcclusionTex", 6);
            
            // Height sur slot 7
            GL.ActiveTexture(TextureUnit.Texture7);
            GL.BindTexture(TextureTarget.Texture2D, HeightTex);
            sh.SetInt("u_HeightTex", 7);
            
            // === DETAIL TEXTURES (slots 16-18 to avoid terrain conflict at 8+) ===
            // DetailMask sur slot 16
            GL.ActiveTexture(TextureUnit.Texture16);
            GL.BindTexture(TextureTarget.Texture2D, DetailMaskTex);
            sh.SetInt("u_DetailMaskTex", 16);
            
            // DetailAlbedo sur slot 17
            GL.ActiveTexture(TextureUnit.Texture17);
            GL.BindTexture(TextureTarget.Texture2D, DetailAlbedoTex);
            sh.SetInt("u_DetailAlbedoTex", 17);
            
            // DetailNormal sur slot 18
            GL.ActiveTexture(TextureUnit.Texture18);
            GL.BindTexture(TextureTarget.Texture2D, DetailNormalTex);
            sh.SetInt("u_DetailNormalTex", 18);

            // Bind per-material normal Y flip flag so shaders that sample normal maps (and SSAO) can match conventions
            try { sh.SetInt("u_FlipNormalY", FlipNormalY); } catch { }

            // === BASE PARAMETERS ===
            sh.SetVec4("u_AlbedoColor", new OpenTK.Mathematics.Vector4(AlbedoColor[0], AlbedoColor[1], AlbedoColor[2], AlbedoColor[3]));
            sh.SetFloat("u_NormalStrength", NormalStrength);
            
            // === PBR PARAMETERS ===
            sh.SetFloat("u_Metallic", Metallic);
            sh.SetFloat("u_Smoothness", Smoothness);
            sh.SetFloat("u_OcclusionStrength", OcclusionStrength);
            sh.SetVec3("u_EmissiveColor", new OpenTK.Mathematics.Vector3(EmissiveColor[0], EmissiveColor[1], EmissiveColor[2]));
            sh.SetFloat("u_HeightScale", HeightScale);
            
            // Texture tiling and offset
            sh.SetVec2("u_TextureTiling", new OpenTK.Mathematics.Vector2(TextureTiling[0], TextureTiling[1]));
            sh.SetVec2("u_TextureOffset", new OpenTK.Mathematics.Vector2(TextureOffset[0], TextureOffset[1]));

            // Triplanar mapping parameters
            sh.SetInt("u_UseTriplanar", UseTriplanar);
            sh.SetFloat("u_TriplanarScale", TriplanarScale);
            sh.SetFloat("u_TriplanarBlendSharpness", TriplanarBlendSharpness);

            sh.SetInt("u_TransparencyMode", TransparencyMode);

            // Alpha clipping parameters
            sh.SetInt("u_AlphaClippingEnabled", AlphaClippingEnabled);
            sh.SetFloat("u_AlphaClipThreshold", AlphaClipThreshold);

            // Stylization parameters
            sh.SetFloat("u_Saturation", Saturation);
            sh.SetFloat("u_Brightness", Brightness);
            sh.SetFloat("u_Contrast", Contrast);
            sh.SetFloat("u_Hue", Hue);
            sh.SetFloat("u_Emission", Emission);

            // Bind terrain layer textures and uniforms if shader expects them
            try
            {
                // bind layer textures starting at unit 4 (0..3 used elsewhere: albedo, normal, SSAO etc.)
                int baseUnit = 8; // choose a higher base to avoid conflicts; Engine code uses 0..3 often
                for (int i = 0; i < LayerCount; i++)
                {
                    int unit = baseUnit + i * 2; // reserve two units per layer (albedo + normal)
                    // Convert integer unit offset into a valid TextureUnit enum by adding to Texture0
                    var albedoTexUnit = (TextureUnit)((int)TextureUnit.Texture0 + unit);
                    GL.ActiveTexture(albedoTexUnit);
                    GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, LayerAlbedoTex[i]);
                    sh.SetInt($"u_LayerAlbedo[{i}]", unit);

                    var normalTexUnit = (TextureUnit)((int)TextureUnit.Texture0 + unit + 1);
                    GL.ActiveTexture(normalTexUnit);
                    GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, LayerNormalTex[i]);
                    sh.SetInt($"u_LayerNormal[{i}]", unit + 1);

                    // tiling/offset
                    var tvo = new OpenTK.Mathematics.Vector4(LayerTilingOffset[i, 0], LayerTilingOffset[i, 1], LayerTilingOffset[i, 2], LayerTilingOffset[i, 3]);
                    sh.SetVec4($"u_LayerTilingOffset[{i}]", tvo);

                    // height/slope
                    var hsp = new OpenTK.Mathematics.Vector4(LayerHeightSlope[i, 0], LayerHeightSlope[i, 1], LayerHeightSlope[i, 2], LayerHeightSlope[i, 3]);
                    sh.SetVec4($"u_LayerHeightSlope[{i}]", hsp);

                    sh.SetFloat($"u_LayerStrength[{i}]", LayerStrength[i]);

                    // Underwater parameters
                    sh.SetInt($"u_LayerIsUnderwater[{i}]", LayerIsUnderwater[i]);
                    var uwp = new OpenTK.Mathematics.Vector4(
                        LayerUnderwaterParams[i, 0],
                        LayerUnderwaterParams[i, 1],
                        LayerUnderwaterParams[i, 2],
                        LayerUnderwaterParams[i, 3]
                    );
                    sh.SetVec4($"u_LayerUnderwaterParams[{i}]", uwp);

                    // Debug log underwater params
                    if (LayerIsUnderwater[i] == 1)
                    {
                        try
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose)
                                Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Layer {i} UNDERWATER: waterLevel={LayerUnderwaterParams[i, 0]}, blend={LayerUnderwaterParams[i, 1]}, slopeMin={LayerUnderwaterParams[i, 2]}, slopeMax={LayerUnderwaterParams[i, 3]}");
                        }
                        catch { }
                    }
                }
                sh.SetInt("u_LayerCount", LayerCount);
            }
            catch { }

            // Bind water shader uniforms if shader is "Water"
            if (string.Equals(ShaderName, "Water", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Binding Water shader uniforms"); } catch { }

                    // Water color and opacity
                    sh.SetVec4("u_WaterColor", new OpenTK.Mathematics.Vector4(WaterColor[0], WaterColor[1], WaterColor[2], WaterColor[3]));
                    sh.SetFloat("u_Opacity", WaterOpacity);

                    // PBR properties
                    sh.SetFloat("u_Metallic", Metallic);
                    sh.SetFloat("u_Smoothness", Smoothness);
                    sh.SetFloat("u_NormalStrength", NormalStrength);

                    // Triplanar mapping
                    sh.SetInt("u_UseTriplanar", UseTriplanar);
                    sh.SetFloat("u_TriplanarScale", TriplanarScale);
                    sh.SetFloat("u_TriplanarBlendSharpness", TriplanarBlendSharpness);

                    // Planar reflection support removed
                }
                catch { }
            }

            // Bind glass shader uniforms if shader is "Glass"
            if (string.Equals(ShaderName, "Glass", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Binding Glass shader uniforms"); } catch { }

                    // Glass refraction properties
                    sh.SetFloat("u_RefractiveIndex", GlassRefractiveIndex);
                    sh.SetFloat("u_DistortionStrength", GlassDistortionStrength);
                    sh.SetFloat("u_ChromaticAberration", GlassChromaticAberration);

                    // Glass appearance
                    sh.SetFloat("u_Roughness", GlassRoughness);
                    sh.SetFloat("u_Thickness", GlassThickness);
                    sh.SetVec3("u_Tint", new OpenTK.Mathematics.Vector3(GlassTint[0], GlassTint[1], GlassTint[2]));
                    sh.SetFloat("u_Opacity", GlassOpacity);

                    // Glass reflections (Fresnel)
                    sh.SetFloat("u_FresnelPower", GlassFresnelPower);
                    sh.SetFloat("u_ReflectionStrength", GlassReflectionStrength);

                    // Normal map strength (for frosted glass effects)
                    sh.SetFloat("u_NormalStrength", NormalStrength);

                    // Scene color texture for refraction (bound on unit 19 by ViewportRenderer)
                    // This texture contains the complete opaque scene (vegetation, particles, etc.)
                    sh.SetInt("u_SceneColorTex", 19);
                }
                catch { }
            }

            // Bind WaterForward shader uniforms if shader is "WaterForward"
            if (string.Equals(ShaderName, "WaterForward", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Phase 1: Base Color
                    sh.SetVec4("u_WaterColor", new OpenTK.Mathematics.Vector4(
                        WaterForwardColor[0], WaterForwardColor[1], WaterForwardColor[2], WaterForwardColor[3]));
                    sh.SetVec4("u_DeepWaterColor", new OpenTK.Mathematics.Vector4(
                        WaterForwardDeepColor[0], WaterForwardDeepColor[1], WaterForwardDeepColor[2], WaterForwardDeepColor[3]));
                    sh.SetFloat("u_Transparency", WaterForwardTransparency);

                    // Wave Animation - Legacy uniforms (deprecated, use u_WaveMode system instead)
                    sh.SetFloat("u_WaveSpeed", WaterForwardWaveSpeed);
                    sh.SetFloat("u_WaveAmplitude", WaterForwardWaveAmplitude);
                    sh.SetFloat("u_WaveFrequency", WaterForwardWaveFrequency);
                    sh.SetVec2("u_WaveDirection", new OpenTK.Mathematics.Vector2(
                        WaterForwardWaveDirection[0], WaterForwardWaveDirection[1]));

                    // Global/Local/Blend system for wave animation
                    sh.SetInt("u_WaveMode", WaterForwardWaveMode);
                    sh.SetFloat("u_WaveBlendFactor", WaterForwardWaveBlendFactor);
                    // Local wave parameters (used when mode != Global)
                    sh.SetFloat("u_WaveSpeed_Local", WaterForwardWaveSpeed);
                    sh.SetFloat("u_WaveAmplitude_Local", WaterForwardWaveAmplitude);
                    sh.SetFloat("u_WaveFrequency_Local", WaterForwardWaveFrequency);
                    sh.SetVec2("u_WaveDirection_Local", new OpenTK.Mathematics.Vector2(
                        WaterForwardWaveDirection[0], WaterForwardWaveDirection[1]));

                    // Phase 2: Normal Mapping (two layers)
                    sh.SetFloat("u_NormalStrength", WaterForwardNormalStrength);
                    sh.SetFloat("u_NormalStrength2", WaterForwardNormalStrength2);
                    sh.SetFloat("u_NormalBlend", WaterForwardNormalBlend);
                    sh.SetFloat("u_NormalMapScale", WaterForwardNormalMapScale); // Legacy fallback
                    sh.SetFloat("u_NormalLayer1Scale", WaterForwardNormalLayer1Scale);
                    sh.SetFloat("u_NormalLayer2Scale", WaterForwardNormalLayer2Scale);
                    sh.SetFloat("u_NormalLayer1Speed", WaterForwardNormalLayer1Speed);
                    sh.SetFloat("u_NormalLayer2Speed", WaterForwardNormalLayer2Speed);
                    sh.SetVec2("u_NormalLayer1Direction_Local", new OpenTK.Mathematics.Vector2(
                        WaterForwardNormalLayer1Direction[0], WaterForwardNormalLayer1Direction[1]));
                    sh.SetVec2("u_NormalLayer2Direction_Local", new OpenTK.Mathematics.Vector2(
                        WaterForwardNormalLayer2Direction[0], WaterForwardNormalLayer2Direction[1]));

                    // Phase 2: Depth & Refraction
                    sh.SetFloat("u_DepthFadeDistance", WaterForwardDepthFadeDistance);
                    sh.SetFloat("u_RefractionStrength", WaterForwardRefractionStrength);
                    sh.SetInt("u_UseRefraction", WaterForwardUseRefraction ? 1 : 0);

                    // Phase 3: PBR & Lighting
                    sh.SetFloat("u_Roughness", WaterForwardRoughness);
                    sh.SetFloat("u_Metallic", WaterForwardMetallic);
                    sh.SetFloat("u_Fresnel", WaterForwardFresnel);
                    sh.SetFloat("u_SpecularStrength", WaterForwardSpecularStrength);

                    // Phase 4: Color Absorption
                    sh.SetVec3("u_AbsorptionColor", new OpenTK.Mathematics.Vector3(
                        WaterForwardAbsorptionColor[0], WaterForwardAbsorptionColor[1], WaterForwardAbsorptionColor[2]));
                    sh.SetFloat("u_AbsorptionStrength", WaterForwardAbsorptionStrength);

                    // Phase 5: Foam
                    sh.SetFloat("u_FoamAmount", WaterForwardFoamAmount);
                    sh.SetFloat("u_FoamCutoff", WaterForwardFoamCutoff);
                    sh.SetVec4("u_FoamColor", new OpenTK.Mathematics.Vector4(
                        WaterForwardFoamColor[0], WaterForwardFoamColor[1], WaterForwardFoamColor[2], WaterForwardFoamColor[3]));
                    sh.SetFloat("u_FoamTextureScale", WaterForwardFoamTextureScale);
                    sh.SetFloat("u_FoamAlphaClipThreshold", WaterForwardFoamAlphaClipThreshold);
                    sh.SetFloat("u_EdgeFadeDistance", WaterForwardEdgeFadeDistance);

                    // Phase 6: AAA Features
                    sh.SetInt("u_UseCaustics", WaterForwardUseCaustics ? 1 : 0);
                    sh.SetFloat("u_CausticsStrength", WaterForwardCausticsStrength);
                    sh.SetFloat("u_CausticsScale", WaterForwardCausticsScale);
                    sh.SetFloat("u_CausticsSpeed", WaterForwardCausticsSpeed);
                    sh.SetVec3("u_CausticsColor", new OpenTK.Mathematics.Vector3(
                        WaterForwardCausticsColor[0], WaterForwardCausticsColor[1], WaterForwardCausticsColor[2]));
                    sh.SetFloat("u_CausticsSplit", WaterForwardCausticsSplit);
                    sh.SetFloat("u_CausticsDistortion", WaterForwardCausticsDistortion);
                    int usePlanarReflections = WaterForwardUsePlanarReflections ? 1 : 0;
                    sh.SetInt("u_UsePlanarReflections", usePlanarReflections);
                    sh.SetFloat("u_ReflectionBlur", WaterForwardReflectionBlur);

                    // DEBUG: Log planar reflections status
                    try { Engine.Utils.DebugLogger.Log($"[WATER SHADER] u_UsePlanarReflections = {usePlanarReflections}"); } catch { }

                    // Texture samplers (using existing texture slots)
                    // u_NormalTex = slot 1 (first normal map layer)
                    // u_NormalTex2 = slot 3 (second normal map layer) - use Metallic texture slot
                    sh.SetInt("u_NormalTex2", 3);
                    // u_FoamTex = slot 2 (foam texture) - use Emissive texture slot
                    sh.SetInt("u_FoamTex", 2);

                    // Depth buffer and scene color texture (bound by ViewportRenderer)
                    sh.SetInt("u_DepthTex", 18);
                    sh.SetInt("u_SceneColorTex", 19);

                    // Planar reflection texture (bound by ViewportRenderer to slot 22)
                    sh.SetInt("u_PlanarReflectionTex", 22);

                    // Planar reflection view-projection matrix
                    sh.SetMat4("u_ReflectionViewProj", Engine.Rendering.ReflectionBuffer.ReflectionViewProj);

                    // CRITICAL: Bind GLOBAL wind parameters from WeatherComponent
                    // These are used when WaveMode = 0 (Global) or 2 (Blend)
                    // This makes water waves and normal directions follow the wind!
                    try
                    {
                        var wm = Engine.Systems.WeatherManager.GetCurrentWeather();
                        if (wm != null)
                        {
                            sh.SetFloat("u_WindStrength", wm.WindStrength);
                            sh.SetVec2("u_WindDirection", new OpenTK.Mathematics.Vector2(wm.GetWindDirection().X, wm.GetWindDirection().Y));
                            sh.SetFloat("u_WindSpeed", wm.WindSpeed);
                            sh.SetFloat("u_WindGustiness", wm.WindGustiness);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Error binding WaterForward uniforms: {ex.Message}"); } catch { }
                }
            }

            // Bind VegetationForward shader uniforms if shader is "VegetationForward"
            if (string.Equals(ShaderName, "VegetationForward", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Global/Local/Blend system for wind animation
                    sh.SetInt("u_WindMode", VegetationWindMode);
                    sh.SetFloat("u_WindBlendFactor", VegetationWindBlendFactor);

                    // Local wind parameters (used when mode != Global)
                    sh.SetFloat("u_WindStrength_Local", VegetationWindStrength);
                    sh.SetVec2("u_WindDirection_Local", new OpenTK.Mathematics.Vector2(
                        VegetationWindDirection[0], VegetationWindDirection[1]));
                    sh.SetFloat("u_WindSpeed_Local", VegetationWindSpeed);
                    sh.SetFloat("u_WindGustiness_Local", VegetationWindGustiness);

                    // Advanced wind parameters (branches, trunk, leaves)
                    sh.SetFloat("u_BranchAmplitude_Local", VegetationBranchAmplitude);
                    sh.SetFloat("u_BranchSpeed_Local", VegetationBranchSpeed);
                    sh.SetFloat("u_BranchTurbulence_Local", VegetationBranchTurbulence);
                    sh.SetFloat("u_TrunkStiffness_Local", VegetationTrunkStiffness);
                    sh.SetFloat("u_TrunkBendAmount_Local", VegetationTrunkBendAmount);
                    sh.SetFloat("u_LeafFlutter_Local", VegetationLeafFlutter);
                    sh.SetFloat("u_LeafFlutterSpeed_Local", VegetationLeafFlutterSpeed);

                    // CRITICAL: Bind GLOBAL wind parameters from WeatherComponent
                    // These are used when WindMode = 0 (Global) or 2 (Blend)
                    try
                    {
                        var wm = Engine.Systems.WeatherManager.GetCurrentWeather();
                        if (wm != null)
                        {
                            // Primary wind parameters
                            sh.SetFloat("u_WindStrength", wm.WindStrength);
                            sh.SetVec2("u_WindDirection", new OpenTK.Mathematics.Vector2(wm.GetWindDirection().X, wm.GetWindDirection().Y));
                            sh.SetFloat("u_WindSpeed", wm.WindSpeed);
                            sh.SetFloat("u_WindGustiness", wm.WindGustiness);

                            // Advanced wind parameters
                            sh.SetFloat("u_BranchAmplitude", wm.BranchAmplitude);
                            sh.SetFloat("u_BranchSpeed", wm.BranchSpeed);
                            sh.SetFloat("u_BranchTurbulence", wm.BranchTurbulence);
                            sh.SetFloat("u_TrunkStiffness", wm.TrunkStiffness);
                            sh.SetFloat("u_TrunkBendAmount", wm.TrunkBendAmount);
                            sh.SetFloat("u_LeafFlutter", wm.LeafFlutter);
                            sh.SetFloat("u_LeafFlutterSpeed", wm.LeafFlutterSpeed);

                            // Weather effects
                            sh.SetFloat("u_RainIntensity", wm.RainIntensity);
                            sh.SetFloat("u_SnowAccumulation", wm.SnowAccumulation);
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Error binding VegetationForward uniforms: {ex.Message}"); } catch { }
                }
            }

            // Set time uniform for all shaders that need animation (Water, Vegetation, BlackHole, etc.)
            // NOTE: We use u_Time (lowercase) not uTime (Global UBO) because shadow shaders expect u_Time
            try
            {
                sh.SetFloat("u_Time", time);
            }
            catch { }

            // === IBL / Environment maps (global) ===
            try
            {
                int hasIbl = 0;
                uint irr = SkyboxRenderer.IrradianceMap;
                uint pref = SkyboxRenderer.PrefilteredEnvMap;
                uint brdf = SkyboxRenderer.BRDFLUTTexture;

                // CRITICAL: Always bind ALL sampler uniforms to avoid InvalidOperation
                // Even if IBL is disabled, OpenGL requires all samplers to be bound

                // Irradiance cubemap -> unit 10
                GL.ActiveTexture(TextureUnit.Texture10);
                if (irr != 0)
                {
                    hasIbl = 1;
                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)irr);
                    // CRITICAL FIX: Irradiance map is 32x32 with NO mipmaps, use Linear NOT LinearMipmapLinear
                    // Using mipmap filter on a single-level texture causes black/incorrect sampling
                    try
                    {
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                        try { GL.Enable(EnableCap.TextureCubeMapSeamless); } catch { }
                    }
                    catch { }
                }
                else
                {
                    GL.BindTexture(TextureTarget.TextureCubeMap, 0); // Bind null/default
                }
                sh.SetInt("u_IrradianceMap", 10);

                // Prefiltered env map -> unit 11
                GL.ActiveTexture(TextureUnit.Texture11);
                if (pref != 0)
                {
                    GL.BindTexture(TextureTarget.TextureCubeMap, (int)pref);
                    // Ensure the sampler parameters are appropriate for trilinear sampling
                    // Some platforms or texture sources might have wrong filtering (nearest)
                    // which causes blocky/quantized reflections. Force linear mipmap filtering
                    // and clamp-to-edge wrapping here as a safety measure.
                    try
                    {
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
                        // Ensure GL knows the max mip level (some loaders don't set this)
                        try {
                            int maxLvl = (int)Math.Max(0.0f, Math.Floor(SkyboxRenderer.PrefilterMaxLod));
                            GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMaxLevel, maxLvl);
                        } catch { }
                        // Enable seamless cubemap sampling to avoid seam artifacts
                        try { GL.Enable(EnableCap.TextureCubeMapSeamless); } catch { }
                    }
                    catch { }
                }
                else
                {
                    GL.BindTexture(TextureTarget.TextureCubeMap, 0);
                }
                sh.SetInt("u_PrefilteredEnvMap", 11);

                // BRDF LUT -> unit 12
                GL.ActiveTexture(TextureUnit.Texture12);
                if (brdf != 0)
                {
                    GL.BindTexture(TextureTarget.Texture2D, (int)brdf);
                }
                else
                {
                    GL.BindTexture(TextureTarget.Texture2D, Engine.Rendering.TextureCache.White1x1);
                }
                sh.SetInt("u_BRDFLUT", 12);

                sh.SetInt("u_HasIBL", hasIbl);
                try {
                    if (Engine.Utils.DebugLogger.EnableVerbose == true) try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] IBL bound: hasIbl={hasIbl}, irr={irr}, pref={pref}, maxLod={SkyboxRenderer.PrefilterMaxLod}"); } catch { }
                } catch { }
                // Additional diagnostic: warn if prefiltered env map exists but PrefilterMaxLod not set yet
                try
                {
                    if (pref != 0 && SkyboxRenderer.PrefilterMaxLod <= 0)
                    {
                        try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] WARNING: PrefilteredEnvMap bound (handle={pref}) but PrefilterMaxLod={SkyboxRenderer.PrefilterMaxLod}"); } catch { }
                    }
                }
                catch { }
                // If PrefilterMaxLod looks invalid (race or not yet set), compute it from the bound cubemap
                try
                {
                    if (SkyboxRenderer.PrefilterMaxLod <= 0.0f && pref != 0)
                    {
                        int computed = -1;
                        // Try to read the texture's TextureMaxLevel first
                        GL.BindTexture(TextureTarget.TextureCubeMap, (int)pref);
                        GL.GetTexParameter(TextureTarget.TextureCubeMap, GetTextureParameter.TextureMaxLevel, out int texMaxLevel);
                        if (texMaxLevel > 0)
                        {
                            computed = texMaxLevel;
                        }
                        else
                        {
                            // Fallback: probe mip levels and find the last non-zero width
                            int lastNonZero = -1;
                            for (int level = 0; level < 16; level++)
                            {
                                GL.GetTexLevelParameter(TextureTarget.TextureCubeMapPositiveX, level, GetTextureParameter.TextureWidth, out int w);
                                if (w > 0) lastNonZero = level; else break;
                            }
                            if (lastNonZero >= 0) computed = lastNonZero;
                        }
                        GL.BindTexture(TextureTarget.TextureCubeMap, 0);

                        if (computed >= 0)
                        {
                            SkyboxRenderer.PrefilterMaxLod = computed;
                            try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Computed PrefilterMaxLod={computed} from texture handle={(int)pref}"); } catch { }
                        }
                    }
                }
                catch { }

                // Set prefilter max LOD based on actual cubemap mipmap levels
                sh.SetFloat("u_PrefilterMaxLod", SkyboxRenderer.PrefilterMaxLod);
            }
            catch { }
            
            // === Ambient / Skybox uniforms (from current lighting state) ===
            try
            {
                var ls = SkyboxRenderer.CurrentLightingState;
                if (ls != null)
                {
                    sh.SetVec3("uAmbientColor", ls.AmbientColor);
                    sh.SetFloat("uAmbientIntensity", ls.AmbientIntensity);
                    sh.SetVec3("uSkyboxTint", ls.SkyboxTint);
                    sh.SetFloat("uSkyboxExposure", ls.SkyboxExposure);
                }
                else
                {
                    sh.SetVec3("uAmbientColor", new OpenTK.Mathematics.Vector3(0.05f, 0.05f, 0.05f));
                    sh.SetFloat("uAmbientIntensity", 1.0f);
                    sh.SetVec3("uSkyboxTint", new OpenTK.Mathematics.Vector3(1f, 1f, 1f));
                    sh.SetFloat("uSkyboxExposure", 1.0f);
                }
            }
            catch { }

            // FIX C4: CRITICAL - Reset active texture unit to 0 to avoid state pollution
            // This prevents texture binding conflicts in subsequent render calls
            GL.ActiveTexture(TextureUnit.Texture0);
        }
    }
}
