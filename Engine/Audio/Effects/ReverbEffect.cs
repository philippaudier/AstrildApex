namespace Engine.Audio.Effects
{
    [System.Obsolete("Legacy per-source effects removed; use EFX-based filters/mixer")]
    public sealed class ReverbEffect : AudioEffect
    {
        public enum ReverbPreset
        {
            Generic,
            Room,
            LivingRoom,
            Hall,
            Cathedral,
            Cave,
            Arena,
            Hangar,
            Underwater
        }

        public ReverbEffect() : base("Reverb (deprecated)") { }
        public override void Create() { }
        public override void Apply(int sourceId) { }
        public override void UpdateParameters() { }
    }
}
