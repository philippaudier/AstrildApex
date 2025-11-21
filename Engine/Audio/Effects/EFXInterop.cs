using System;
using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;

namespace Engine.Audio.Effects
{
    /// <summary>
    /// P/Invoke bindings for OpenAL EFX (Effects Extension)
    /// These are not included in OpenTK.Audio.OpenAL by default, so we define them here.
    ///
    /// Reference: OpenAL Effects Extension Guide
    /// https://github.com/kcat/openal-soft/blob/master/docs/effects/
    /// </summary>
    public static class EFX
    {
        // Effect object functions
        [DllImport("OpenAL32.dll", EntryPoint = "alGenEffects", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GenEffects(int count, out int effect);

        [DllImport("OpenAL32.dll", EntryPoint = "alDeleteEffects", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeleteEffects(int count, ref int effect);

        [DllImport("OpenAL32.dll", EntryPoint = "alIsEffect", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool IsEffect(int effect);

        [DllImport("OpenAL32.dll", EntryPoint = "alEffecti", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Effecti(int effect, int param, int value);

        [DllImport("OpenAL32.dll", EntryPoint = "alEffectf", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Effectf(int effect, int param, float value);

        [DllImport("OpenAL32.dll", EntryPoint = "alEffectfv", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Effectfv(int effect, int param, float[] values);

        // Filter object functions
        [DllImport("OpenAL32.dll", EntryPoint = "alGenFilters", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GenFilters(int count, out int filter);

        [DllImport("OpenAL32.dll", EntryPoint = "alDeleteFilters", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeleteFilters(int count, ref int filter);

        [DllImport("OpenAL32.dll", EntryPoint = "alIsFilter", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool IsFilter(int filter);

        [DllImport("OpenAL32.dll", EntryPoint = "alFilteri", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Filteri(int filter, int param, int value);

        [DllImport("OpenAL32.dll", EntryPoint = "alFilterf", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Filterf(int filter, int param, float value);

        // Auxiliary Effect Slot functions
        [DllImport("OpenAL32.dll", EntryPoint = "alGenAuxiliaryEffectSlots", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GenAuxiliaryEffectSlots(int count, out int slot);

        [DllImport("OpenAL32.dll", EntryPoint = "alDeleteAuxiliaryEffectSlots", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DeleteAuxiliaryEffectSlots(int count, ref int slot);

        [DllImport("OpenAL32.dll", EntryPoint = "alIsAuxiliaryEffectSlot", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool IsAuxiliaryEffectSlot(int slot);

        [DllImport("OpenAL32.dll", EntryPoint = "alAuxiliaryEffectSloti", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AuxiliaryEffectSloti(int slot, int param, int value);

        [DllImport("OpenAL32.dll", EntryPoint = "alAuxiliaryEffectSlotf", CallingConvention = CallingConvention.Cdecl)]
        public static extern void AuxiliaryEffectSlotf(int slot, int param, float value);

        // Effect Types
        public const int AL_EFFECT_TYPE = 0x8001;
        public const int AL_EFFECT_NULL = 0x0000;
        public const int AL_EFFECT_REVERB = 0x0001;
        public const int AL_EFFECT_CHORUS = 0x0002;
        public const int AL_EFFECT_DISTORTION = 0x0003;
        public const int AL_EFFECT_ECHO = 0x0004;
        public const int AL_EFFECT_FLANGER = 0x0005;
        public const int AL_EFFECT_FREQUENCY_SHIFTER = 0x0006;
        public const int AL_EFFECT_VOCAL_MORPHER = 0x0007;
        public const int AL_EFFECT_PITCH_SHIFTER = 0x0008;
        public const int AL_EFFECT_RING_MODULATOR = 0x0009;
        public const int AL_EFFECT_AUTOWAH = 0x000A;
        public const int AL_EFFECT_COMPRESSOR = 0x000B;
        public const int AL_EFFECT_EQUALIZER = 0x000C;
        public const int AL_EFFECT_EAXREVERB = 0x8000;

        // Reverb effect parameters
        public const int AL_REVERB_DENSITY = 0x0001;
        public const int AL_REVERB_DIFFUSION = 0x0002;
        public const int AL_REVERB_GAIN = 0x0003;
        public const int AL_REVERB_GAINHF = 0x0004;
        public const int AL_REVERB_DECAY_TIME = 0x0005;
        public const int AL_REVERB_DECAY_HFRATIO = 0x0006;
        public const int AL_REVERB_REFLECTIONS_GAIN = 0x0007;
        public const int AL_REVERB_REFLECTIONS_DELAY = 0x0008;
        public const int AL_REVERB_LATE_REVERB_GAIN = 0x0009;
        public const int AL_REVERB_LATE_REVERB_DELAY = 0x000A;
        public const int AL_REVERB_AIR_ABSORPTION_GAINHF = 0x000B;
        public const int AL_REVERB_ROOM_ROLLOFF_FACTOR = 0x000C;
        public const int AL_REVERB_DECAY_HFLIMIT = 0x000D;

        // Echo effect parameters
        public const int AL_ECHO_DELAY = 0x0001;
        public const int AL_ECHO_LRDELAY = 0x0002;
        public const int AL_ECHO_DAMPING = 0x0003;
        public const int AL_ECHO_FEEDBACK = 0x0004;
        public const int AL_ECHO_SPREAD = 0x0005;

        // Filter Types
        public const int AL_FILTER_TYPE = 0x8001;
        public const int AL_FILTER_NULL = 0x0000;
        public const int AL_FILTER_LOWPASS = 0x0001;
        public const int AL_FILTER_HIGHPASS = 0x0002;
        public const int AL_FILTER_BANDPASS = 0x0003;

        // Lowpass filter parameters
        public const int AL_LOWPASS_GAIN = 0x0001;
        public const int AL_LOWPASS_GAINHF = 0x0002;

        // Highpass filter parameters
        public const int AL_HIGHPASS_GAIN = 0x0001;
        public const int AL_HIGHPASS_GAINLF = 0x0002;

        // Bandpass filter parameters
        public const int AL_BANDPASS_GAIN = 0x0001;
        public const int AL_BANDPASS_GAINLF = 0x0002;
        public const int AL_BANDPASS_GAINHF = 0x0003;

        // Auxiliary Effect Slot parameters
        public const int AL_EFFECTSLOT_EFFECT = 0x0001;
        public const int AL_EFFECTSLOT_GAIN = 0x0002;
        public const int AL_EFFECTSLOT_AUXILIARY_SEND_AUTO = 0x0003;

        // Source parameters for EFX
        public const int AL_DIRECT_FILTER = 0x20005;
        public const int AL_AUXILIARY_SEND_FILTER = 0x20006;
        public const int AL_AIR_ABSORPTION_FACTOR = 0x20007;
        public const int AL_ROOM_ROLLOFF_FACTOR = 0x20008;
        public const int AL_CONE_OUTER_GAINHF = 0x20009;
        public const int AL_DIRECT_FILTER_GAINHF_AUTO = 0x2000A;
        public const int AL_AUXILIARY_SEND_FILTER_GAIN_AUTO = 0x2000B;
        public const int AL_AUXILIARY_SEND_FILTER_GAINHF_AUTO = 0x2000C;

        // P/Invoke for AL.Source3i (needed for auxiliary sends)
        [DllImport("OpenAL32.dll", EntryPoint = "alSource3i", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void alSource3i(int source, int param, int value1, int value2, int value3);

        /// <summary>
        /// Helper to attach an auxiliary effect slot to a source
        /// </summary>
        public static void SourceAttachAuxSlot(int sourceId, int slotId, int sendIndex)
        {
            // Use alSource3i for auxiliary sends (not available in OpenTK)
            alSource3i(sourceId, AL_AUXILIARY_SEND_FILTER, slotId, sendIndex, 0);
        }

        /// <summary>
        /// Helper to detach an auxiliary effect slot from a source
        /// </summary>
        public static void SourceDetachAuxSlot(int sourceId, int sendIndex)
        {
            alSource3i(sourceId, AL_AUXILIARY_SEND_FILTER, 0, sendIndex, 0);
        }

        /// <summary>
        /// Helper to attach a direct filter to a source
        /// </summary>
        public static void SourceAttachDirectFilter(int sourceId, int filterId)
        {
            AL.Source(sourceId, (ALSourcei)AL_DIRECT_FILTER, filterId);
        }

        /// <summary>
        /// Helper to detach a direct filter from a source
        /// </summary>
        public static void SourceDetachDirectFilter(int sourceId)
        {
            AL.Source(sourceId, (ALSourcei)AL_DIRECT_FILTER, 0);
        }
    }
}
