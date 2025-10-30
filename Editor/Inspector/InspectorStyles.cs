using System.Numerics;

namespace Editor.Inspector
{
    /// <summary>
    /// Unity-style color palette for inspector UI
    /// </summary>
    public static class InspectorColors
    {
        // Text colors
        public static Vector4 Label { get; set; } = new Vector4(0.8f, 0.8f, 0.8f, 1f);
        public static Vector4 LabelDisabled { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 1f);
        public static Vector4 Value { get; set; } = new Vector4(1.0f, 1.0f, 1.0f, 1f);
        public static Vector4 ValueModified { get; set; } = new Vector4(0.8f, 1.0f, 0.8f, 1f);
        
        // Status colors
        public static Vector4 Warning { get; set; } = new Vector4(1.0f, 0.8f, 0.2f, 1f);
        public static Vector4 Error { get; set; } = new Vector4(1.0f, 0.3f, 0.3f, 1f);
        public static Vector4 Success { get; set; } = new Vector4(0.3f, 1.0f, 0.3f, 1f);
        public static Vector4 Info { get; set; } = new Vector4(0.4f, 0.7f, 1.0f, 1f);
        
        // UI elements
        public static Vector4 Section { get; set; } = new Vector4(0.3f, 0.5f, 0.8f, 1f);
        public static Vector4 SectionBackground { get; set; } = new Vector4(0.15f, 0.15f, 0.17f, 1f);
        public static Vector4 DropZone { get; set; } = new Vector4(0.2f, 0.6f, 1.0f, 0.3f);
        public static Vector4 DropZoneActive { get; set; } = new Vector4(0.2f, 0.8f, 1.0f, 0.5f);
        public static Vector4 Separator { get; set; } = new Vector4(0.3f, 0.3f, 0.3f, 1f);
        
        // Buttons
        public static Vector4 Button { get; set; } = new Vector4(0.25f, 0.25f, 0.27f, 1f);
        public static Vector4 ButtonHovered { get; set; } = new Vector4(0.35f, 0.35f, 0.37f, 1f);
        public static Vector4 ButtonActive { get; set; } = new Vector4(0.45f, 0.45f, 0.47f, 1f);
    }
    
    /// <summary>
    /// Unity-style layout constants for inspector UI
    /// </summary>
    public static class InspectorLayout
    {
        // Spacing
        public const float Padding = 8f;
        public const float ItemSpacing = 4f;
        public const float SectionSpacing = 12f;
        public const float IndentWidth = 16f;
        
        // Widths (based on 300px panel width)
        public const float LabelWidth = 120f;     // 40%
        public const float ControlWidth = 180f;   // 60%
        public const float FullWidth = 300f;
        
        // Heights
        public const float LineHeight = 20f;
        public const float SectionHeaderHeight = 24f;
        public const float ButtonHeight = 20f;
        
        // Icons
        public const float IconSize = 16f;
        public const float SmallIconSize = 12f;
    }
}
