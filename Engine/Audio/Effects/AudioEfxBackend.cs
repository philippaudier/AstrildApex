using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Effects
{
    /*
     * ============================================================================
     * AUDIO ARCHITECTURE OVERVIEW
     * ============================================================================
     *
     * Current Audio System (Before EFX Integration):
     * ----------------------------------------------
     * 1. AudioEngine (singleton)
     *    - Initializes OpenAL device/context
     *    - Manages AudioListener position/orientation
     *    - Tracks active AudioSource components
     *    - Handles category-based volume (Master, Music, SFX, Voice, Ambient)
     *
     * 2. AudioSource (ECS Component)
     *    - Plays audio clips (streaming or buffered)
     *    - 3D spatialization support
     *    - Category assignment (Music/SFX/Voice/Ambient)
     *    - Volume, pitch, loop, doppler, rolloff
     *    - Has a list of AudioEffect objects (currently stub implementations)
     *
     * 3. AudioMixer & AudioMixerGroup
     *    - Unity-like mixer groups (Master, Music, SFX, Voice, Ambient)
     *    - Hierarchical volume control
     *    - Mute/Solo support
     *
     * 4. AudioMixerPanel (Editor UI)
     *    - Visual mixer interface with VU meters
     *    - Volume sliders per group
     *    - Mute/Solo buttons
     *
     * 5. Effects & Filters (Stub Implementations)
     *    - AudioEffect base class (Reverb, Echo, Chorus, Distortion)
     *    - AudioFilter base class (Lowpass, Highpass, Bandpass)
     *    - Currently have TODOs for EFX implementation
     *
     * EFX Integration Strategy:
     * -------------------------
     * This AudioEfxBackend class provides the missing EFX implementation layer:
     *
     * - Wraps OpenAL EFX calls (via EFXInterop P/Invoke bindings)
     * - Manages lifetime of EFX objects (effects, filters, aux slots)
     * - Provides Unity-like API for creating/applying effects
     * - Integrates with existing AudioSource and AudioMixerGroup classes
     *
     * Key Features:
     * 1. Per-Source Direct Filters (like Unity AudioLowPassFilter)
     * 2. Per-MixerGroup Bus Effects (global reverb, EQ on groups)
     * 3. Reverb Zones for 3D spatial audio effects
     *
     * ============================================================================
     */

    /// <summary>
    /// Handle wrapper for EFX Effect objects
    /// </summary>
    public struct EfxEffectHandle
    {
        public int Id;
        public bool IsValid => Id > 0 && EFX.IsEffect(Id);

        public EfxEffectHandle(int id) { Id = id; }

        public static readonly EfxEffectHandle Invalid = new EfxEffectHandle(0);
    }

    /// <summary>
    /// Handle wrapper for EFX Filter objects
    /// </summary>
    public struct EfxFilterHandle
    {
        public int Id;
        public bool IsValid => Id > 0 && EFX.IsFilter(Id);

        public EfxFilterHandle(int id) { Id = id; }

        public static readonly EfxFilterHandle Invalid = new EfxFilterHandle(0);
    }

    /// <summary>
    /// Handle wrapper for EFX Auxiliary Effect Slot objects
    /// </summary>
    public struct EfxAuxSlotHandle
    {
        public int Id;
        public bool IsValid => Id > 0 && EFX.IsAuxiliaryEffectSlot(Id);

        public EfxAuxSlotHandle(int id) { Id = id; }

        public static readonly EfxAuxSlotHandle Invalid = new EfxAuxSlotHandle(0);
    }

    /// <summary>
    /// Settings for Reverb effect (Unity-like)
    /// </summary>
    public class ReverbSettings
    {
        public float Density = 1.0f;          // 0.0 to 1.0
        public float Diffusion = 1.0f;        // 0.0 to 1.0
        public float Gain = 0.32f;            // 0.0 to 1.0
        public float GainHF = 0.89f;          // 0.0 to 1.0
        public float DecayTime = 1.49f;       // 0.1 to 20.0 seconds
        public float DecayHFRatio = 0.83f;    // 0.1 to 2.0
        public float ReflectionsGain = 0.05f; // 0.0 to 3.16
        public float ReflectionsDelay = 0.007f; // 0.0 to 0.3 seconds
        public float LateReverbGain = 1.26f;  // 0.0 to 10.0
        public float LateReverbDelay = 0.011f; // 0.0 to 0.1 seconds
        public float AirAbsorptionGainHF = 0.994f; // 0.892 to 1.0
        public float RoomRolloffFactor = 0.0f; // 0.0 to 10.0
        public bool DecayHFLimit = true;

        public static ReverbSettings GenericPreset() => new ReverbSettings();

        public static ReverbSettings RoomPreset() => new ReverbSettings
        {
            DecayTime = 0.4f,
            LateReverbGain = 1.0f
        };

        public static ReverbSettings CathedralPreset() => new ReverbSettings
        {
            DecayTime = 7.0f,
            LateReverbGain = 2.0f,
            ReflectionsDelay = 0.03f
        };

        public static ReverbSettings CavePreset() => new ReverbSettings
        {
            DecayTime = 3.0f,
            LateReverbGain = 1.5f,
            Diffusion = 0.8f
        };
    }

    /// <summary>
    /// Settings for Echo effect
    /// </summary>
    public class EchoSettings
    {
        public float Delay = 0.1f;      // 0.0 to 0.207 seconds
        public float LRDelay = 0.1f;    // 0.0 to 0.404 seconds
        public float Damping = 0.5f;    // 0.0 to 0.99
        public float Feedback = 0.5f;   // 0.0 to 1.0
        public float Spread = -1.0f;    // -1.0 to 1.0
    }

    /// <summary>
    /// Settings for Low-Pass filter
    /// </summary>
    public class LowPassSettings
    {
        public float Gain = 1.0f;    // 0.0 to 1.0 (overall volume)
        public float GainHF = 1.0f;  // 0.0 to 1.0 (high freq attenuation)
    }

    /// <summary>
    /// Settings for High-Pass filter
    /// </summary>
    public class HighPassSettings
    {
        public float Gain = 1.0f;    // 0.0 to 1.0 (overall volume)
        public float GainLF = 1.0f;  // 0.0 to 1.0 (low freq attenuation)
    }

    /// <summary>
    /// Settings for Band-Pass filter
    /// </summary>
    public class BandPassSettings
    {
        public float Gain = 1.0f;    // 0.0 to 1.0 (overall volume)
        public float GainLF = 1.0f;  // 0.0 to 1.0 (low freq attenuation)
        public float GainHF = 1.0f;  // 0.0 to 1.0 (high freq attenuation)
    }

    /// <summary>
    /// OpenAL EFX Backend - Provides a clean API for creating and managing audio effects
    ///
    /// This class is independent from ECS and works directly with OpenAL IDs.
    /// Higher-level components (AudioSource, AudioMixerGroup, ReverbZone) will use this backend.
    /// </summary>
    public sealed class AudioEfxBackend : IDisposable
    {
        private static AudioEfxBackend? _instance;
        public static AudioEfxBackend Instance => _instance ??= new AudioEfxBackend();

        private bool _initialized = false;
        private bool _efxSupported = false;

        // Track created objects for cleanup
        private readonly HashSet<int> _createdEffects = new();
        private readonly HashSet<int> _createdFilters = new();
        private readonly HashSet<int> _createdAuxSlots = new();

        public bool IsInitialized => _initialized;
        public bool IsEFXSupported => _efxSupported;

        private AudioEfxBackend() { }

        /// <summary>
        /// Initialize the EFX backend (should be called after OpenAL context is created)
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Log.Warning("[AudioEfxBackend] Already initialized");
                return;
            }

            try
            {
                // Check if EFX extension is available (must use ALC, not AL)
                var device = ALC.GetContextsDevice(ALC.GetCurrentContext());
                _efxSupported = ALC.IsExtensionPresent(device, "ALC_EXT_EFX");

                if (_efxSupported)
                {
                    Log.Information("[AudioEfxBackend] ✓ ALC_EXT_EFX extension detected and enabled");
                }
                else
                {
                    Log.Warning("[AudioEfxBackend] ALC_EXT_EFX extension not supported - effects will be disabled");
                }

                _initialized = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to initialize");
                _efxSupported = false;
            }
        }

        #region Effect Creation

        /// <summary>
        /// Creates a Reverb effect with the given settings
        /// </summary>
        public EfxEffectHandle CreateReverbEffect(ReverbSettings settings)
        {
            if (!_efxSupported)
            {
                Log.Warning("[AudioEfxBackend] Cannot create reverb - EFX not supported");
                return EfxEffectHandle.Invalid;
            }

            try
            {
                EFX.GenEffects(1, out int effectId);
                CheckALError("GenEffects");

                EFX.Effecti(effectId, EFX.AL_EFFECT_TYPE, EFX.AL_EFFECT_REVERB);
                CheckALError("Effecti TYPE");

                // Apply settings
                EFX.Effectf(effectId, EFX.AL_REVERB_DENSITY, settings.Density);
                EFX.Effectf(effectId, EFX.AL_REVERB_DIFFUSION, settings.Diffusion);
                EFX.Effectf(effectId, EFX.AL_REVERB_GAIN, settings.Gain);
                EFX.Effectf(effectId, EFX.AL_REVERB_GAINHF, settings.GainHF);
                EFX.Effectf(effectId, EFX.AL_REVERB_DECAY_TIME, settings.DecayTime);
                EFX.Effectf(effectId, EFX.AL_REVERB_DECAY_HFRATIO, settings.DecayHFRatio);
                EFX.Effectf(effectId, EFX.AL_REVERB_REFLECTIONS_GAIN, settings.ReflectionsGain);
                EFX.Effectf(effectId, EFX.AL_REVERB_REFLECTIONS_DELAY, settings.ReflectionsDelay);
                EFX.Effectf(effectId, EFX.AL_REVERB_LATE_REVERB_GAIN, settings.LateReverbGain);
                EFX.Effectf(effectId, EFX.AL_REVERB_LATE_REVERB_DELAY, settings.LateReverbDelay);
                EFX.Effectf(effectId, EFX.AL_REVERB_AIR_ABSORPTION_GAINHF, settings.AirAbsorptionGainHF);
                EFX.Effectf(effectId, EFX.AL_REVERB_ROOM_ROLLOFF_FACTOR, settings.RoomRolloffFactor);
                EFX.Effecti(effectId, EFX.AL_REVERB_DECAY_HFLIMIT, settings.DecayHFLimit ? 1 : 0);
                CheckALError("Reverb parameters");

                _createdEffects.Add(effectId);
                Log.Debug($"[AudioEfxBackend] Created reverb effect: {effectId}");
                return new EfxEffectHandle(effectId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to create reverb effect");
                return EfxEffectHandle.Invalid;
            }
        }

        /// <summary>
        /// Creates an Echo effect with the given settings
        /// </summary>
        public EfxEffectHandle CreateEchoEffect(EchoSettings settings)
        {
            if (!_efxSupported)
            {
                Log.Warning("[AudioEfxBackend] Cannot create echo - EFX not supported");
                return EfxEffectHandle.Invalid;
            }

            try
            {
                EFX.GenEffects(1, out int effectId);
                CheckALError("GenEffects");

                EFX.Effecti(effectId, EFX.AL_EFFECT_TYPE, EFX.AL_EFFECT_ECHO);
                CheckALError("Effecti TYPE");

                EFX.Effectf(effectId, EFX.AL_ECHO_DELAY, settings.Delay);
                EFX.Effectf(effectId, EFX.AL_ECHO_LRDELAY, settings.LRDelay);
                EFX.Effectf(effectId, EFX.AL_ECHO_DAMPING, settings.Damping);
                EFX.Effectf(effectId, EFX.AL_ECHO_FEEDBACK, settings.Feedback);
                EFX.Effectf(effectId, EFX.AL_ECHO_SPREAD, settings.Spread);
                CheckALError("Echo parameters");

                _createdEffects.Add(effectId);
                Log.Debug($"[AudioEfxBackend] Created echo effect: {effectId}");
                return new EfxEffectHandle(effectId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to create echo effect");
                return EfxEffectHandle.Invalid;
            }
        }

        /// <summary>
        /// Updates an existing reverb effect with new settings
        /// </summary>
        public void UpdateReverbEffect(EfxEffectHandle handle, ReverbSettings settings)
        {
            if (!handle.IsValid || !_efxSupported) return;

            try
            {
                EFX.Effectf(handle.Id, EFX.AL_REVERB_DENSITY, settings.Density);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_DIFFUSION, settings.Diffusion);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_GAIN, settings.Gain);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_GAINHF, settings.GainHF);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_DECAY_TIME, settings.DecayTime);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_DECAY_HFRATIO, settings.DecayHFRatio);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_REFLECTIONS_GAIN, settings.ReflectionsGain);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_REFLECTIONS_DELAY, settings.ReflectionsDelay);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_LATE_REVERB_GAIN, settings.LateReverbGain);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_LATE_REVERB_DELAY, settings.LateReverbDelay);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_AIR_ABSORPTION_GAINHF, settings.AirAbsorptionGainHF);
                EFX.Effectf(handle.Id, EFX.AL_REVERB_ROOM_ROLLOFF_FACTOR, settings.RoomRolloffFactor);
                EFX.Effecti(handle.Id, EFX.AL_REVERB_DECAY_HFLIMIT, settings.DecayHFLimit ? 1 : 0);
                CheckALError("Update reverb parameters");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to update reverb effect");
            }
        }

        /// <summary>
        /// Destroys an effect object
        /// </summary>
        public void DestroyEffect(EfxEffectHandle handle)
        {
            if (!handle.IsValid) return;

            try
            {
                int id = handle.Id;
                EFX.DeleteEffects(1, ref id);
                _createdEffects.Remove(handle.Id);
                Log.Debug($"[AudioEfxBackend] Destroyed effect: {handle.Id}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to destroy effect {handle.Id}");
            }
        }

        #endregion

        #region Filter Creation

        /// <summary>
        /// Creates a Low-Pass filter
        /// </summary>
        public EfxFilterHandle CreateLowPassFilter(LowPassSettings settings)
        {
            if (!_efxSupported)
            {
                Log.Warning("[AudioEfxBackend] Cannot create lowpass filter - EFX not supported");
                return EfxFilterHandle.Invalid;
            }

            try
            {
                EFX.GenFilters(1, out int filterId);
                CheckALError("GenFilters");

                EFX.Filteri(filterId, EFX.AL_FILTER_TYPE, EFX.AL_FILTER_LOWPASS);
                CheckALError("Filteri TYPE");

                EFX.Filterf(filterId, EFX.AL_LOWPASS_GAIN, settings.Gain);
                EFX.Filterf(filterId, EFX.AL_LOWPASS_GAINHF, settings.GainHF);
                CheckALError("Lowpass parameters");

                _createdFilters.Add(filterId);
                Log.Debug($"[AudioEfxBackend] Created lowpass filter: {filterId}");
                return new EfxFilterHandle(filterId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to create lowpass filter");
                return EfxFilterHandle.Invalid;
            }
        }

        /// <summary>
        /// Creates a High-Pass filter
        /// </summary>
        public EfxFilterHandle CreateHighPassFilter(HighPassSettings settings)
        {
            if (!_efxSupported)
            {
                Log.Warning("[AudioEfxBackend] Cannot create highpass filter - EFX not supported");
                return EfxFilterHandle.Invalid;
            }

            try
            {
                EFX.GenFilters(1, out int filterId);
                CheckALError("GenFilters");

                EFX.Filteri(filterId, EFX.AL_FILTER_TYPE, EFX.AL_FILTER_HIGHPASS);
                CheckALError("Filteri TYPE");

                EFX.Filterf(filterId, EFX.AL_HIGHPASS_GAIN, settings.Gain);
                EFX.Filterf(filterId, EFX.AL_HIGHPASS_GAINLF, settings.GainLF);
                CheckALError("Highpass parameters");

                _createdFilters.Add(filterId);
                Log.Debug($"[AudioEfxBackend] Created highpass filter: {filterId}");
                return new EfxFilterHandle(filterId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to create highpass filter");
                return EfxFilterHandle.Invalid;
            }
        }

        /// <summary>
        /// Creates a Band-Pass filter
        /// </summary>
        public EfxFilterHandle CreateBandPassFilter(BandPassSettings settings)
        {
            if (!_efxSupported)
            {
                Log.Warning("[AudioEfxBackend] Cannot create bandpass filter - EFX not supported");
                return EfxFilterHandle.Invalid;
            }

            try
            {
                EFX.GenFilters(1, out int filterId);
                CheckALError("GenFilters");

                EFX.Filteri(filterId, EFX.AL_FILTER_TYPE, EFX.AL_FILTER_BANDPASS);
                CheckALError("Filteri TYPE");

                EFX.Filterf(filterId, EFX.AL_BANDPASS_GAIN, settings.Gain);
                EFX.Filterf(filterId, EFX.AL_BANDPASS_GAINLF, settings.GainLF);
                EFX.Filterf(filterId, EFX.AL_BANDPASS_GAINHF, settings.GainHF);
                CheckALError("Bandpass parameters");

                _createdFilters.Add(filterId);
                Log.Debug($"[AudioEfxBackend] Created bandpass filter: {filterId}");
                return new EfxFilterHandle(filterId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to create bandpass filter");
                return EfxFilterHandle.Invalid;
            }
        }

        /// <summary>
        /// Updates a low-pass filter with new settings
        /// </summary>
        public void UpdateLowPassFilter(EfxFilterHandle handle, LowPassSettings settings)
        {
            if (!handle.IsValid || !_efxSupported) return;

            try
            {
                EFX.Filterf(handle.Id, EFX.AL_LOWPASS_GAIN, settings.Gain);
                EFX.Filterf(handle.Id, EFX.AL_LOWPASS_GAINHF, settings.GainHF);
                CheckALError("Update lowpass parameters");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to update lowpass filter");
            }
        }

        /// <summary>
        /// Updates a high-pass filter with new settings
        /// </summary>
        public void UpdateHighPassFilter(EfxFilterHandle handle, HighPassSettings settings)
        {
            if (!handle.IsValid || !_efxSupported) return;

            try
            {
                EFX.Filterf(handle.Id, EFX.AL_HIGHPASS_GAIN, settings.Gain);
                EFX.Filterf(handle.Id, EFX.AL_HIGHPASS_GAINLF, settings.GainLF);
                CheckALError("Update highpass parameters");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to update highpass filter");
            }
        }

        /// <summary>
        /// Destroys a filter object
        /// </summary>
        public void DestroyFilter(EfxFilterHandle handle)
        {
            if (!handle.IsValid) return;

            try
            {
                int id = handle.Id;
                EFX.DeleteFilters(1, ref id);
                _createdFilters.Remove(handle.Id);
                Log.Debug($"[AudioEfxBackend] Destroyed filter: {handle.Id}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to destroy filter {handle.Id}");
            }
        }

        #endregion

        #region Auxiliary Effect Slots

        /// <summary>
        /// Creates an auxiliary effect slot and attaches an effect to it
        /// </summary>
        public EfxAuxSlotHandle CreateAuxSlot(EfxEffectHandle effect, float gain = 1.0f)
        {
            if (!_efxSupported)
            {
                Log.Warning("[AudioEfxBackend] Cannot create aux slot - EFX not supported");
                return EfxAuxSlotHandle.Invalid;
            }

            try
            {
                EFX.GenAuxiliaryEffectSlots(1, out int slotId);
                CheckALError("GenAuxiliaryEffectSlots");

                if (effect.IsValid)
                {
                    EFX.AuxiliaryEffectSloti(slotId, EFX.AL_EFFECTSLOT_EFFECT, effect.Id);
                    CheckALError("AuxiliaryEffectSloti EFFECT");
                }

                EFX.AuxiliaryEffectSlotf(slotId, EFX.AL_EFFECTSLOT_GAIN, gain);
                CheckALError("AuxiliaryEffectSlotf GAIN");

                _createdAuxSlots.Add(slotId);
                Log.Debug($"[AudioEfxBackend] Created aux slot: {slotId}");
                return new EfxAuxSlotHandle(slotId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to create aux slot");
                return EfxAuxSlotHandle.Invalid;
            }
        }

        /// <summary>
        /// Updates the effect attached to an auxiliary slot
        /// </summary>
        public void UpdateAuxSlotEffect(EfxAuxSlotHandle slot, EfxEffectHandle effect)
        {
            if (!slot.IsValid || !_efxSupported) return;

            try
            {
                EFX.AuxiliaryEffectSloti(slot.Id, EFX.AL_EFFECTSLOT_EFFECT,
                    effect.IsValid ? effect.Id : 0);
                CheckALError("UpdateAuxSlotEffect");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to update aux slot effect");
            }
        }

        /// <summary>
        /// Updates the gain of an auxiliary slot
        /// </summary>
        public void UpdateAuxSlotGain(EfxAuxSlotHandle slot, float gain)
        {
            if (!slot.IsValid || !_efxSupported) return;

            try
            {
                EFX.AuxiliaryEffectSlotf(slot.Id, EFX.AL_EFFECTSLOT_GAIN, gain);
                CheckALError("UpdateAuxSlotGain");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEfxBackend] Failed to update aux slot gain");
            }
        }

        /// <summary>
        /// Destroys an auxiliary effect slot
        /// </summary>
        public void DestroyAuxSlot(EfxAuxSlotHandle handle)
        {
            if (!handle.IsValid) return;

            try
            {
                int id = handle.Id;
                EFX.DeleteAuxiliaryEffectSlots(1, ref id);
                _createdAuxSlots.Remove(handle.Id);
                Log.Debug($"[AudioEfxBackend] Destroyed aux slot: {handle.Id}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to destroy aux slot {handle.Id}");
            }
        }

        #endregion

        #region Source Attachment

        /// <summary>
        /// Attaches a direct filter to an audio source
        /// </summary>
        public void AttachDirectFilterToSource(int sourceId, EfxFilterHandle filter)
        {
            if (!_efxSupported || sourceId <= 0) return;

            try
            {
                EFX.SourceAttachDirectFilter(sourceId, filter.IsValid ? filter.Id : 0);
                CheckALError($"AttachDirectFilter to source {sourceId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to attach filter to source {sourceId}");
            }
        }

        /// <summary>
        /// Attaches an auxiliary effect slot to an audio source
        /// </summary>
        public void AttachAuxSlotToSource(int sourceId, EfxAuxSlotHandle slot, int sendIndex,
            EfxFilterHandle? sendFilter = null)
        {
            if (!_efxSupported || sourceId <= 0 || !slot.IsValid) return;

            try
            {
                int filterId = sendFilter?.Id ?? 0;
                // Use EFX.alSource3i for auxiliary sends (slot, sendIndex, filter)
                EFX.alSource3i(sourceId, EFX.AL_AUXILIARY_SEND_FILTER, slot.Id, sendIndex, filterId);
                CheckALError($"AttachAuxSlot to source {sourceId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to attach aux slot to source {sourceId}");
            }
        }

        /// <summary>
        /// Detaches an auxiliary effect slot from a source's send index
        /// </summary>
        public void DetachAuxSlotFromSource(int sourceId, int sendIndex)
        {
            if (!_efxSupported || sourceId <= 0) return;

            try
            {
                EFX.SourceDetachAuxSlot(sourceId, sendIndex);
                CheckALError($"DetachAuxSlot from source {sourceId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to detach aux slot from source {sourceId}");
            }
        }

        /// <summary>
        /// Detaches the direct filter from a source
        /// </summary>
        public void DetachDirectFilterFromSource(int sourceId)
        {
            if (!_efxSupported || sourceId <= 0) return;

            try
            {
                EFX.SourceDetachDirectFilter(sourceId);
                CheckALError($"DetachDirectFilter from source {sourceId}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioEfxBackend] Failed to detach filter from source {sourceId}");
            }
        }

        #endregion

        #region Utility

        private void CheckALError(string context)
        {
            var error = AL.GetError();
            if (error != ALError.NoError)
            {
                Log.Warning($"[AudioEfxBackend] OpenAL Error ({context}): {error}");
            }
        }

        public void Dispose()
        {
            if (!_initialized) return;

            Log.Information("[AudioEfxBackend] Disposing...");

            // Clean up all created EFX objects
            foreach (var effectId in _createdEffects.ToArray())
            {
                try
                {
                    int id = effectId;
                    EFX.DeleteEffects(1, ref id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"[AudioEfxBackend] Failed to delete effect {effectId}");
                }
            }
            _createdEffects.Clear();

            foreach (var filterId in _createdFilters.ToArray())
            {
                try
                {
                    int id = filterId;
                    EFX.DeleteFilters(1, ref id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"[AudioEfxBackend] Failed to delete filter {filterId}");
                }
            }
            _createdFilters.Clear();

            foreach (var slotId in _createdAuxSlots.ToArray())
            {
                try
                {
                    int id = slotId;
                    EFX.DeleteAuxiliaryEffectSlots(1, ref id);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"[AudioEfxBackend] Failed to delete aux slot {slotId}");
                }
            }
            _createdAuxSlots.Clear();

            _initialized = false;
            Log.Information("[AudioEfxBackend] Disposed");
        }

        #endregion
    }
}
