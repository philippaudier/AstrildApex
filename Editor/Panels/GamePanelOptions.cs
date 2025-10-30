using System;

namespace Editor.Panels
{
    /// <summary>
    /// Game Panel display and behavior options (Unity-style)
    /// </summary>
    public class GamePanelOptions
    {
        /// <summary>
        /// Automatically focus the Game panel when entering Play Mode
        /// </summary>
        public bool FocusOnPlay { get; set; } = true;

        /// <summary>
        /// Maximize the Game panel when entering Play Mode
        /// </summary>
        public bool MaximizeOnPlay { get; set; } = false;

        /// <summary>
        /// Mute audio in the Game panel
        /// </summary>
        public bool MuteAudio { get; set; } = false;

        /// <summary>
        /// Show performance stats overlay in the Game panel
        /// </summary>
        public bool ShowStats { get; set; } = true;

        /// <summary>
        /// Show gizmos in the Game panel (normally only in Scene view)
        /// </summary>
        public bool ShowGizmos { get; set; } = false;

        /// <summary>
        /// Aspect ratio mode for the Game panel
        /// </summary>
        public AspectRatioMode AspectMode { get; set; } = AspectRatioMode.Free;

        /// <summary>
        /// Custom aspect ratio when AspectMode is set to Custom
        /// </summary>
        public float CustomAspectRatio { get; set; } = 16f / 9f;

        /// <summary>
        /// Target resolution scale (1.0 = native, 0.5 = half res, 2.0 = supersampling)
        /// </summary>
        public float ResolutionScale { get; set; } = 1.0f;

        /// <summary>
        /// VSync enabled for Game panel rendering
        /// </summary>
        public bool VSync { get; set; } = true;

        /// <summary>
        /// Target frame rate limit (0 = unlimited)
        /// </summary>
        public int TargetFrameRate { get; set; } = 0;
    }

    public enum AspectRatioMode
    {
        Free,           // No aspect ratio constraint
        Aspect16_9,     // 16:9 aspect ratio
        Aspect16_10,    // 16:10 aspect ratio
        Aspect4_3,      // 4:3 aspect ratio
        Aspect5_4,      // 5:4 aspect ratio
        Aspect1_1,      // 1:1 square
        Custom          // User-defined aspect ratio
    }
}
