namespace Engine.Audio.Effects
{
    [System.Obsolete("Legacy per-source effects removed; use EFX-based filters/mixer")]
    public sealed class DistortionEffect : AudioEffect
    {
        public DistortionEffect() : base("Distortion (deprecated)") { }
        public override void Create() { }
        public override void Apply(int sourceId) { }
        public override void UpdateParameters() { }
    }
}
