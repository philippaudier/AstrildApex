using Serilog;

namespace Engine.Audio.Filters
{
    /// <summary>
    /// Filtre passe-bande - Atténue à la fois les basses et hautes fréquences
    /// Ne laisse passer qu'une bande de fréquences (milieu du spectre)
    /// Utile pour simuler l'effet "vieux phonographe" ou "talkie-walkie"
    /// </summary>
    public sealed class BandpassFilter : AudioFilter
    {
        private float _gain = 1.0f;
        private float _gainLF = 1.0f;
        private float _gainHF = 1.0f;

        /// <summary>
        /// Gain global (0.0 - 1.0)
        /// </summary>
        public float Gain
        {
            get => _gain;
            set
            {
                _gain = System.Math.Clamp(value, 0f, 1f);
                UpdateParameters();
            }
        }

        /// <summary>
        /// Gain des basses fréquences (0.0 - 1.0)
        /// </summary>
        public float GainLF
        {
            get => _gainLF;
            set
            {
                _gainLF = System.Math.Clamp(value, 0f, 1f);
                UpdateParameters();
            }
        }

        /// <summary>
        /// Gain des hautes fréquences (0.0 - 1.0)
        /// </summary>
        public float GainHF
        {
            get => _gainHF;
            set
            {
                _gainHF = System.Math.Clamp(value, 0f, 1f);
                UpdateParameters();
            }
        }

        public BandpassFilter() : base("Bandpass")
        {
        }

        public override void Create()
        {
            if (!Effects.EFXManager.IsEFXSupported)
            {
                Log.Warning("[BandpassFilter] EFX not supported");
                return;
            }

            // TODO: Implémenter avec OpenAL EFX
            // _filterId = AL.GenFilter();
            // AL.Filter(_filterId, FilterInteger.FilterType, (int)FilterType.Bandpass);

            UpdateParameters();
            Log.Information("[BandpassFilter] Created");
        }

        public override void Apply(int sourceId)
        {
            if (_filterId == -1 || !IsEnabled)
                return;

            // TODO: Implémenter
            // AL.Source(sourceId, ALSourcei.DirectFilter, _filterId);
        }

        public override void UpdateParameters()
        {
            if (_filterId == -1)
                return;

            // TODO: Implémenter
            // AL.Filter(_filterId, FilterFloat.BandpassGain, _gain);
            // AL.Filter(_filterId, FilterFloat.BandpassGainLF, _gainLF);
            // AL.Filter(_filterId, FilterFloat.BandpassGainHF, _gainHF);
        }
    }
}
