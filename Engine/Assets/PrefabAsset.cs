using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Assets
{
    /// <summary>
    /// Prefab Asset - stores a serialized entity hierarchy that can be instantiated in scenes
    /// Similar to Unity prefabs: drag entity from hierarchy to asset panel to create prefab
    /// </summary>
    public sealed class PrefabAsset
    {
        public Guid Guid { get; set; }
        public string? Name { get; set; }
        
        /// <summary>
        /// Root entity data (including all components and children)
        /// </summary>
        public PrefabEntityData? RootEntity { get; set; }
        
        /// <summary>
        /// Metadata: creation date, last modified, etc.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Preview icon GUID (optional, for asset browser)
        /// </summary>
        public Guid? PreviewIconGuid { get; set; }
        
        /// <summary>
        /// Load prefab from JSON file
        /// </summary>
        public static PrefabAsset Load(string file)
        {
            var prefab = JsonSerializer.Deserialize<PrefabAsset>(System.IO.File.ReadAllText(file))!;
            return prefab;
        }
        
        /// <summary>
        /// Save prefab to JSON file
        /// </summary>
        public static void Save(string file, PrefabAsset prefab)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            System.IO.File.WriteAllText(file, JsonSerializer.Serialize(prefab, options));
        }
        
        /// <summary>
        /// Save prefab atomically (write to temp file then replace)
        /// </summary>
        public static void SaveAtomic(string file, PrefabAsset prefab)
        {
            var dir = System.IO.Path.GetDirectoryName(file) ?? System.IO.Path.GetTempPath();
            var tmp = System.IO.Path.Combine(dir, System.IO.Path.GetFileName(file) + ".tmp");
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            System.IO.File.WriteAllText(tmp, JsonSerializer.Serialize(prefab, options));
            System.IO.File.Copy(tmp, file, overwrite: true);
            
            try { System.IO.File.Delete(tmp); } catch { }
        }
    }

    /// <summary>
    /// Serializable entity data for prefabs
    /// Stores all entity properties, components, and children recursively
    /// </summary>
    public sealed class PrefabEntityData
    {
        [JsonPropertyName("guid")]
        public Guid Guid { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Entity";
        
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Local transform (position, rotation, scale relative to parent)
        /// </summary>
        [JsonPropertyName("localPosition")]
        public float[] LocalPosition { get; set; } = new float[] { 0, 0, 0 };
        
        [JsonPropertyName("localRotation")]
        public float[] LocalRotation { get; set; } = new float[] { 0, 0, 0 }; // Euler angles
        
        [JsonPropertyName("localScale")]
        public float[] LocalScale { get; set; } = new float[] { 1, 1, 1 };
        
        /// <summary>
        /// Components serialized as JSON (type name -> JSON data)
        /// </summary>
        [JsonPropertyName("components")]
        public Dictionary<string, JsonElement> Components { get; set; } = new();
        
        /// <summary>
        /// Child entities (recursive hierarchy)
        /// </summary>
        [JsonPropertyName("children")]
        public List<PrefabEntityData> Children { get; set; } = new();
    }
}
