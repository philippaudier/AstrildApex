using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Effects
{
    /// <summary>
    /// Gestionnaire des extensions OpenAL EFX (Effects Extension)
    /// Gère la création et l'application des effets audio
    /// </summary>
    public static class EFXManager
    {
        private static bool _efxSupported = false;
        private static bool _initialized = false;

        // Limites d'effets et de slots
        private static int _maxAuxiliarySends = 0;

        public static bool IsEFXSupported => _efxSupported;
        public static bool IsInitialized => _initialized;
        public static int MaxAuxiliarySends => _maxAuxiliarySends;

        /// <summary>
        /// Initialise le système EFX
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // EFX is a CONTEXT extension (ALC), not a source extension (AL)
                // Must check with ALC.IsExtensionPresent, not AL.IsExtensionPresent
                var device = ALC.GetContextsDevice(ALC.GetCurrentContext());
                _efxSupported = ALC.IsExtensionPresent(device, "ALC_EXT_EFX");

                if (_efxSupported)
                {
                    // Query max auxiliary sends from the device
                    // ALC_MAX_AUXILIARY_SENDS = 0x20003
                    const int ALC_MAX_AUXILIARY_SENDS = 0x20003;
                    ALC.GetInteger(device, (AlcGetInteger)ALC_MAX_AUXILIARY_SENDS, 1, out _maxAuxiliarySends);

                    // Fallback to safe default if query fails
                    if (_maxAuxiliarySends <= 0)
                        _maxAuxiliarySends = 4;

                    Log.Information($"[EFXManager] ✓ ALC_EXT_EFX Supported - Max Auxiliary Sends: {_maxAuxiliarySends}");

                    // Initialize the new EFX backend
                    AudioEfxBackend.Instance.Initialize();
                }
                else
                {
                    Log.Warning("[EFXManager] ALC_EXT_EFX extension not found - effects disabled");
                    Log.Warning("[EFXManager] This should not happen with OpenAL Soft - please verify your OpenAL installation");
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[EFXManager] Failed to initialize EFX");
                _efxSupported = false;
            }
        }

        /// <summary>
        /// Vérifie les erreurs OpenAL
        /// </summary>
        public static bool CheckALError(string context = "")
        {
            var error = AL.GetError();
            if (error != ALError.NoError)
            {
                Log.Error($"[EFXManager] OpenAL Error ({context}): {error}");
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Types d'effets EFX disponibles
    /// </summary>
    public enum EFXEffectType
    {
        Reverb,
        Chorus,
        Distortion,
        Echo,
        Flanger,
        FrequencyShifter,
        VocalMorpher,
        PitchShifter,
        RingModulator,
        Autowah,
        Compressor,
        Equalizer
    }
}
