using System;

namespace Engine.Assets
{
    /// <summary>
    /// Texture import settings similar to Unity
    /// </summary>
    [Serializable]
    public class TextureImportSettings
    {
        public TextureImportType TextureType { get; set; } = TextureImportType.Default;
        public TextureImportWrapMode WrapMode { get; set; } = TextureImportWrapMode.Repeat;
        public TextureImportFilterMode FilterMode { get; set; } = TextureImportFilterMode.Bilinear;
        public bool GenerateMipmaps { get; set; } = true;

        // Sprite settings
        public float AlphaCutoff { get; set; } = 0.5f;
        public bool UseAlphaCutoff { get; set; } = false;

        // Normal map settings
        public bool IsNormalMap { get; set; } = false;
        public bool FlipGreen { get; set; } = false;

        // Compression
        public TextureImportCompression Compression { get; set; } = TextureImportCompression.Automatic;

        // Max size
        public int MaxTextureSize { get; set; } = 2048;
    }

    public enum TextureImportType
    {
        Default,        // Standard texture (albedo, etc.)
        Sprite,         // 2D sprites with alpha support
        NormalMap,      // Normal maps
        HDR,            // High dynamic range
        Lightmap,       // Baked lighting
        Cursor          // UI cursor
    }

    public enum TextureImportWrapMode
    {
        Repeat,
        Clamp,
        Mirror,
        MirrorOnce
    }

    public enum TextureImportFilterMode
    {
        Point,          // Nearest neighbor (pixelated)
        Bilinear,       // Smooth
        Trilinear       // Smooth with mipmaps
    }

    public enum TextureImportCompression
    {
        None,
        Automatic,
        HighQuality,
        LowQuality
    }
}
