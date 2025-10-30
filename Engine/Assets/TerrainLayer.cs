using System;
using Engine.Inspector;
using Engine.Serialization;

namespace Engine.Assets
{
    /// <summary>
    /// Unity-like terrain layer with automatic blending based on slope, height, and blend distance
    /// </summary>
    public sealed class TerrainLayer
    {
        [Engine.Serialization.SerializableAttribute("name")]
        [Editable]
        public string Name { get; set; } = "New Layer";

        // Material reference (replaces individual texture properties)
        [Engine.Serialization.SerializableAttribute("material")]
        [Editable]
        public Guid? Material { get; set; }

        // UV Transform (independent of material, specific to terrain layer)
        [Engine.Serialization.SerializableAttribute("tiling")]
        [Editable]
        public float[] Tiling { get; set; } = new float[] { 1f, 1f };

        [Engine.Serialization.SerializableAttribute("offset")]
        [Editable]
        public float[] Offset { get; set; } = new float[] { 0f, 0f };

        // DEPRECATED: Legacy texture properties (kept for backward compatibility, will be migrated to Material)
        [Engine.Serialization.SerializableAttribute("albedoTexture")]
        [Obsolete("Use Material property instead")]
        public Guid? AlbedoTexture { get; set; }

        [Engine.Serialization.SerializableAttribute("normalTexture")]
        [Obsolete("Use Material property instead")]
        public Guid? NormalTexture { get; set; }

        [Engine.Serialization.SerializableAttribute("metallic")]
        [Obsolete("Use Material property instead")]
        public float Metallic { get; set; } = 0f;

        [Engine.Serialization.SerializableAttribute("smoothness")]
        [Obsolete("Use Material property instead")]
        public float Smoothness { get; set; } = 0.5f;

        // Height-based blending (world units)
        [Engine.Serialization.SerializableAttribute("heightMin")]
        [Editable]
        public float HeightMin { get; set; } = -10000f;

        [Engine.Serialization.SerializableAttribute("heightMax")]
        [Editable]
        public float HeightMax { get; set; } = 10000f;

        [Engine.Serialization.SerializableAttribute("heightBlendDistance")]
        [Editable]
        public float HeightBlendDistance { get; set; } = 5f;

        // Slope-based blending (degrees [0..90])
        [Engine.Serialization.SerializableAttribute("slopeMinDeg")]
        [Editable]
        public float SlopeMinDeg { get; set; } = 0f;

        [Engine.Serialization.SerializableAttribute("slopeMaxDeg")]
        [Editable]
        public float SlopeMaxDeg { get; set; } = 90f;

        [Engine.Serialization.SerializableAttribute("slopeBlendDistance")]
        [Editable]
        public float SlopeBlendDistance { get; set; } = 5f;

        // Layer strength and priority
        [Engine.Serialization.SerializableAttribute("strength")]
        [Editable]
        public float Strength { get; set; } = 1f;

        [Engine.Serialization.SerializableAttribute("priority")]
        [Editable]
        public int Priority { get; set; } = 0;

        // Blend mode for this layer
        [Engine.Serialization.SerializableAttribute("blendMode")]
        [Editable]
        public TerrainLayerBlendMode BlendMode { get; set; } = TerrainLayerBlendMode.HeightAndSlope;

        // Underwater mode - applies texture at 1.0 strength below water level
        [Engine.Serialization.SerializableAttribute("isUnderwater")]
        [Editable]
        public bool IsUnderwater { get; set; } = false;

        [Engine.Serialization.SerializableAttribute("underwaterHeightMax")]
        [Editable]
        public float UnderwaterHeightMax { get; set; } = 0f;

        [Engine.Serialization.SerializableAttribute("underwaterBlendDistance")]
        [Editable]
        public float UnderwaterBlendDistance { get; set; } = 2f;

        [Engine.Serialization.SerializableAttribute("underwaterSlopeMin")]
        [Editable]
        public float UnderwaterSlopeMin { get; set; } = 0f;

        [Engine.Serialization.SerializableAttribute("underwaterSlopeMax")]
        [Editable]
        public float UnderwaterSlopeMax { get; set; } = 90f;

        [Engine.Serialization.SerializableAttribute("underwaterBlendWithOthers")]
        [Editable]
        public float UnderwaterBlendWithOthers { get; set; } = 0f; // 0 = full underwater, 1 = full blend with others

        /// <summary>
        /// Calculate the blend weight for this layer at a given world position
        /// </summary>
        public float CalculateBlendWeight(float worldHeight, float slopeDegrees, float noiseValue = 0f)
        {
            // Underwater mode - override normal blending
            if (IsUnderwater)
            {
                return CalculateUnderwaterWeight(worldHeight, slopeDegrees);
            }

            float weight = 1f;

            switch (BlendMode)
            {
                case TerrainLayerBlendMode.Height:
                    weight = CalculateHeightWeight(worldHeight);
                    break;
                case TerrainLayerBlendMode.Slope:
                    weight = CalculateSlopeWeight(slopeDegrees);
                    break;
                case TerrainLayerBlendMode.HeightAndSlope:
                    weight = CalculateHeightWeight(worldHeight) * CalculateSlopeWeight(slopeDegrees);
                    break;
                case TerrainLayerBlendMode.HeightOrSlope:
                    weight = Math.Max(CalculateHeightWeight(worldHeight), CalculateSlopeWeight(slopeDegrees));
                    break;
            }

            // Apply noise variation if provided
            weight *= (1f + noiseValue * 0.1f);

            // Apply layer strength
            weight *= Strength;

            return Math.Max(0f, Math.Min(1f, weight));
        }

        private float CalculateUnderwaterWeight(float worldHeight, float slopeDegrees)
        {
            // Full coverage below water level (with blend distance)
            float heightWeight = 0f;

            if (worldHeight <= UnderwaterHeightMax)
            {
                // Below water - full strength
                if (worldHeight <= UnderwaterHeightMax - UnderwaterBlendDistance)
                {
                    heightWeight = 1f;
                }
                // Blend zone at water surface
                else
                {
                    float t = (UnderwaterHeightMax - worldHeight) / UnderwaterBlendDistance;
                    heightWeight = SmoothStep(0f, 1f, t);
                }
            }

            // Apply slope constraint
            float slopeWeight = 1f;
            if (slopeDegrees < UnderwaterSlopeMin || slopeDegrees > UnderwaterSlopeMax)
            {
                slopeWeight = 0f;
            }
            else
            {
                // Blend at slope boundaries
                if (slopeDegrees < UnderwaterSlopeMin + 5f)
                {
                    float t = (slopeDegrees - UnderwaterSlopeMin) / 5f;
                    slopeWeight *= SmoothStep(0f, 1f, t);
                }
                if (slopeDegrees > UnderwaterSlopeMax - 5f)
                {
                    float t = (UnderwaterSlopeMax - slopeDegrees) / 5f;
                    slopeWeight *= SmoothStep(0f, 1f, t);
                }
            }

            return heightWeight * slopeWeight;
        }

        private float CalculateHeightWeight(float worldHeight)
        {
            if (worldHeight < HeightMin - HeightBlendDistance || worldHeight > HeightMax + HeightBlendDistance)
                return 0f;

            float weight = 1f;

            // Blend in from min height
            if (worldHeight < HeightMin + HeightBlendDistance)
            {
                float t = (worldHeight - (HeightMin - HeightBlendDistance)) / (2f * HeightBlendDistance);
                weight *= SmoothStep(0f, 1f, t);
            }

            // Blend out to max height
            if (worldHeight > HeightMax - HeightBlendDistance)
            {
                float t = (worldHeight - (HeightMax - HeightBlendDistance)) / (2f * HeightBlendDistance);
                weight *= SmoothStep(1f, 0f, t);
            }

            return weight;
        }

        private float CalculateSlopeWeight(float slopeDegrees)
        {
            if (slopeDegrees < SlopeMinDeg - SlopeBlendDistance || slopeDegrees > SlopeMaxDeg + SlopeBlendDistance)
                return 0f;

            float weight = 1f;

            // Blend in from min slope
            if (slopeDegrees < SlopeMinDeg + SlopeBlendDistance)
            {
                float t = (slopeDegrees - (SlopeMinDeg - SlopeBlendDistance)) / (2f * SlopeBlendDistance);
                weight *= SmoothStep(0f, 1f, t);
            }

            // Blend out to max slope
            if (slopeDegrees > SlopeMaxDeg - SlopeBlendDistance)
            {
                float t = (slopeDegrees - (SlopeMaxDeg - SlopeBlendDistance)) / (2f * SlopeBlendDistance);
                weight *= SmoothStep(1f, 0f, t);
            }

            return weight;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - edge0) / (edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }
    }

    public enum TerrainLayerBlendMode
    {
        Height,         // Blend based on height only
        Slope,          // Blend based on slope only
        HeightAndSlope, // Blend based on both height AND slope (multiplicative)
        HeightOrSlope   // Blend based on height OR slope (maximum)
    }
}
