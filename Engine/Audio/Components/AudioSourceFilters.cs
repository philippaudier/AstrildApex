using System;
using System.Collections.Generic;
using Engine.Audio.Effects;
using Serilog;

namespace Engine.Audio.Components
{
    /// <summary>
    /// Type de filtre audio pour AudioSource (Unity-like)
    /// </summary>
    public enum AudioSourceFilterType
    {
        None,
        LowPass,
        HighPass,
        BandPass
    }

    /// <summary>
    /// Filtre audio appliqué directement à une AudioSource
    /// Similaire à AudioLowPassFilter / AudioHighPassFilter dans Unity
    /// </summary>
    public class AudioSourceFilter
    {
        public AudioSourceFilterType Type;
        public bool Enabled;
        public object? Settings; // LowPassSettings, HighPassSettings, BandPassSettings

        // EFX handle (internal)
        internal EfxFilterHandle FilterHandle;

        public AudioSourceFilter(AudioSourceFilterType type)
        {
            Type = type;
            Enabled = true;
            Settings = CreateDefaultSettings(type);
        }

        private static object? CreateDefaultSettings(AudioSourceFilterType type)
        {
            return type switch
            {
                AudioSourceFilterType.LowPass => new LowPassSettings(),
                AudioSourceFilterType.HighPass => new HighPassSettings(),
                AudioSourceFilterType.BandPass => new BandPassSettings(),
                _ => null
            };
        }
    }

    /// <summary>
    /// Helper class to manage per-source filters on AudioSource
    /// This provides Unity-like filter components without creating separate Component classes
    /// </summary>
    public static class AudioSourceFilterExtensions
    {
        /// <summary>
        /// Ajoute un filtre Low-Pass à la source (atténue les hautes fréquences)
        /// Similaire à Unity's AudioLowPassFilter component
        /// </summary>
        public static AudioSourceFilter AddLowPassFilter(this AudioSource source, float cutoffFrequency = 5000f)
        {
            var filter = new AudioSourceFilter(AudioSourceFilterType.LowPass)
            {
                Settings = new LowPassSettings
                {
                    Gain = 1.0f,
                    GainHF = CutoffToGainHF(cutoffFrequency)
                }
            };

            source.Filters.Add(filter);
            CreateFilterHandle(filter);
            return filter;
        }

        /// <summary>
        /// Ajoute un filtre High-Pass à la source (atténue les basses fréquences)
        /// Similaire à Unity's AudioHighPassFilter component
        /// </summary>
        public static AudioSourceFilter AddHighPassFilter(this AudioSource source, float cutoffFrequency = 500f)
        {
            var filter = new AudioSourceFilter(AudioSourceFilterType.HighPass)
            {
                Settings = new HighPassSettings
                {
                    Gain = 1.0f,
                    GainLF = CutoffToGainLF(cutoffFrequency)
                }
            };

            source.Filters.Add(filter);
            CreateFilterHandle(filter);
            return filter;
        }

        /// <summary>
        /// Convertit une fréquence de coupure (Hz) en valeur GainHF pour lowpass
        /// (Approximation simple)
        /// </summary>
        private static float CutoffToGainHF(float cutoffHz)
        {
            // 22050 Hz = Nyquist frequency for 44.1kHz sample rate
            // This is a simplified mapping; real implementation would be more complex
            float normalized = Math.Clamp(cutoffHz / 22050f, 0f, 1f);
            return normalized;
        }

        /// <summary>
        /// Convertit une fréquence de coupure (Hz) en valeur GainLF pour highpass
        /// </summary>
        private static float CutoffToGainLF(float cutoffHz)
        {
            // Lower cutoff = more attenuation of low frequencies
            float normalized = Math.Clamp(cutoffHz / 1000f, 0f, 1f);
            return 1.0f - normalized; // Invert: lower cutoff = lower gain
        }

        /// <summary>
        /// Crée le handle EFX pour un filtre
        /// </summary>
        private static void CreateFilterHandle(AudioSourceFilter filter)
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported) return;

            try
            {
                switch (filter.Type)
                {
                    case AudioSourceFilterType.LowPass:
                        if (filter.Settings is LowPassSettings lowPassSettings)
                        {
                            filter.FilterHandle = AudioEfxBackend.Instance.CreateLowPassFilter(lowPassSettings);
                        }
                        break;

                    case AudioSourceFilterType.HighPass:
                        if (filter.Settings is HighPassSettings highPassSettings)
                        {
                            filter.FilterHandle = AudioEfxBackend.Instance.CreateHighPassFilter(highPassSettings);
                        }
                        break;

                    case AudioSourceFilterType.BandPass:
                        if (filter.Settings is BandPassSettings bandPassSettings)
                        {
                            filter.FilterHandle = AudioEfxBackend.Instance.CreateBandPassFilter(bandPassSettings);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioSourceFilter] Failed to create filter handle for {filter.Type}");
            }
        }

        /// <summary>
        /// Détruit le handle EFX d'un filtre
        /// </summary>
        public static void DestroyFilterHandle(AudioSourceFilter filter)
        {
            try
            {
                if (filter.FilterHandle.IsValid)
                {
                    AudioEfxBackend.Instance.DestroyFilter(filter.FilterHandle);
                    filter.FilterHandle = EfxFilterHandle.Invalid;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioSourceFilter] Failed to destroy filter handle");
            }
        }

        /// <summary>
        /// Met à jour les paramètres d'un filtre existant
        /// </summary>
        public static void UpdateFilter(this AudioSourceFilter filter)
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported) return;

            try
            {
                switch (filter.Type)
                {
                    case AudioSourceFilterType.LowPass:
                        if (filter.Settings is LowPassSettings lowPassSettings && filter.FilterHandle.IsValid)
                        {
                            AudioEfxBackend.Instance.UpdateLowPassFilter(filter.FilterHandle, lowPassSettings);
                        }
                        break;

                    case AudioSourceFilterType.HighPass:
                        if (filter.Settings is HighPassSettings highPassSettings && filter.FilterHandle.IsValid)
                        {
                            AudioEfxBackend.Instance.UpdateHighPassFilter(filter.FilterHandle, highPassSettings);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioSourceFilter] Failed to update filter {filter.Type}");
            }
        }
    }
}
