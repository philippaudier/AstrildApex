using System;
using OpenTK.Mathematics;

namespace Engine.Components
{
    public enum LightType
    {
        Directional,
        Point,
        Spot
    }

    /// <summary>
    /// Unity-like Light component
    /// </summary>
    public class LightComponent : Component
    {
        [Engine.Serialization.Serializable("type")]
        public LightType Type { get; set; } = LightType.Directional;

        [Engine.Serialization.Serializable("color")]
        public Vector3 Color { get; set; } = Vector3.One; // RGB color

        [Engine.Serialization.Serializable("intensity")]
        public float Intensity { get; set; } = 1.0f;

        [Engine.Serialization.Serializable("range")]
        public float Range { get; set; } = 10.0f; // For Point and Spot lights

        [Engine.Serialization.Serializable("spotAngle")]
        public float SpotAngle { get; set; } = 30.0f; // For Spot lights outer cone (degrees)

        [Engine.Serialization.Serializable("spotInnerAngle")]
        public float SpotInnerAngle { get; set; } = 20.0f; // For Spot lights inner cone (degrees)

        [Engine.Serialization.Serializable("castShadows")]
        public bool CastShadows { get; set; } = true;
        
        // For directional light, direction is derived from Entity world transform
        // Left-handed: +Z is forward
        public Vector3 Direction
        {
            get
            {
                if (Entity?.Transform == null) return Vector3.UnitZ;

                // Use world rotation to properly handle entity hierarchy
                Entity.GetWorldTRS(out _, out var worldRotation, out _);
                var direction = Vector3.Transform(Vector3.UnitZ, worldRotation);

                // Debug: log direction calculation

                return direction;
            }
        }
    }
}