using Serilog;

namespace Engine.Audio.Filters
{
    /// <summary>
    /// Filtre passe-haut - Atténue les basses fréquences
    /// Utile pour simuler l'effet "radio" ou "téléphone"
    /// </summary>
    public sealed class HighpassFilter : AudioFilter
    {
        private float _gain = 1.0f;
        private float _gainLF = 1.0f;

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
        /// 0.0 = toutes les basses fréquences coupées
        /// 1.0 = aucune atténuation
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

        public HighpassFilter() : base("Highpass")
        {
        }

        public override void Create()
        {
            if (!Effects.EFXManager.IsEFXSupported)
            {
                Log.Warning("[HighpassFilter] EFX not supported");
                return;
            }

            // TODO: Implémenter avec OpenAL EFX
            // _filterId = AL.GenFilter();
            // AL.Filter(_filterId, FilterInteger.FilterType, (int)FilterType.Highpass);

            UpdateParameters();
            Log.Information("[HighpassFilter] Created");
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
            // AL.Filter(_filterId, FilterFloat.HighpassGain, _gain);
            // AL.Filter(_filterId, FilterFloat.HighpassGainLF, _gainLF);
        }
    }
}
