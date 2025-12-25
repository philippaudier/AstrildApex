using System.Numerics;

namespace Editor.Themes
{
    /// <summary>
    /// Unified UI Theme for AstrildApex Editor
    /// Centralizes ALL colors, spacing, sizing for consistent and easily tweakable UI
    /// ONE place to control the entire look and feel of the engine
    /// </summary>
    public class UITheme
    {
        #region Colors - Status & Semantic
        
        /// <summary>Primary accent color for actions, selections, highlights</summary>
        public Vector4 Primary { get; set; } = new(0.4f, 0.6f, 0.9f, 1f);
        public Vector4 PrimaryHovered { get; set; } = new(0.5f, 0.7f, 1.0f, 1f);
        public Vector4 PrimaryActive { get; set; } = new(0.3f, 0.5f, 0.8f, 1f);
        
        /// <summary>Success color for positive states, valid items</summary>
        public Vector4 Success { get; set; } = new(0.4f, 1f, 0.4f, 1f);
        public Vector4 SuccessHovered { get; set; } = new(0.5f, 1f, 0.5f, 1f);
        public Vector4 SuccessActive { get; set; } = new(0.3f, 0.9f, 0.3f, 1f);
        
        /// <summary>Warning color for caution states</summary>
        public Vector4 Warning { get; set; } = new(1f, 0.8f, 0.2f, 1f);
        public Vector4 WarningHovered { get; set; } = new(1f, 0.9f, 0.3f, 1f);
        public Vector4 WarningActive { get; set; } = new(0.9f, 0.7f, 0.1f, 1f);
        
        /// <summary>Error color for error states, invalid items</summary>
        public Vector4 Error { get; set; } = new(1f, 0.4f, 0.4f, 1f);
        public Vector4 ErrorHovered { get; set; } = new(1f, 0.5f, 0.5f, 1f);
        public Vector4 ErrorActive { get; set; } = new(0.9f, 0.3f, 0.3f, 1f);
        
        /// <summary>Info color for informational messages</summary>
        public Vector4 Info { get; set; } = new(0.4f, 0.8f, 1f, 1f);
        public Vector4 InfoHovered { get; set; } = new(0.5f, 0.9f, 1f, 1f);
        public Vector4 InfoActive { get; set; } = new(0.3f, 0.7f, 0.9f, 1f);
        
        #endregion
        
        #region Colors - Text
        
        /// <summary>Normal text color</summary>
        public Vector4 TextNormal { get; set; } = new(0.9f, 0.95f, 1.0f, 1f);
        
        /// <summary>Disabled/grayed out text</summary>
        public Vector4 TextDisabled { get; set; } = new(0.5f, 0.5f, 0.6f, 1f);
        
        /// <summary>Accent text color (labels, captions)</summary>
        public Vector4 TextAccent { get; set; } = new(0.7f, 0.9f, 1.0f, 1f);
        
        /// <summary>Label text in inspectors</summary>
        public Vector4 TextLabel { get; set; } = new(0.8f, 0.8f, 0.85f, 1f);
        
        /// <summary>Value text in inspectors</summary>
        public Vector4 TextValue { get; set; } = new(1.0f, 1.0f, 1.0f, 1f);
        
        /// <summary>Modified/edited value text</summary>
        public Vector4 TextModified { get; set; } = new(0.8f, 1.0f, 0.8f, 1f);
        
        #endregion
        
        #region Colors - UI Elements
        
        /// <summary>Section headers color</summary>
        public Vector4 Section { get; set; } = new(0.25f, 0.45f, 0.75f, 1f);
        public Vector4 SectionHovered { get; set; } = new(0.35f, 0.55f, 0.85f, 1f);
        public Vector4 SectionActive { get; set; } = new(0.45f, 0.65f, 0.95f, 1f);
        
        /// <summary>Section background (collapsed header background)</summary>
        public Vector4 SectionBackground { get; set; } = new(0.15f, 0.15f, 0.2f, 0.5f);
        
        /// <summary>Separator lines</summary>
        public Vector4 Separator { get; set; } = new(0.4f, 0.4f, 0.5f, 0.6f);
        
        /// <summary>General background</summary>
        public Vector4 Background { get; set; } = new(0.2f, 0.2f, 0.25f, 1f);
        public Vector4 BackgroundHovered { get; set; } = new(0.25f, 0.25f, 0.3f, 1f);
        public Vector4 BackgroundActive { get; set; } = new(0.3f, 0.3f, 0.35f, 1f);
        
        /// <summary>Drop zone colors (drag & drop targets)</summary>
        public Vector4 DropZone { get; set; } = new(0.2f, 0.6f, 1.0f, 0.3f);
        public Vector4 DropZoneActive { get; set; } = new(0.3f, 0.8f, 1.0f, 0.5f);
        
        /// <summary>Border colors</summary>
        public Vector4 Border { get; set; } = new(0.4f, 0.4f, 0.5f, 0.5f);
        public Vector4 BorderActive { get; set; } = new(0.6f, 0.7f, 1.0f, 0.8f);
        
        #endregion
        
        #region Colors - Toolbar & Modern UI (Glassmorphism)
        
        /// <summary>Toolbar background (glassmorphism effect)</summary>
        public Vector4 ToolbarBg { get; set; } = new(0f, 0f, 0f, 0.7f);
        
        /// <summary>Toolbar border</summary>
        public Vector4 ToolbarBorder { get; set; } = new(1f, 1f, 1f, 0.3f);
        
        /// <summary>Toolbar button background</summary>
        public Vector4 ToolbarButton { get; set; } = new(1f, 1f, 1f, 0.05f);
        public Vector4 ToolbarButtonHovered { get; set; } = new(1f, 1f, 1f, 0.15f);
        public Vector4 ToolbarButtonActive { get; set; } = new(1f, 1f, 1f, 0.25f);
        
        /// <summary>Toolbar button border</summary>
        public Vector4 ToolbarButtonBorder { get; set; } = new(1f, 1f, 1f, 0.1f);
        public Vector4 ToolbarButtonBorderActive { get; set; } = new(1f, 1f, 1f, 0.3f);
        
        #endregion
        
        #region Colors - Gradients
        
        /// <summary>Gradient colors for active buttons, headers</summary>
        public Vector4 GradientStart { get; set; } = new(0.4f, 0.5f, 0.92f, 1f); // #667eea
        public Vector4 GradientEnd { get; set; } = new(0.46f, 0.3f, 0.64f, 1f);  // #764ba2
        
        #endregion
        
        #region Spacing & Sizing
        
        // === Spacing ===
        public float SpacingTiny { get; set; } = 2f;
        public float SpacingSmall { get; set; } = 4f;
        public float SpacingMedium { get; set; } = 8f;
        public float SpacingLarge { get; set; } = 16f;
        public float SpacingHuge { get; set; } = 24f;
        
        // === Padding ===
        public float PaddingSmall { get; set; } = 4f;
        public float PaddingMedium { get; set; } = 8f;
        public float PaddingLarge { get; set; } = 12f;
        
        // === Indent ===
        public float IndentWidth { get; set; } = 16f;
        
        // === Control Widths ===
        public float ControlWidthSmall { get; set; } = 60f;
        public float ControlWidthMedium { get; set; } = 120f;
        public float ControlWidthLarge { get; set; } = 180f;
        public float ControlWidthFull { get; set; } = -1f; // Full available width
        public float ControlWidthAuto { get; set; } = -80f; // Offset from right edge
        
        // === Heights ===
        public float LineHeight { get; set; } = 20f;
        public float ButtonHeightSmall { get; set; } = 20f;
        public float ButtonHeightMedium { get; set; } = 25f;
        public float ButtonHeightLarge { get; set; } = 30f;
        public float SectionHeaderHeight { get; set; } = 24f;
        public float ToolbarHeight { get; set; } = 36f;
        
        // === Drag & Drop ===
        public float DragDropMinHeight { get; set; } = 40f;
        public float DragDropLargeHeight { get; set; } = 60f;
        
        // === Icons ===
        public float IconSizeSmall { get; set; } = 12f;
        public float IconSizeMedium { get; set; } = 16f;
        public float IconSizeLarge { get; set; } = 24f;
        
        // === Toolbar Specific ===
        public float ToolbarButtonSize { get; set; } = 36f;
        public float ToolbarIconButtonSize { get; set; } = 28f;
        public float ToolbarPlayButtonSize { get; set; } = 40f;
        
        #endregion
        
        #region Rounding
        
        public float RoundingSmall { get; set; } = 4f;
        public float RoundingMedium { get; set; } = 8f;
        public float RoundingLarge { get; set; } = 12f;
        public float RoundingButton { get; set; } = 8f;
        public float RoundingToolbar { get; set; } = 12f;
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Get a gradient color interpolation between GradientStart and GradientEnd
        /// </summary>
        public Vector4 GetGradientColor(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new Vector4(
                GradientStart.X + (GradientEnd.X - GradientStart.X) * t,
                GradientStart.Y + (GradientEnd.Y - GradientStart.Y) * t,
                GradientStart.Z + (GradientEnd.Z - GradientStart.Z) * t,
                GradientStart.W + (GradientEnd.W - GradientStart.W) * t
            );
        }
        
        /// <summary>
        /// Get a color with adjusted alpha
        /// </summary>
        public Vector4 WithAlpha(Vector4 color, float alpha)
        {
            return new Vector4(color.X, color.Y, color.Z, alpha);
        }
        
        /// <summary>
        /// Get a brightened version of a color
        /// </summary>
        public Vector4 Brighten(Vector4 color, float amount = 0.1f)
        {
            return new Vector4(
                Math.Min(1f, color.X + amount),
                Math.Min(1f, color.Y + amount),
                Math.Min(1f, color.Z + amount),
                color.W
            );
        }
        
        /// <summary>
        /// Get a darkened version of a color
        /// </summary>
        public Vector4 Darken(Vector4 color, float amount = 0.1f)
        {
            return new Vector4(
                Math.Max(0f, color.X - amount),
                Math.Max(0f, color.Y - amount),
                Math.Max(0f, color.Z - amount),
                color.W
            );
        }
        
        #endregion
    }
}
