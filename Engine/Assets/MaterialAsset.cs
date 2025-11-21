using System;
using System.IO;
using System.Text.Json;

namespace Engine.Assets
{
    public sealed class MaterialAsset
    {
        public Guid Guid { get; set; }
        public string? Name { get; set; }
        public string? Shader { get; set; } = "ForwardBase"; // Shader name (e.g., "ForwardBase", "TerrainForward", etc.)
        // Terrain-specific layers (used when Shader == "TerrainForward")
        public Engine.Assets.TerrainLayer[]? TerrainLayers { get; set; }
        
        // === PBR TEXTURES (Unity-compatible) ===
        public Guid? AlbedoTexture { get; set; }
        public float[] AlbedoColor { get; set; } = new float[] {1,1,1,1};
        
        public Guid? NormalTexture { get; set; }
        public float NormalStrength { get; set; } = 1.0f;
        
        public Guid? MetallicTexture { get; set; }      // Metallic map (grayscale)
        public Guid? RoughnessTexture { get; set; }     // Roughness map (grayscale) 
        public Guid? MetallicRoughnessTexture { get; set; }  // Combined metallic-roughness (GLTF 2.0 standard: R=unused, G=roughness, B=metallic)
        
        public Guid? OcclusionTexture { get; set; }     // Ambient Occlusion map (grayscale)
        public float OcclusionStrength { get; set; } = 1.0f;
        
        public Guid? EmissiveTexture { get; set; }      // Emissive/Glow texture (RGB)
        public float[] EmissiveColor { get; set; } = new float[] {1f, 1f, 1f}; // Emissive tint color (white by default)
        
        public Guid? HeightTexture { get; set; }        // Height/Parallax map (grayscale)
        public float HeightScale { get; set; } = 0.05f;
        
        public Guid? DetailMaskTexture { get; set; }    // Detail mask (grayscale, controls detail texture blending)
        public Guid? DetailAlbedoTexture { get; set; }  // Detail albedo (overlaid on main albedo)
        public Guid? DetailNormalTexture { get; set; }  // Detail normal (overlaid on main normal)
        
        // === PBR PARAMETERS ===
        public float Metallic { get; set; } = 0f;
        public float Roughness { get; set; } = 0.5f;

        // Texture tiling and offset properties
        public float[] TextureTiling { get; set; } = new float[] { 1f, 1f };     // UV scale (X, Y)
        public float[] TextureOffset { get; set; } = new float[] { 0f, 0f };    // UV offset (X, Y)

        // Rendering mode: 0 = Opaque, 1 = Transparent
        public int TransparencyMode { get; set; } = 0;

        // Opacity/Alpha control (for transparency)
        public float Opacity { get; set; } = 1.0f;  // 0.0 = fully transparent, 1.0 = fully opaque

        // Stylization parameters (for artistic tweaking)
        public float Saturation { get; set; } = 1.0f;   // 0.0 = grayscale, 1.0 = normal, >1.0 = oversaturated
        public float Brightness { get; set; } = 1.0f;   // 0.0 = black, 1.0 = normal, >1.0 = brighter
        public float Contrast { get; set; } = 1.0f;     // 0.0 = flat gray, 1.0 = normal, >1.0 = more contrast
        public float Hue { get; set; } = 0.0f;          // -1.0 to 1.0, shifts hue (color wheel rotation)
        public float Emission { get; set; } = 0.0f;     // 0.0 = no emission, >0.0 = emissive/glow strength

        // Water-specific properties (used when Shader == "Water")
        public WaterMaterialProperties? WaterProperties { get; set; }

        public static MaterialAsset Load(string file)
        {
            var mat = JsonSerializer.Deserialize<MaterialAsset>(File.ReadAllText(file))!;
            
            // No longer need migration logic - default property values handle missing fields correctly
            // The JsonSerializer will use the property initializers (1.0f) if fields are absent from JSON
            
            return mat;
        }
        public static void Save(string file, MaterialAsset mat)
            => File.WriteAllText(file, JsonSerializer.Serialize(mat, new JsonSerializerOptions{WriteIndented=true}));

        /// <summary>
        /// Save material atomically by writing to a temporary file in the same folder
        /// and then replacing the destination file. This helps avoid readers seeing
        /// partially-written files which can cause reload/revert races.
        /// </summary>
        public static void SaveAtomic(string file, MaterialAsset mat)
        {
            var dir = Path.GetDirectoryName(file) ?? Path.GetTempPath();
            var tmp = Path.Combine(dir, Path.GetFileName(file) + ".tmp");
            // Write to temp file first
            File.WriteAllText(tmp, JsonSerializer.Serialize(mat, new JsonSerializerOptions { WriteIndented = true }));
            // Copy over the target atomically (overwrite)
            File.Copy(tmp, file, overwrite: true);
            try { File.Delete(tmp); } catch { }
        }
    }
}
