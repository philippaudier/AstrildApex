namespace Engine.Rendering
{
    /// <summary>
    /// Anti-aliasing modes available in the engine
    /// </summary>
    public enum AntiAliasingMode
    {
        None = 0,      // No anti-aliasing
        MSAA2x = 2,    // MultiSample AA 2x samples
        MSAA4x = 4,    // MultiSample AA 4x samples
        MSAA8x = 8,    // MultiSample AA 8x samples
        MSAA16x = 16,  // MultiSample AA 16x samples
        TAA = 100      // Temporal AA (post-process)
    }

    public static class AntiAliasingModeExtensions
    {
        public static string ToDisplayString(this AntiAliasingMode mode)
        {
            return mode switch
            {
                AntiAliasingMode.None => "None",
                AntiAliasingMode.MSAA2x => "MSAA 2×",
                AntiAliasingMode.MSAA4x => "MSAA 4×",
                AntiAliasingMode.MSAA8x => "MSAA 8×",
                AntiAliasingMode.MSAA16x => "MSAA 16×",
                AntiAliasingMode.TAA => "TAA (Temporal)",
                _ => "Unknown"
            };
        }

        public static bool IsMSAA(this AntiAliasingMode mode)
        {
            return mode >= AntiAliasingMode.MSAA2x && mode <= AntiAliasingMode.MSAA16x;
        }

        public static int GetSampleCount(this AntiAliasingMode mode)
        {
            return mode.IsMSAA() ? (int)mode : 0;
        }
    }
}
