using Serilog;

namespace Engine.Audio.Effects
{
    /// <summary>
    /// Effet Chorus - Duplique le son avec de légères variations de pitch/timing
    /// </summary>
    [System.Obsolete("Legacy per-source effects removed; use EFX-based filters/mixer")] 
    public sealed class ChorusEffect : AudioEffect
    {
        private ChorusWaveform _waveform = ChorusWaveform.Triangle;
        private int _phase = 90;
        private float _rate = 1.1f;
        private float _depth = 0.1f;
        private float _feedback = 0.25f;
        private float _delay = 0.016f;

        public enum ChorusWaveform
        {
            Sinusoid = 0,
            Triangle = 1
        }

        public ChorusWaveform Waveform
        {
            get => _waveform;
            set
            {
                _waveform = value;
                UpdateParameters();
            }
        }

        public int Phase
        {
            get => _phase;
            set
            {
                _phase = System.Math.Clamp(value, -180, 180);
                UpdateParameters();
            }
        }

        public float Rate
        {
            get => _rate;
            set
            {
                _rate = System.Math.Clamp(value, 0f, 10f);
                UpdateParameters();
            }
        }

        public float Depth
        {
            get => _depth;
            set
            {
                _depth = System.Math.Clamp(value, 0f, 1f);
                UpdateParameters();
            }
        }

        public float Feedback
        {
            get => _feedback;
            set
            {
                _feedback = System.Math.Clamp(value, -1f, 1f);
                UpdateParameters();
            }
        }

        public float Delay
        {
            get => _delay;
            set
            {
                _delay = System.Math.Clamp(value, 0f, 0.016f);
                UpdateParameters();
            }
        }

        public ChorusEffect() : base("Chorus (deprecated)") { }

        public override void Create()
        {
            // No-op: legacy effect kept for compatibility only
        }

        public override void Apply(int sourceId)
        {
            // No-op for deprecated effect
        }

        public override void UpdateParameters() { }
    }
}
