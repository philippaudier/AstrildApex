using Serilog;

namespace Engine.Audio.Filters
{
    /// <summary>
    /// Filtre passe-bas - Atténue les hautes fréquences
    /// Utile pour simuler l'étouffement du son (sous l'eau, à travers un mur)
    /// </summary>
    public sealed class LowpassFilter : AudioFilter
    {
        private float _gain = 1.0f;
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
        /// Gain des hautes fréquences (0.0 - 1.0)
        /// 0.0 = toutes les hautes fréquences coupées
        /// 1.0 = aucune atténuation
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

        public LowpassFilter() : base("Lowpass")
        {
        }

        public override void Create()
        {
            if (!Effects.EFXManager.IsEFXSupported)
            {
                Log.Warning("[LowpassFilter] EFX not supported");
                return;
            }

            // TODO: Implémenter avec OpenAL EFX
            // _filterId = AL.GenFilter();
            // AL.Filter(_filterId, FilterInteger.FilterType, (int)FilterType.Lowpass);

            UpdateParameters();
            Log.Information("[LowpassFilter] Created");
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
            // AL.Filter(_filterId, FilterFloat.LowpassGain, _gain);
            // AL.Filter(_filterId, FilterFloat.LowpassGainHF, _gainHF);
        }
    }
}
