using System;
using System.Collections.Generic;

namespace Engine.Components
{
    /// <summary>
    /// Placeholder component for missing/removed component types.
    /// Prevents scene corruption when loading scenes with deleted component types.
    /// Similar to Unity's missing script placeholder.
    /// </summary>
    public class MissingComponent : Component
    {
        /// <summary>
        /// The original type name of the missing component
        /// </summary>
        public string MissingTypeName { get; set; } = "Unknown";

        /// <summary>
        /// The original serialized data (preserved in case the component is restored)
        /// </summary>
        public Dictionary<string, object>? SerializedData { get; set; }

        /// <summary>
        /// When was this component marked as missing
        /// </summary>
        public DateTime MarkedMissingAt { get; set; } = DateTime.Now;

        public MissingComponent()
        {
        }

        public MissingComponent(string typeName, Dictionary<string, object>? data = null)
        {
            MissingTypeName = typeName;
            SerializedData = data;
            MarkedMissingAt = DateTime.Now;
        }

        public override string ToString()
        {
            return $"Missing Component: {MissingTypeName}";
        }
    }
}
