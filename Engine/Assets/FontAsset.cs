using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Engine.Assets
{
    /// <summary>
    /// Font asset that stores font file path and metadata for UI rendering.
    /// Supports regular, bold, italic, and bold-italic variants.
    /// </summary>
    public class FontAsset
    {
        [JsonPropertyName("guid")]
        public Guid Guid { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "New Font";

        [JsonPropertyName("regularPath")]
        public string? RegularPath { get; set; }

        [JsonPropertyName("boldPath")]
        public string? BoldPath { get; set; }

        [JsonPropertyName("italicPath")]
        public string? ItalicPath { get; set; }

        [JsonPropertyName("boldItalicPath")]
        public string? BoldItalicPath { get; set; }

        [JsonPropertyName("defaultSize")]
        public float DefaultSize { get; set; } = 14f;

        [JsonPropertyName("lineHeight")]
        public float LineHeight { get; set; } = 1.2f;

        /// <summary>
        /// Get the appropriate font path based on style flags
        /// </summary>
        public string? GetFontPath(bool bold, bool italic)
        {
            // Try to find exact match first
            if (bold && italic && !string.IsNullOrEmpty(BoldItalicPath))
                return BoldItalicPath;
            if (bold && !string.IsNullOrEmpty(BoldPath))
                return BoldPath;
            if (italic && !string.IsNullOrEmpty(ItalicPath))
                return ItalicPath;

            // Fallback to regular
            return RegularPath;
        }

        /// <summary>
        /// Load the font asset from a .fontasset file
        /// </summary>
        public static FontAsset? Load(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<FontAsset>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save the font asset to a .fontasset file
        /// </summary>
        public void Save(string path)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(this, options);
            File.WriteAllText(path, json);
        }
    }
}
