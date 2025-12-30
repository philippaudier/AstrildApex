using System;

namespace Engine.Assets
{
    /// <summary>
    /// Defines a vegetation layer with spawning rules for procedural placement on terrain.
    /// Inspired by Unity's Terrain Detail system and Unreal's Foliage system.
    /// </summary>
    [Serializable]
    public sealed class VegetationLayer
    {
        // === IDENTIFICATION ===
        
        [Engine.Serialization.SerializableAttribute("name")]
        public string Name { get; set; } = "New Vegetation Layer";

        [Engine.Serialization.SerializableAttribute("enabled")]
        public bool Enabled { get; set; } = true;

        // === MODEL/PREFAB REFERENCE ===
        
        /// <summary>
        /// GUID of the imported model asset (e.g., .gltf, .fbx) to use for this vegetation type.
        /// The model's materials and submeshes will be used automatically.
        /// DEPRECATED: Use PrefabGuid instead for better workflow.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("modelGuid")]
        public Guid? ModelGuid { get; set; } = null;
        
        /// <summary>
        /// GUID of the prefab asset to use for this vegetation type.
        /// When set, this takes priority over ModelGuid.
        /// Prefabs allow full entity hierarchies with components, transforms, and children.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("prefabGuid")]
        public Guid? PrefabGuid { get; set; } = null;

        /// <summary>
        /// Optional: specify which submesh to use from the model (0 = all submeshes).
        /// Set to -1 to render all submeshes of the model.
        /// Only used when ModelGuid is set (not used with prefabs).
        /// </summary>
        [Engine.Serialization.SerializableAttribute("submeshIndex")]
        public int SubmeshIndex { get; set; } = -1;

        // === DENSITY & DISTRIBUTION ===
        
        /// <summary>
        /// Number of instances per 100x100 terrain area (density).
        /// Higher values = more instances spawned.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("density")]
        public float Density { get; set; } = 10f;

        /// <summary>
        /// Random seed for consistent placement across regenerations.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("seed")]
        public int Seed { get; set; } = 12345;

        // === PLACEMENT RULES ===
        
        /// <summary>
        /// Minimum terrain height (normalized 0-1) for placement.
        /// 0 = lowest point, 1 = highest point.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("minHeight")]
        public float MinHeight { get; set; } = 0f;

        /// <summary>
        /// Maximum terrain height (normalized 0-1) for placement.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("maxHeight")]
        public float MaxHeight { get; set; } = 1f;

        /// <summary>
        /// Minimum slope angle (degrees) for placement.
        /// 0 = flat ground, 90 = vertical cliff.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("minSlope")]
        public float MinSlope { get; set; } = 0f;

        /// <summary>
        /// Maximum slope angle (degrees) for placement.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("maxSlope")]
        public float MaxSlope { get; set; } = 30f;

        /// <summary>
        /// Minimum allowed distance between instances (world units). When > 0, new
        /// placements closer than this distance to an existing instance are rejected.
        /// Use this to prevent trees from spawning on top of each other.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("minDistance")]
        public float MinDistance { get; set; } = 2.0f;

        // === SCALE & VARIATION ===
        
        /// <summary>
        /// Minimum uniform scale for instances.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("minScale")]
        public float MinScale { get; set; } = 0.8f;

        /// <summary>
        /// Maximum uniform scale for instances.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("maxScale")]
        public float MaxScale { get; set; } = 1.2f;

        /// <summary>
        /// Enable random Y-axis rotation for natural variation.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("randomRotation")]
        public bool RandomRotation { get; set; } = true;

        /// <summary>
        /// Align instances to terrain normal (up vector).
        /// </summary>
        [Engine.Serialization.SerializableAttribute("alignToNormal")]
        public bool AlignToNormal { get; set; } = false;

        /// <summary>
        /// Percentage of alignment to terrain normal (0-100%).
        /// 0 = no alignment (vertical), 100 = full alignment to surface normal.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("alignmentStrength")]
        public float AlignmentStrength { get; set; } = 100f;

        // === CULLING & OPTIMIZATION ===

        /// <summary>
        /// Maximum render distance from camera. Instances beyond this distance are culled.
        /// Set to 0 for infinite distance (not recommended for dense vegetation).
        /// </summary>
        [Engine.Serialization.SerializableAttribute("maxRenderDistance")]
        public float MaxRenderDistance { get; set; } = 500f;

        /// <summary>
        /// Bounding sphere radius for frustum culling (in local space units).
        /// Should roughly match the largest dimension of the model.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("cullingSphereRadius")]
        public float CullingSphereRadius { get; set; } = 5f;

        // === WIND & ANIMATION ===

        // Wind and LOD-related properties removed: global weather/wind system and
        // shader-level behaviour now control animation and any distance-based logic.

        // === HELPER METHODS ===

        /// <summary>
        /// Check if a point passes the height filter.
        /// </summary>
        public bool PassesHeightFilter(float normalizedHeight)
        {
            return normalizedHeight >= MinHeight && normalizedHeight <= MaxHeight;
        }

        /// <summary>
        /// Check if a slope angle passes the slope filter.
        /// </summary>
        public bool PassesSlopeFilter(float slopeAngleDegrees)
        {
            return slopeAngleDegrees >= MinSlope && slopeAngleDegrees <= MaxSlope;
        }

        /// <summary>
        /// Clone this layer with a new seed for variation.
        /// </summary>
        public VegetationLayer Clone()
        {
            return new VegetationLayer
            {
                Name = Name,
                Enabled = Enabled,
                ModelGuid = ModelGuid,
                PrefabGuid = PrefabGuid,
                SubmeshIndex = SubmeshIndex,
                Density = Density,
                Seed = Seed,
                MinHeight = MinHeight,
                MaxHeight = MaxHeight,
                MinSlope = MinSlope,
                MaxSlope = MaxSlope,
                MinDistance = MinDistance,
                MinScale = MinScale,
                MaxScale = MaxScale,
                RandomRotation = RandomRotation,
                AlignToNormal = AlignToNormal,
                AlignmentStrength = AlignmentStrength,
                MaxRenderDistance = MaxRenderDistance,
                CullingSphereRadius = CullingSphereRadius
            };
        }
    }
}
