using System;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Core
{
    /// <summary>
    /// Utilitaire pour vérifier la version d'OpenAL et les extensions disponibles
    /// </summary>
    public static class OpenALVersionChecker
    {
        public static void LogOpenALInfo()
        {
            try
            {
                var vendor = AL.Get(ALGetString.Vendor);
                var version = AL.Get(ALGetString.Version);
                var renderer = AL.Get(ALGetString.Renderer);
                var extensions = AL.Get(ALGetString.Extensions);

                Log.Information("===== OpenAL Information =====");
                Log.Information($"Vendor:   {vendor}");
                Log.Information($"Version:  {version}");
                Log.Information($"Renderer: {renderer}");
                Log.Information($"Extensions: {extensions}");
                Log.Information("==============================");

                // Check for EFX specifically (must use ALC, not AL)
                var device = ALC.GetContextsDevice(ALC.GetCurrentContext());
                bool hasEFX_ALC = ALC.IsExtensionPresent(device, "ALC_EXT_EFX");
                bool hasEFX_AL = AL.IsExtensionPresent("AL_EXT_EFX");

                Log.Information($"ALC_EXT_EFX Extension Present (ALC): {hasEFX_ALC}");
                Log.Information($"AL_EXT_EFX Extension Present (AL):  {hasEFX_AL} (deprecated check method)");

                // Parse vendor to detect OpenAL Soft
                if (vendor != null && vendor.Contains("OpenAL Community", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("✓ OpenAL Soft detected (GOOD - EFX should be available)");
                }
                else if (vendor != null && vendor.Contains("Creative", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("✗ Creative Labs OpenAL detected (BAD - EFX not supported)");
                    Log.Warning("Please install OpenAL Soft - see OPENAL_SOFT_SETUP.md");
                }
                else
                {
                    Log.Warning($"✗ Unknown OpenAL vendor: {vendor}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[OpenALVersionChecker] Failed to get OpenAL info");
            }
        }
    }
}
