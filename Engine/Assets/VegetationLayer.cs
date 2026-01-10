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
        /// Vegetation density - number of placement ATTEMPTS per 100m² area.
        /// Note: Actual instance count may be lower due to placement constraints (height, slope, distance).
        /// Recommended values: 0.1-2.0 for large trees, 5-20 for grass/small plants.
        /// Example: Density=1.0 means ~1 instance per 100m², so a 100x100m tile = ~100 instances.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("density")]
        public float Density { get; set; } = 0.5f;

        /// <summary>
        /// Random seed for consistent placement across regenerations.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("seed")]
        public int Seed { get; set; } = 12345;

        // === INFINITE STREAMING MODE SETTINGS ===

        /// <summary>
        /// [STREAMING MODE] Probability that this vegetation layer spawns on a given tile (0-1).
        /// 0 = never spawns, 1 = always spawns. Use this to create sparse/patchy vegetation.
        /// Example: 0.3 = only 30% of tiles will have this vegetation type.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("tileSpawnChance")]
        public float TileSpawnChance { get; set; } = 1.0f;

        /// <summary>
        /// [STREAMING MODE] Random variation in density between tiles (0-1).
        /// 0 = all tiles have same density, 1 = density can vary from 0% to 200% of base density.
        /// Creates natural variation: some tiles dense, others sparse.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("densityVariation")]
        public float DensityVariation { get; set; } = 0.3f;

        /// <summary>
        /// [STREAMING MODE] Random variation in scale/size between tiles (0-1).
        /// 0 = all tiles have same size distribution, 1 = tiles can have very different average sizes.
        /// Example: 0.5 means one tile might have small trees, another large trees.
        /// </summary>
        [Engine.Serialization.SerializableAttribute("sizeVariation")]
        public float SizeVariation { get; set; } = 0.2f;

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
        
        // === GPU GRASS LAYER (optional) ===
        // When enabled on a vegetation layer, this layer will be treated as a GPU-generated
        // grass coverage layer. Uses geometry shader to generate dense grass from terrain mesh.
        [Engine.Serialization.SerializableAttribute("isGrassLayer")]
        public bool IsGrassLayer { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("grassProperties")]
        public GrassProperties? GrassProperties { get; set; } = null;
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

    /// <summary>
    /// GPU-generated grass properties for vegetation layers.
    /// Uses geometry shader to generate dense grass coverage directly from terrain vertices.
    /// </summary>
    [System.Serializable]
    public sealed class GrassProperties
    {
        // === DENSITY & COVERAGE ===
        [Engine.Serialization.SerializableAttribute("density")]
        public float Density { get; set; } = 1.5f; // Multiplier for blade count (0.5-3)

        [Engine.Serialization.SerializableAttribute("coverageNoiseScale")]
        public float CoverageNoiseScale { get; set; } = 0.05f; // Noise scale for patchy grass (world-space)

        [Engine.Serialization.SerializableAttribute("coverageThreshold")]
        public float CoverageThreshold { get; set; } = 0.2f; // 0-1, lower = fuller coverage, higher = sparser patches

        // === SLOPE & HEIGHT CONSTRAINTS ===
        [Engine.Serialization.SerializableAttribute("minSlope")]
        public float MinSlope { get; set; } = 0.0f; // Minimum slope angle in degrees (0 = flat)

        [Engine.Serialization.SerializableAttribute("maxSlope")]
        public float MaxSlope { get; set; } = 45.0f; // Maximum slope angle in degrees (90 = vertical)

        [Engine.Serialization.SerializableAttribute("minHeight")]
        public float MinHeight { get; set; } = -1000.0f; // Minimum world height for grass

        [Engine.Serialization.SerializableAttribute("maxHeight")]
        public float MaxHeight { get; set; } = 1000.0f; // Maximum world height for grass

        // === GRASS BLADE GEOMETRY ===
        [Engine.Serialization.SerializableAttribute("bladeHeight")]
        public float BladeHeight { get; set; } = 0.4f; // Height of grass blades in world units

        [Engine.Serialization.SerializableAttribute("bladeHeightVariation")]
        public float BladeHeightVariation { get; set; } = 0.4f; // Random height variation (0-1)

        [Engine.Serialization.SerializableAttribute("bladeWidth")]
        public float BladeWidth { get; set; } = 0.05f; // Width of grass blades

        [Engine.Serialization.SerializableAttribute("bladeCurvature")]
        public float BladeCurvature { get; set; } = 0.4f; // Bend amount (0-1)

        [Engine.Serialization.SerializableAttribute("bladesPerVertex")]
        public int BladesPerVertex { get; set; } = 6; // Base number of grass blades per triangle (1-10)

        // === APPEARANCE ===
        [Engine.Serialization.SerializableAttribute("colorTop")]
        public float[] ColorTop { get; set; } = new float[] { 0.45f, 0.75f, 0.25f, 1.0f }; // RGBA - brighter green

        [Engine.Serialization.SerializableAttribute("colorBottom")]
        public float[] ColorBottom { get; set; } = new float[] { 0.2f, 0.4f, 0.15f, 1.0f }; // RGBA

        [Engine.Serialization.SerializableAttribute("colorVariation")]
        public float ColorVariation { get; set; } = 0.15f; // Color randomness (0-1)

        [Engine.Serialization.SerializableAttribute("albedoTexture")]
        public Guid? AlbedoTexture { get; set; } = null; // Optional grass blade texture

        // === DENSITY MAP (for painted grass coverage) ===
        [Engine.Serialization.SerializableAttribute("densityMap")]
        public Guid? DensityMap { get; set; } = null; // Optional R8 texture for painting grass coverage

        [Engine.Serialization.SerializableAttribute("densityMapScale")]
        public float DensityMapScale { get; set; } = 0.01f; // World-space UV scale for density map

        // === WIND ANIMATION ===
        [Engine.Serialization.SerializableAttribute("windStrength")]
        public float WindStrength { get; set; } = 0.5f; // How much wind affects grass

        [Engine.Serialization.SerializableAttribute("windSpeed")]
        public float WindSpeed { get; set; } = 1.5f; // Wind animation speed

        [Engine.Serialization.SerializableAttribute("windTurbulence")]
        public float WindTurbulence { get; set; } = 0.6f; // Wind noise/variation

        // === LOD & CULLING ===
        [Engine.Serialization.SerializableAttribute("maxRenderDistance")]
        public float MaxRenderDistance { get; set; } = 150f; // Distance fade-out

        [Engine.Serialization.SerializableAttribute("fadeRange")]
        public float FadeRange { get; set; } = 30f; // Distance over which grass fades
    }
}
