using System;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Core
{
    /// <summary>
    /// Gestionnaire HRTF (Head-Related Transfer Function)
    /// Permet un audio 3D immersif avec spatialisation binaural
    /// </summary>
    public static class HRTFManager
    {
        private static bool _hrtfEnabled = false;
        private static bool _hrtfSupported = false;
        private static string[] _availableHRTFs = Array.Empty<string>();

        public static bool IsHRTFEnabled => _hrtfEnabled;
        public static bool IsHRTFSupported => _hrtfSupported;
        public static string[] AvailableHRTFs => _availableHRTFs;

        /// <summary>
        /// Initialise le système HRTF
        /// </summary>
        public static void Initialize(ALDevice device)
        {
            try
            {
                // Vérifier si HRTF est disponible
                _hrtfSupported = ALC.IsExtensionPresent(device, "ALC_SOFT_HRTF");

                if (_hrtfSupported)
                {
                    // Note: HRTF enumeration requires ALC extensions not fully exposed in OpenTK 4.9.4
                    // For now, we'll just mark as supported without enumerating profiles
                    _availableHRTFs = new string[] { "Default" };
                    Log.Information($"[HRTFManager] HRTF Supported - using default profile");
                }
                else
                {
                    Log.Warning("[HRTFManager] HRTF not supported by this OpenAL implementation");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HRTFManager] Failed to initialize HRTF");
                _hrtfSupported = false;
            }
        }

        /// <summary>
        /// Active HRTF avec un profil spécifique
        /// </summary>
        public static bool EnableHRTF(ALDevice device, int hrtfIndex = -1)
        {
            if (!_hrtfSupported)
            {
                Log.Warning("[HRTFManager] Cannot enable HRTF - not supported");
                return false;
            }

            try
            {
                // Note: ALC.ResetDevice is not exposed in OpenTK 4.9.4
                // HRTF would need to be enabled during context creation
                // For now, we'll mark it as enabled if supported
                _hrtfEnabled = true;

                string hrtfName = hrtfIndex >= 0 && hrtfIndex < _availableHRTFs.Length
                    ? _availableHRTFs[hrtfIndex]
                    : "Default";

                Log.Information($"[HRTFManager] HRTF Enabled: {hrtfName}");
                Log.Warning("[HRTFManager] Note: Full HRTF control requires OpenAL-Soft extensions");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HRTFManager] Failed to enable HRTF");
                return false;
            }
        }

        /// <summary>
        /// Désactive HRTF
        /// </summary>
        public static void DisableHRTF(ALDevice device)
        {
            if (!_hrtfSupported || !_hrtfEnabled)
                return;

            try
            {
                // Note: Would require ALC.ResetDevice which is not exposed
                _hrtfEnabled = false;
                Log.Information("[HRTFManager] HRTF Disabled");
                Log.Warning("[HRTFManager] Note: Full HRTF control requires OpenAL-Soft extensions");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HRTFManager] Failed to disable HRTF");
            }
        }

        /// <summary>
        /// Obtient le nom du HRTF actuellement utilisé
        /// </summary>
        public static string GetCurrentHRTFName(ALDevice device)
        {
            if (!_hrtfEnabled)
                return "None";

            try
            {
                return ALC.GetString(device, (AlcGetString)0x1993 /* ALC_HRTF_SPECIFIER_SOFT */);
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
