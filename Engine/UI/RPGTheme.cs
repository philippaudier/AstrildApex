using System.Numerics;

namespace Engine.UI
{
    /// <summary>
    /// RPG Theme - Dark Fantasy Color Palette
    /// Ported from UIBuilder/src/styles/rpg-theme.css
    /// </summary>
    public static class RPGTheme
    {
        // Dark Fantasy Color Palette
        public static readonly Vector4 BgDark = new Vector4(0.04f, 0.04f, 0.06f, 1f);           // #0a0a0f
        public static readonly Vector4 BgDarkLighter = new Vector4(0.10f, 0.10f, 0.18f, 1f);    // #1a1a2e
        public static readonly Vector4 BgPanel = new Vector4(0.09f, 0.13f, 0.24f, 1f);          // #16213e
        public static readonly Vector4 BgPanelHover = new Vector4(0.12f, 0.18f, 0.31f, 1f);     // #1f2d50

        public static readonly Vector4 AccentPrimary = new Vector4(0.91f, 0.27f, 0.38f, 1f);    // #e94560
        public static readonly Vector4 AccentSecondary = new Vector4(0.95f, 0.61f, 0.07f, 1f);  // #f39c12
        public static readonly Vector4 AccentTertiary = new Vector4(0.56f, 0.27f, 0.68f, 1f);   // #8e44ad

        public static readonly Vector4 TextPrimary = new Vector4(0.91f, 0.91f, 0.91f, 1f);      // #e8e8e8
        public static readonly Vector4 TextSecondary = new Vector4(0.63f, 0.63f, 0.63f, 1f);    // #a0a0a0
        public static readonly Vector4 TextDisabled = new Vector4(0.38f, 0.38f, 0.38f, 1f);     // #606060

        public static readonly Vector4 BorderColor = new Vector4(0.17f, 0.24f, 0.31f, 1f);      // #2c3e50
        public static readonly Vector4 BorderAccent = new Vector4(0.91f, 0.27f, 0.38f, 1f);     // #e94560

        // Stats Colors
        public static readonly Vector4 HealthColor = new Vector4(0.91f, 0.30f, 0.24f, 1f);      // #e74c3c
        public static readonly Vector4 ManaColor = new Vector4(0.20f, 0.60f, 0.86f, 1f);        // #3498db
        public static readonly Vector4 XpColor = new Vector4(0.95f, 0.61f, 0.07f, 1f);          // #f39c12
        public static readonly Vector4 StaminaColor = new Vector4(0.18f, 0.80f, 0.44f, 1f);     // #2ecc71

        // Overlay with transparency
        public static readonly Vector4 OverlayBg = new Vector4(0.04f, 0.04f, 0.06f, 0.85f);     // rgba(10, 10, 15, 0.85)

        // UI Constants
        public const float BorderRadius = 16f;
        public const float ButtonPadding = 15f;
        public const float PanelPadding = 30f;
        public const float TabHeight = 50f;
        public const float HeaderHeight = 70f;
    }
}
