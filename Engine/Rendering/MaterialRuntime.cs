using System;
using Engine.Assets;
using OpenTK.Graphics.OpenGL4;

namespace Engine.Rendering
{
    public sealed class MaterialRuntime
    {
        // Global cache shared across renderers to avoid reloading materials when multiple renderers exist
    private static readonly System.Collections.Generic.Dictionary<Guid, MaterialRuntime> _globalCache = new();
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
                _globalCache.Remove(guid);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaterialRuntime] ✗ Error in OnMaterialSaved: {ex.Message}");
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
                int count = _globalCache.Count;
                _globalCache.Clear();
                // PERFORMANCE: Disabled log
                // Console.WriteLine($"[MaterialRuntime] ✓ Cleared global cache ({count} materials)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MaterialRuntime] ✗ Error clearing global cache: {ex.Message}");
            }
        }

        public Guid AssetGuid;
        public int AlbedoTex = 0;
        public float[] AlbedoColor = new float[] { 1, 1, 1, 1 };
        public int NormalTex = 0;          // Nouvelle propriété
        public float NormalStrength = 1.0f; // Nouvelle propriété
    // Per-material normal green-channel flip flag (0 = no flip, 1 = flip)
    public int FlipNormalY = 0;
        public float Metallic = 0f;
        public float Smoothness = 0.5f;
        
        // Texture tiling and offset
        public float[] TextureTiling = new float[] { 1f, 1f };
        public float[] TextureOffset = new float[] { 0f, 0f };
    public int TransparencyMode = 0; // 0 = Opaque, 1 = Transparent
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

        // Water shader properties
        public float WaveAmplitude = 0.1f;
        public float WaveFrequency = 1.0f;
        public float WaveSpeed = 1.0f;
        public float[] WaveDirection = new float[] { 1f, 0f };
        public float[] WaterColor = new float[] { 0.1f, 0.3f, 0.5f, 0.8f };
        public float Opacity = 0.8f;
        public bool IsOpaque = false;
        public float[] AlbedoTiling = new float[] { 1f, 1f };
        public float[] AlbedoScrollSpeed = new float[] { 0f, 0f };
        public float[] NormalTiling = new float[] { 1f, 1f };
        public float[] NormalScrollSpeed1 = new float[] { 0.05f, 0.03f };
        public float[] NormalScrollSpeed2 = new float[] { -0.04f, -0.06f };
        public int NoiseTexture1 = 0;
        public int NoiseTexture2 = 0;
        public float[] Noise1Speed = new float[] { 0.03f, 0.03f };
        public float[] Noise1Direction = new float[] { 1f, 0f };
        public float[] Noise1Tiling = new float[] { 1f, 1f };
        public float Noise1Strength = 0.05f;
        public float[] Noise2Speed = new float[] { 0.02f, -0.02f };
        public float[] Noise2Direction = new float[] { 0f, 1f };
        public float[] Noise2Tiling = new float[] { 1.5f, 1.5f };
        public float Noise2Strength = 0.03f;
        public float RefractionStrength = 0.5f;
        public float FresnelPower = 2.0f;
        public float[] FresnelColor = new float[] { 0.8f, 0.9f, 1.0f, 1.0f };
        public float TessellationLevel = 32.0f;

    // Use procedural noise for displacement (true) or sample noise textures (false) for cheaper rendering
    public bool UseProceduralNoise = true;

        // Planar Reflection properties
        public bool EnableReflection = false;
        public int ReflectionTexture = 0;
        public float ReflectionStrength = 1.0f;
        public float ReflectionBlur = 0.0f;

        public static MaterialRuntime FromAsset(MaterialAsset a, Func<Guid, string?> resolvePath)
        {
            TextureCache.Initialize();
            try
            {
                Engine.Utils.DebugLogger.Log($"[MaterialRuntime] FromAsset: Creating runtime for material {a.Guid} (assetName={a.Name})");
            }
            catch { }
            // Return cached runtime if available
            if (a != null && a.Guid != Guid.Empty)
            {
                if (_globalCache.TryGetValue(a.Guid, out var cached))
                {
                    // PERFORMANCE: Disabled per-frame cache logs
                    // Console.WriteLine($"[MaterialRuntime] Cache HIT for {a.Name ?? a.Guid.ToString()} - AlbedoTex={cached.AlbedoTex}, NormalTex={cached.NormalTex}");
                    return cached;
                }
                // PERFORMANCE: Disabled per-frame cache logs
                // else
                // {
                //     Console.WriteLine($"[MaterialRuntime] Cache MISS for {a.Name ?? a.Guid.ToString()} - loading textures...");
                // }
            }
            var albedoPath = a?.AlbedoTexture.HasValue == true ? resolvePath(a.AlbedoTexture.Value) : null;
            var normalPath = a?.NormalTexture.HasValue == true ? resolvePath(a.NormalTexture.Value) : null;
        // (previous misplaced initialization removed) - Water properties are applied below after mr is created
            // For Water shader, use default white textures initially (will be overridden by WaterProperties)
            bool isWaterShader = string.Equals(a?.Shader, "Water", StringComparison.OrdinalIgnoreCase);
            
            var mr = new MaterialRuntime
            {
                AssetGuid = a?.Guid ?? Guid.Empty,
                AlbedoTex = !isWaterShader && a?.AlbedoTexture.HasValue == true ? TextureCache.GetOrLoad(a.AlbedoTexture.Value, resolvePath) : TextureCache.White1x1,
                AlbedoColor = !isWaterShader ? (a?.AlbedoColor ?? new[] { 1f, 1f, 1f, 1f }) : new[] { 1f, 1f, 1f, 1f },
                NormalTex = !isWaterShader && a?.NormalTexture.HasValue == true ? TextureCache.GetOrLoad(a.NormalTexture.Value, resolvePath) : TextureCache.White1x1,
                NormalStrength = !isWaterShader ? (a?.NormalStrength ?? 1.0f) : 1.0f,
                Metallic = !isWaterShader ? (a?.Metallic ?? 0f) : 0f,
                Smoothness = !isWaterShader ? (a != null ? 1.0f - a.Roughness : 0.5f) : 0.9f,
                TextureTiling = a?.TextureTiling ?? new[] { 1f, 1f },
                TextureOffset = a?.TextureOffset ?? new[] { 0f, 0f }
                ,
                TransparencyMode = a?.Guid != Guid.Empty ? 0 : 0 // placeholder, will be set below
            };
            // Shader name from asset if present
            try { mr.ShaderName = a?.Shader; } catch { mr.ShaderName = null; }

            // Initialize all layer arrays to zero/defaults
            for (int i = 0; i < MAX_LAYERS; i++)
            {
                mr.LayerIsUnderwater[i] = 0;
                mr.LayerStrength[i] = 0f;
            }

            // Map terrain layers if present
            try
            {
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

                        // Debug: log layer loading
                        try
                        {
                            Console.WriteLine($"[MaterialRuntime] Loading Layer {i}: IsUnderwater={l.IsUnderwater}, waterLevel={l.UnderwaterHeightMax}, blend={l.UnderwaterBlendDistance}");
                        }
                        catch { }
                    }
                }
            }
            catch { }
            // Determine transparency mode from asset if available
            try
            {
                mr.TransparencyMode = a != null ? a.GetType().GetProperty("TransparencyMode")?.GetValue(a) is int tm ? tm : 0 : 0;
            }
            catch
            {
                mr.TransparencyMode = 0;
            }

            // Load water properties if present
            try
            {
                if (a?.WaterProperties != null)
                {
                    try { Engine.Utils.DebugLogger.Log($"[MaterialRuntime] Loading Water properties for material {a.Name}"); } catch { }
                    var w = a.WaterProperties;
                    mr.WaveAmplitude = w.WaveAmplitude;
                    mr.WaveFrequency = w.WaveFrequency;
                    mr.WaveSpeed = w.WaveSpeed;
                    mr.WaveDirection = w.WaveDirection ?? new float[] { 1f, 0f };
                    mr.WaterColor = w.WaterColor ?? new float[] { 0.1f, 0.3f, 0.5f, 0.8f };
                    mr.Opacity = w.Opacity;
                    
                    // Load water-specific textures (override base material textures)
                    if (w.AlbedoTexture.HasValue)
                    {
                        mr.AlbedoTex = TextureCache.GetOrLoad(w.AlbedoTexture.Value, resolvePath);
                        mr.AlbedoColor = w.AlbedoColor ?? new float[] { 1f, 1f, 1f, 1f };
                    }
                    mr.AlbedoTiling = w.AlbedoTiling ?? new float[] { 1f, 1f };
                    mr.AlbedoScrollSpeed = w.AlbedoScrollSpeed ?? new float[] { 0f, 0f };

                    if (w.NormalTexture.HasValue)
                    {
                        mr.NormalTex = TextureCache.GetOrLoad(w.NormalTexture.Value, resolvePath);
                        mr.NormalStrength = w.NormalStrength;
                    }
                    mr.NormalTiling = w.NormalTiling ?? new float[] { 1f, 1f };
                    mr.NormalScrollSpeed1 = w.NormalScrollSpeed1 ?? new float[] { 0.05f, 0.03f };
                    mr.NormalScrollSpeed2 = w.NormalScrollSpeed2 ?? new float[] { -0.04f, -0.06f };

                    // PBR properties
                    mr.Metallic = w.Metallic;
                    mr.Smoothness = w.Smoothness;
                    
                    mr.NoiseTexture1 = w.NoiseTexture1.HasValue ? TextureCache.GetOrLoad(w.NoiseTexture1.Value, resolvePath) : TextureCache.White1x1;
                    mr.NoiseTexture2 = w.NoiseTexture2.HasValue ? TextureCache.GetOrLoad(w.NoiseTexture2.Value, resolvePath) : TextureCache.White1x1;
                    mr.Noise1Speed = w.Noise1Speed ?? new float[] { 0.03f, 0.03f };
                    mr.Noise1Direction = w.Noise1Direction ?? new float[] { 1f, 0f };
                    mr.Noise1Tiling = w.Noise1Tiling ?? new float[] { 1f, 1f };
                    mr.Noise1Strength = w.Noise1Strength;
                    mr.Noise2Speed = w.Noise2Speed ?? new float[] { 0.02f, -0.02f };
                    mr.Noise2Direction = w.Noise2Direction ?? new float[] { 0f, 1f };
                    mr.Noise2Tiling = w.Noise2Tiling ?? new float[] { 1.5f, 1.5f };
                    mr.Noise2Strength = w.Noise2Strength;
                    mr.RefractionStrength = w.RefractionStrength;
                    mr.FresnelPower = w.FresnelPower;
                    mr.FresnelColor = w.FresnelColor ?? new float[] { 0.8f, 0.9f, 1.0f, 1.0f };
                    mr.UseProceduralNoise = w.UseProceduralNoise;
                    mr.TessellationLevel = w.TessellationLevel;

                    // Planar Reflection (texture is set by ViewportRenderer from auto-generated reflection)
                    mr.EnableReflection = w.EnableReflection;
                    mr.ReflectionTexture = 0; // Will be set by ViewportRenderer
                    mr.ReflectionStrength = w.ReflectionStrength;
                    mr.ReflectionBlur = w.ReflectionBlur;
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

                // If not determined yet, check water override normal texture
                try
                {
                    if (flip == 0 && a?.WaterProperties != null && a.WaterProperties.NormalTexture.HasValue)
                    {
                        var p = resolvePath(a.WaterProperties.NormalTexture.Value);
                        if (!string.IsNullOrEmpty(p))
                        {
                            var metaPath = p + Engine.Assets.AssetDatabase.MetaExt;
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
                }
                catch { }

                // If still not determined, inspect layer normal textures (first found wins)
                try
                {
                    if (flip == 0 && a?.TerrainLayers != null)
                    {
#pragma warning disable CS0618 // inspect legacy NormalTexture meta for compatibility
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
#pragma warning restore CS0618
                    }
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
                    _globalCache[a.Guid] = mr;
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

            // Albedo sur slot 0
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, AlbedoTex);
            sh.SetInt("u_AlbedoTex", 0);

            // Normal sur slot 1
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, NormalTex);
            sh.SetInt("u_NormalTex", 1);

            // Bind per-material normal Y flip flag so shaders that sample normal maps (and SSAO) can match conventions
            try { sh.SetInt("u_FlipNormalY", FlipNormalY); } catch { }

            sh.SetVec4("u_AlbedoColor", new OpenTK.Mathematics.Vector4(AlbedoColor[0], AlbedoColor[1], AlbedoColor[2], AlbedoColor[3]));
            sh.SetFloat("u_NormalStrength", NormalStrength);
            sh.SetFloat("u_Metallic", Metallic);
            sh.SetFloat("u_Smoothness", Smoothness);
            
            // Texture tiling and offset
            sh.SetVec2("u_TextureTiling", new OpenTK.Mathematics.Vector2(TextureTiling[0], TextureTiling[1]));
            sh.SetVec2("u_TextureOffset", new OpenTK.Mathematics.Vector2(TextureOffset[0], TextureOffset[1]));
            sh.SetInt("u_TransparencyMode", TransparencyMode);

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
                                Console.WriteLine($"[MaterialRuntime] Layer {i} UNDERWATER: waterLevel={LayerUnderwaterParams[i, 0]}, blend={LayerUnderwaterParams[i, 1]}, slopeMin={LayerUnderwaterParams[i, 2]}, slopeMax={LayerUnderwaterParams[i, 3]}");
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
                    // Albedo texture is already bound to slot 0, just set the uniform
                    sh.SetInt("u_AlbedoTexture", 0);
                    sh.SetVec4("u_AlbedoColor", new OpenTK.Mathematics.Vector4(AlbedoColor[0], AlbedoColor[1], AlbedoColor[2], AlbedoColor[3]));
                    sh.SetVec2("u_AlbedoTiling", new OpenTK.Mathematics.Vector2(AlbedoTiling[0], AlbedoTiling[1]));
                    sh.SetVec2("u_AlbedoScrollSpeed", new OpenTK.Mathematics.Vector2(AlbedoScrollSpeed[0], AlbedoScrollSpeed[1]));

                    // Normal map is already bound to slot 1
                    sh.SetInt("u_NormalMap", 1);
                    sh.SetFloat("u_NormalMapStrength", NormalStrength);
                    sh.SetVec2("u_NormalTiling", new OpenTK.Mathematics.Vector2(NormalTiling[0], NormalTiling[1]));
                    sh.SetVec2("u_NormalScrollSpeed1", new OpenTK.Mathematics.Vector2(NormalScrollSpeed1[0], NormalScrollSpeed1[1]));
                    sh.SetVec2("u_NormalScrollSpeed2", new OpenTK.Mathematics.Vector2(NormalScrollSpeed2[0], NormalScrollSpeed2[1]));

                    // PBR properties
                    sh.SetFloat("u_Metallic", Metallic);
                    sh.SetFloat("u_Smoothness", Smoothness);

                    // Texture tiling and offset (from base material properties)
                    sh.SetVec2("u_TextureTiling", new OpenTK.Mathematics.Vector2(TextureTiling[0], TextureTiling[1]));
                    sh.SetVec2("u_TextureOffset", new OpenTK.Mathematics.Vector2(TextureOffset[0], TextureOffset[1]));

                    // Time uniform for animation
                    sh.SetFloat("u_Time", time);

                    // Tessellation level
                    sh.SetFloat("u_TessellationLevel", TessellationLevel);

                    // Wave parameters
                    sh.SetFloat("u_WaveAmplitude", WaveAmplitude);
                    sh.SetFloat("u_WaveFrequency", WaveFrequency);
                    sh.SetFloat("u_WaveSpeed", WaveSpeed);
                    sh.SetVec2("u_WaveDirection", new OpenTK.Mathematics.Vector2(WaveDirection[0], WaveDirection[1]));

                    // Water appearance
                    sh.SetVec4("u_WaterColor", new OpenTK.Mathematics.Vector4(WaterColor[0], WaterColor[1], WaterColor[2], WaterColor[3]));
                    sh.SetFloat("u_Opacity", Opacity);

                    // Bind noise textures (using slots 4 and 5 to avoid conflict with SSAO on slot 3)
                    GL.ActiveTexture(TextureUnit.Texture4);
                    GL.BindTexture(TextureTarget.Texture2D, NoiseTexture1);
                    sh.SetInt("u_NoiseTexture1", 4);

                    GL.ActiveTexture(TextureUnit.Texture5);
                    GL.BindTexture(TextureTarget.Texture2D, NoiseTexture2);
                    sh.SetInt("u_NoiseTexture2", 5);

                    // Noise 1 parameters
                    sh.SetVec2("u_Noise1Speed", new OpenTK.Mathematics.Vector2(Noise1Speed[0], Noise1Speed[1]));
                    sh.SetVec2("u_Noise1Direction", new OpenTK.Mathematics.Vector2(Noise1Direction[0], Noise1Direction[1]));
                    sh.SetVec2("u_Noise1Tiling", new OpenTK.Mathematics.Vector2(Noise1Tiling[0], Noise1Tiling[1]));
                    sh.SetFloat("u_Noise1Strength", Noise1Strength);

                    // Noise 2 parameters
                    sh.SetVec2("u_Noise2Speed", new OpenTK.Mathematics.Vector2(Noise2Speed[0], Noise2Speed[1]));
                    sh.SetVec2("u_Noise2Direction", new OpenTK.Mathematics.Vector2(Noise2Direction[0], Noise2Direction[1]));
                    sh.SetVec2("u_Noise2Tiling", new OpenTK.Mathematics.Vector2(Noise2Tiling[0], Noise2Tiling[1]));
                    sh.SetFloat("u_Noise2Strength", Noise2Strength);

                    // Use procedural noise flag (1 = procedural snoise, 0 = sample noise textures)
                    sh.SetInt("u_UseProceduralNoise", UseProceduralNoise ? 1 : 0);

                    // Fresnel and refraction
                    sh.SetFloat("u_FresnelPower", FresnelPower);
                    sh.SetVec4("u_FresnelColor", new OpenTK.Mathematics.Vector4(FresnelColor[0], FresnelColor[1], FresnelColor[2], FresnelColor[3]));
                    sh.SetFloat("u_RefractionStrength", RefractionStrength);

                    // Planar Reflection (using slot 6 to avoid conflicts)
                    sh.SetInt("u_EnableReflection", EnableReflection ? 1 : 0);
                    if (EnableReflection)
                    {
                        try
                        {
                            if (Engine.Utils.DebugLogger.EnableVerbose)
                                Console.WriteLine($"[MaterialRuntime] Binding reflection texture: ID={ReflectionTexture}, Strength={ReflectionStrength}");
                        }
                        catch { }

                        GL.ActiveTexture(TextureUnit.Texture6);
                        GL.BindTexture(TextureTarget.Texture2D, ReflectionTexture);
                        sh.SetInt("u_ReflectionTexture", 6);
                        sh.SetFloat("u_ReflectionStrength", ReflectionStrength);
                        sh.SetFloat("u_ReflectionBlur", ReflectionBlur);
                    }
                }
                catch { }
            }
        }
    }
}
