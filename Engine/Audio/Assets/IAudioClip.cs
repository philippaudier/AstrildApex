using System;
using OpenTK.Audio.OpenAL;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Interface commune pour tous les types de clips audio (streaming ou en mémoire)
    /// Permet à AudioSource de gérer uniformément les deux types
    /// </summary>
    public interface IAudioClip : IDisposable
    {
        Guid Guid { get; }
        string Name { get; set; }
        string FilePath { get; }

        /// <summary>Durée du clip en secondes</summary>
        float Length { get; }

        /// <summary>Fréquence d'échantillonnage (ex: 44100 Hz)</summary>
        int Frequency { get; }

        /// <summary>Format audio (Mono16, Stereo16, etc.)</summary>
        ALFormat Format { get; }

        /// <summary>Nombre de canaux (1=mono, 2=stéréo)</summary>
        int Channels { get; }

        /// <summary>Taille des données en octets</summary>
        int SizeInBytes { get; }

        bool IsLoaded { get; }

        /// <summary>Indique si ce clip utilise le streaming</summary>
        bool IsStreaming { get; }

        /// <summary>
        /// Pour les clips non-streaming : retourne l'ID du buffer OpenAL
        /// Pour les clips streaming : retourne -1 (gestion des buffers différente)
        /// </summary>
        int BufferId { get; }
    }
}
