using System;
using System.Collections.Generic;
using System.IO;
using Serilog;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Gestionnaire d'importation et de cache des clips audio
    /// Similaire à l'AssetDatabase pour les matériaux
    /// </summary>
    public static class AudioImporter
    {
        private static readonly Dictionary<Guid, IAudioClip> _loadedClips = new();
        private static readonly Dictionary<string, Guid> _pathToGuid = new();

        /// <summary>
        /// Charge ou récupère un clip audio depuis le cache
        /// </summary>
        /// <param name="filePath">Chemin du fichier audio</param>
        /// <param name="forceStreaming">Force le streaming même pour les fichiers courts</param>
        /// <param name="streamingThreshold">Durée en secondes au-delà de laquelle utiliser le streaming (défaut: 30s)</param>
        public static IAudioClip? LoadClip(string filePath, bool forceStreaming = false, float streamingThreshold = 30f)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            // Normaliser le chemin
            string normalizedPath = Path.GetFullPath(filePath);

            // Vérifier si déjà chargé
            if (_pathToGuid.TryGetValue(normalizedPath, out var guid))
            {
                if (_loadedClips.TryGetValue(guid, out var cachedClip))
                {
                    return cachedClip;
                }
            }

            // Déterminer si on doit streamer ou charger en mémoire
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            bool shouldStream = forceStreaming;

            // Pour MP3/OGG, on streame par défaut (compression)
            if (!shouldStream && (extension == ".mp3" || extension == ".ogg"))
            {
                shouldStream = true;
            }

            // Pour WAV, streamer seulement si > threshold (> 1MB)
            if (!shouldStream && extension == ".wav")
            {
                try
                {
                    var fileInfo = new FileInfo(normalizedPath);
                    if (fileInfo.Length > 1_000_000) // > 1MB
                    {
                        shouldStream = true;
                    }
                }
                catch { }
            }

            if (shouldStream)
            {
                // Utiliser le streaming pour les fichiers longs
                var streamingClip = StreamingAudioClip.LoadFromFile(normalizedPath);
                if (streamingClip != null)
                {
                    _loadedClips[streamingClip.Guid] = streamingClip;
                    _pathToGuid[normalizedPath] = streamingClip.Guid;
                    Log.Information($"[AudioImporter] Imported (STREAMING): {Path.GetFileName(filePath)}");
                }
                return streamingClip;
            }
            else
            {
                // Charger le clip en mémoire
                var audioClip = AudioClip.LoadFromFile(normalizedPath);
                if (audioClip != null)
                {
                    _loadedClips[audioClip.Guid] = audioClip;
                    _pathToGuid[normalizedPath] = audioClip.Guid;
                    Log.Information($"[AudioImporter] Imported: {Path.GetFileName(filePath)}");
                }
                return audioClip;
            }
        }

        /// <summary>
        /// Charge un clip en streaming (pour musique/fichiers longs)
        /// </summary>
        public static IAudioClip? LoadStreamingClip(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            string normalizedPath = Path.GetFullPath(filePath);
            var streamingClip = StreamingAudioClip.LoadFromFile(normalizedPath);

            if (streamingClip != null)
            {
                _loadedClips[streamingClip.Guid] = streamingClip;
                _pathToGuid[normalizedPath] = streamingClip.Guid;
            }

            return streamingClip;
        }

        /// <summary>
        /// Récupère un clip par son GUID
        /// </summary>
        public static IAudioClip? GetClip(Guid guid)
        {
            _loadedClips.TryGetValue(guid, out var clip);
            return clip;
        }

        /// <summary>
        /// Récupère tous les clips chargés
        /// </summary>
        public static IEnumerable<IAudioClip> GetAllClips()
        {
            return _loadedClips.Values;
        }

        /// <summary>
        /// Décharge un clip de la mémoire
        /// </summary>
        public static void UnloadClip(Guid guid)
        {
            if (_loadedClips.TryGetValue(guid, out var clip))
            {
                // Trouver et supprimer le mapping path -> guid
                foreach (var kvp in _pathToGuid)
                {
                    if (kvp.Value == guid)
                    {
                        _pathToGuid.Remove(kvp.Key);
                        break;
                    }
                }

                clip.Dispose();
                _loadedClips.Remove(guid);
                Log.Information($"[AudioImporter] Unloaded: {clip.Name}");
            }
        }

        /// <summary>
        /// Décharge tous les clips
        /// </summary>
        public static void UnloadAll()
        {
            foreach (var clip in _loadedClips.Values)
            {
                clip.Dispose();
            }

            _loadedClips.Clear();
            _pathToGuid.Clear();
            Log.Information("[AudioImporter] Unloaded all clips");
        }

        /// <summary>
        /// Scanne un répertoire pour les fichiers audio
        /// </summary>
        public static List<string> ScanDirectory(string directory, bool recursive = true)
        {
            var audioFiles = new List<string>();

            if (!Directory.Exists(directory))
            {
                Log.Warning($"[AudioImporter] Directory not found: {directory}");
                return audioFiles;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Extensions supportées
            string[] extensions = { "*.wav", "*.ogg", "*.mp3" };

            foreach (var extension in extensions)
            {
                audioFiles.AddRange(Directory.GetFiles(directory, extension, searchOption));
            }

            return audioFiles;
        }

        /// <summary>
        /// Pré-charge tous les clips audio d'un répertoire
        /// </summary>
        public static void PreloadDirectory(string directory, bool recursive = true)
        {
            var files = ScanDirectory(directory, recursive);

            Log.Information($"[AudioImporter] Preloading {files.Count} audio files from {directory}");

            int successCount = 0;
            foreach (var file in files)
            {
                if (LoadClip(file) != null)
                    successCount++;
            }

            Log.Information($"[AudioImporter] Successfully preloaded {successCount}/{files.Count} clips");
        }
    }
}
