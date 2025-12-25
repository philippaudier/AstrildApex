using System;
using OpenTK.Audio.OpenAL;
using Serilog;
using Engine.Utils;

namespace Engine.Audio.Core
{
    /// <summary>
    /// Utilitaire pour vérifier la version d'OpenAL et les extensions disponibles
    /// </summary>
    public static class OpenALVersionChecker
    {
        public static void LogOpenALInfo()
        {
            // Only emit verbose OpenAL diagnostic info when verbose logging is explicitly enabled
            if (!DebugLogger.EnableVerbose) return;

            try
            {
                var vendor = AL.Get(ALGetString.Vendor);
                var version = AL.Get(ALGetString.Version);
                var renderer = AL.Get(ALGetString.Renderer);
                var extensions = AL.Get(ALGetString.Extensions);

                DebugLogger.Log("[OpenAL] ===== OpenAL Information =====");
                DebugLogger.Log($"[OpenAL] Vendor:   {vendor}");
                DebugLogger.Log($"[OpenAL] Version:  {version}");
                DebugLogger.Log($"[OpenAL] Renderer: {renderer}");
                DebugLogger.Log($"[OpenAL] Extensions: {extensions}");
                DebugLogger.Log("[OpenAL] ==============================");

                // Check for EFX specifically (must use ALC, not AL)
                var device = ALC.GetContextsDevice(ALC.GetCurrentContext());
                bool hasEFX_ALC = ALC.IsExtensionPresent(device, "ALC_EXT_EFX");
                bool hasEFX_AL = AL.IsExtensionPresent("AL_EXT_EFX");

                DebugLogger.Log($"[OpenAL] ALC_EXT_EFX Extension Present (ALC): {hasEFX_ALC}");
                DebugLogger.Log($"[OpenAL] AL_EXT_EFX Extension Present (AL):  {hasEFX_AL} (deprecated check method)");

                // Parse vendor to detect OpenAL Soft
                if (vendor != null && vendor.Contains("OpenAL Community", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log("[OpenAL] ✓ OpenAL Soft detected (GOOD - EFX should be available)");
                }
                else if (vendor != null && vendor.Contains("Creative", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log("[OpenAL] ✗ Creative Labs OpenAL detected (BAD - EFX not supported)");
                    DebugLogger.Log("[OpenAL] Please install OpenAL Soft - see OPENAL_SOFT_SETUP.md");
                }
                else
                {
                    DebugLogger.Log($"[OpenAL] ✗ Unknown OpenAL vendor: {vendor}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[OpenAL] Failed to get OpenAL info: {ex.Message}");
            }
        }
    }
}
