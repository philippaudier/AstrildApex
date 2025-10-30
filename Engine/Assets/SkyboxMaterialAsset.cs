using System;
using System.IO;
using System.Text.Json;

namespace Engine.Assets
{
    /// <summary>
    /// Skybox material asset supporting Unity-like skybox types
    /// </summary>
    public sealed class SkyboxMaterialAsset
    {
        public Guid Guid { get; set; }
        public string? Name { get; set; }
        public SkyboxType Type { get; set; } = SkyboxType.Procedural;
        
        // === Procedural Skybox Properties (Unity-like) ===
        public float[] SkyTint { get; set; } = new float[] { 0.5f, 0.5f, 0.5f, 1.0f };
        public float[] GroundColor { get; set; } = new float[] { 0.369f, 0.349f, 0.341f, 1.0f };
        public float Exposure { get; set; } = 1.3f;
        public float AtmosphereThickness { get; set; } = 1.0f;
        public float[] SunTint { get; set; } = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float SunSize { get; set; } = 0.04f;
        public float SunSizeConvergence { get; set; } = 5.0f;
        
        // === Cubemap Skybox Properties ===
        public Guid? CubemapTexture { get; set; }
        public float[] CubemapTint { get; set; } = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float CubemapExposure { get; set; } = 1.0f;
        public float CubemapRotation { get; set; } = 0.0f;
        
        // === 6-Sided Skybox Properties ===
        public Guid? FrontTexture { get; set; }  // +Z
        public Guid? BackTexture { get; set; }   // -Z
        public Guid? LeftTexture { get; set; }   // -X
        public Guid? RightTexture { get; set; }  // +X
        public Guid? UpTexture { get; set; }     // +Y
        public Guid? DownTexture { get; set; }   // -Y
        public float[] SixSidedTint { get; set; } = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float SixSidedExposure { get; set; } = 1.0f;
        
        // === Panoramic/HDRI Skybox Properties ===
        public Guid? PanoramicTexture { get; set; }
        public float[] PanoramicTint { get; set; } = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
        public float PanoramicExposure { get; set; } = 1.0f;
        public float PanoramicRotation { get; set; } = 0.0f;
        public PanoramicMapping Mapping { get; set; } = PanoramicMapping.Latitude_Longitude_Layout;
        public bool MirrorOnBack { get; set; } = false;
        public PanoramicImageType ImageType { get; set; } = PanoramicImageType.Degrees360;
        
        // === Common Properties ===
        public bool UseForFog { get; set; } = false;
        
        public static SkyboxMaterialAsset Load(string file)
        {
            var content = File.ReadAllText(file);
            var asset = JsonSerializer.Deserialize<SkyboxMaterialAsset>(content)!;
            return asset;
        }
            
        public static void Save(string file, SkyboxMaterialAsset mat)
        {
            var content = JsonSerializer.Serialize(mat, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(file, content);
        }
    }
    
    public enum SkyboxType
    {
        Procedural,    // Unity's procedural skybox
        Cubemap,       // Cubemap texture
        SixSided,      // 6 separate textures
        Panoramic      // Single panoramic/HDRI texture
    }
    
    public enum PanoramicMapping
    {
        Latitude_Longitude_Layout,  // Standard spherical mapping
        Mirror_Ball,                // Mirror ball mapping
        Mirror_Ball_Front_Only      // Front-facing mirror ball
    }
    
    public enum PanoramicImageType
    {
        Degrees360,    // Full 360-degree image
        Degrees180     // 180-degree image
    }
}