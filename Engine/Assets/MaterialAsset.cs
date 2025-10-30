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
        public Guid? AlbedoTexture { get; set; }
        public float[] AlbedoColor { get; set; } = new float[] {1,1,1,1};
        public Guid? NormalTexture { get; set; }  // Nouvelle propriété
        public float NormalStrength { get; set; } = 1.0f;  // Nouvelle propriété
        public float Metallic { get; set; } = 0f;
        public float Roughness { get; set; } = 0.5f;
        
        // Texture tiling and offset properties
        public float[] TextureTiling { get; set; } = new float[] { 1f, 1f };     // UV scale (X, Y)
        public float[] TextureOffset { get; set; } = new float[] { 0f, 0f };    // UV offset (X, Y)
    // Rendering mode: 0 = Opaque, 1 = Transparent
    public int TransparencyMode { get; set; } = 0;

        // Water-specific properties (used when Shader == "Water")
        public WaterMaterialProperties? WaterProperties { get; set; }

        public static MaterialAsset Load(string file)
            => JsonSerializer.Deserialize<MaterialAsset>(File.ReadAllText(file))!;
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
