using System;
using System.IO;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Représente un clip audio chargé en mémoire
    /// Similaire à Unity AudioClip
    /// </summary>
    public sealed class AudioClip : IAudioClip
    {
        public Guid Guid { get; private set; }
        public string Name { get; set; }
        public string FilePath { get; private set; }

        /// <summary>Buffer OpenAL contenant les données audio</summary>
        public int BufferId { get; private set; }

        /// <summary>Durée du clip en secondes</summary>
        public float Length { get; private set; }

        /// <summary>Fréquence d'échantillonnage (ex: 44100 Hz)</summary>
        public int Frequency { get; private set; }

        /// <summary>Format audio (Mono16, Stereo16, etc.)</summary>
        public ALFormat Format { get; private set; }

        /// <summary>Nombre de canaux (1=mono, 2=stéréo)</summary>
        public int Channels { get; private set; }

        /// <summary>Taille des données en octets</summary>
        public int SizeInBytes { get; private set; }

        public bool IsLoaded { get; private set; }

        /// <summary>Les AudioClip en mémoire ne sont pas streamés</summary>
        public bool IsStreaming => false;

        private AudioClip(string filePath, string name)
        {
            Guid = Guid.NewGuid();
            FilePath = filePath;
            Name = name;
            BufferId = -1;
        }

        /// <summary>
        /// Charge un clip audio depuis un fichier WAV
        /// </summary>
        public static AudioClip? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Log.Error($"[AudioClip] File not found: {filePath}");
                return null;
            }

            var clip = new AudioClip(filePath, Path.GetFileNameWithoutExtension(filePath));

            try
            {
                // Générer un buffer OpenAL
                clip.BufferId = AL.GenBuffer();

                // Charger les données audio selon le format
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".wav")
                {
                    clip.LoadWav(filePath);
                }
                else if (extension == ".ogg")
                {
                    // TODO: Support OGG Vorbis avec NVorbis
                    Log.Warning($"[AudioClip] OGG format not yet supported: {filePath}");
                    return null;
                }
                else if (extension == ".mp3")
                {
                    // TODO: Support MP3 avec NLayer
                    Log.Warning($"[AudioClip] MP3 format not yet supported: {filePath}");
                    return null;
                }
                else
                {
                    Log.Error($"[AudioClip] Unsupported audio format: {extension}");
                    return null;
                }

                clip.IsLoaded = true;
                Log.Information($"[AudioClip] Loaded: {clip.Name} ({clip.Length:F2}s, {clip.Frequency}Hz, {clip.Channels}ch)");
                return clip;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[AudioClip] Failed to load: {filePath}");
                clip.Dispose();
                return null;
            }
        }

        /// <summary>
        /// Charge un fichier WAV (format RIFF/WAVE PCM)
        /// </summary>
        private void LoadWav(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // Lire l'en-tête RIFF
            string signature = new string(reader.ReadChars(4));
            if (signature != "RIFF")
                throw new Exception("Invalid WAV file - missing RIFF signature");

            reader.ReadInt32(); // ChunkSize
            string format = new string(reader.ReadChars(4));
            if (format != "WAVE")
                throw new Exception("Invalid WAV file - missing WAVE format");

            // Lire le chunk fmt
            string fmtSignature = new string(reader.ReadChars(4));
            if (fmtSignature != "fmt ")
                throw new Exception("Invalid WAV file - missing fmt chunk");

            int fmtChunkSize = reader.ReadInt32();
            int audioFormat = reader.ReadInt16(); // 1 = PCM
            int channels = reader.ReadInt16();
            int sampleRate = reader.ReadInt32();
            int byteRate = reader.ReadInt32();
            int blockAlign = reader.ReadInt16();
            int bitsPerSample = reader.ReadInt16();

            if (audioFormat != 1)
                throw new Exception($"Unsupported WAV format: {audioFormat} (only PCM is supported)");

            // Skip extra fmt bytes if any
            if (fmtChunkSize > 16)
                reader.ReadBytes(fmtChunkSize - 16);

            // Trouver le chunk data
            string dataSignature;
            int dataSize;
            while (true)
            {
                dataSignature = new string(reader.ReadChars(4));
                dataSize = reader.ReadInt32();

                if (dataSignature == "data")
                    break;

                // Skip unknown chunks
                reader.ReadBytes(dataSize);
            }

            // Lire les données audio
            byte[] audioData = reader.ReadBytes(dataSize);

            // Déterminer le format OpenAL
            ALFormat alFormat;
            if (channels == 1 && bitsPerSample == 8)
                alFormat = ALFormat.Mono8;
            else if (channels == 1 && bitsPerSample == 16)
                alFormat = ALFormat.Mono16;
            else if (channels == 2 && bitsPerSample == 8)
                alFormat = ALFormat.Stereo8;
            else if (channels == 2 && bitsPerSample == 16)
                alFormat = ALFormat.Stereo16;
            else
                throw new Exception($"Unsupported WAV format: {channels}ch, {bitsPerSample}bit");

            // Charger dans le buffer OpenAL
            AL.BufferData(BufferId, alFormat, audioData, sampleRate);

            // Vérifier les erreurs
            var error = AL.GetError();
            if (error != ALError.NoError)
                throw new Exception($"OpenAL error loading buffer: {error}");

            // Stocker les métadonnées
            Format = alFormat;
            Frequency = sampleRate;
            Channels = channels;
            SizeInBytes = audioData.Length;
            Length = (float)audioData.Length / (sampleRate * channels * (bitsPerSample / 8));
        }

        public void Dispose()
        {
            if (BufferId != -1)
            {
                AL.DeleteBuffer(BufferId);
                BufferId = -1;
            }
            IsLoaded = false;
        }

        ~AudioClip()
        {
            Dispose();
        }
    }
}
