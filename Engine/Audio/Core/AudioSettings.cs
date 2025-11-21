using OpenTK.Audio.OpenAL;

namespace Engine.Audio.Core
{
    /// <summary>
    /// Configuration globale du moteur audio
    /// </summary>
    public sealed class AudioSettings
    {
        public float MasterVolume { get; set; } = 1.0f;
        public float MusicVolume { get; set; } = 0.8f;
        public float SFXVolume { get; set; } = 1.0f;
        public float VoiceVolume { get; set; } = 1.0f;

        public ALDistanceModel DistanceModel { get; set; } = ALDistanceModel.InverseDistanceClamped;

        /// <summary>
        /// Facteur Doppler (0 = désactivé, 1 = normal, >1 = exagéré)
        /// </summary>
        public float DopplerFactor { get; set; } = 1.0f;

        /// <summary>
        /// Vitesse du son en unités par seconde (343.3 m/s par défaut)
        /// </summary>
        public float SpeedOfSound { get; set; } = 343.3f;

        /// <summary>
        /// Nombre maximum de sources audio simultanées
        /// </summary>
        public int MaxAudioSources { get; set; } = 64;

        /// <summary>
        /// Active/désactive l'audio 3D spatial
        /// </summary>
        public bool Enable3DAudio { get; set; } = true;
    }
}
