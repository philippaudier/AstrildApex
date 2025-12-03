using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Assets
{
    /// <summary>
    /// Clip audio avec streaming pour les fichiers longs (musique)
    /// Charge les données par petits buffers au lieu de tout charger en mémoire
    /// </summary>
    public sealed class StreamingAudioClip : IAudioClip
    {
        public Guid Guid { get; private set; }
        public string Name { get; set; }
        public string FilePath { get; private set; }

        /// <summary>Buffers OpenAL pour le streaming (3-4 buffers rotatifs)</summary>
        private readonly int[] _buffers;
        // CRITICAL: Increased from 6 to 12 buffers to handle slow decoder initialization on first load
        // 12 buffers × 32768 samples ÷ 44100 Hz = ~8.9 seconds of audio buffer
        // This gives the streaming thread enough time to start refilling before underrun
        private const int BufferCount = 12;
        private const int BufferSizeInSamples = 32768; // larger buffer for more headroom

        /// <summary>Durée totale en secondes</summary>
        public float Length { get; private set; }

        /// <summary>Fréquence d'échantillonnage</summary>
        public int Frequency { get; private set; }

        /// <summary>Format audio</summary>
        public ALFormat Format { get; private set; }

        /// <summary>Nombre de canaux</summary>
        public int Channels { get; private set; }

        /// <summary>Taille totale estimée en octets</summary>
        public int SizeInBytes { get; private set; }

        public bool IsLoaded { get; private set; }

        /// <summary>Les StreamingAudioClip utilisent toujours le streaming</summary>
        public bool IsStreaming => true;

        /// <summary>Les clips streaming n'ont pas de BufferId unique (utilisent plusieurs buffers rotatifs)</summary>
        public int BufferId => -1;

        /// <summary>Décodeur audio pour la session de streaming active (créé par StartStreaming)</summary>
        private IAudioDecoder? _activeDecoder;

        private Thread? _streamingThread;
        private bool _stopStreaming;
        private bool _isPaused;
        private int _sourceId = -1;
        private bool _loopRequested = false;
        // Request produced by streaming thread when it needs the main thread to
        // perform a playback restart. This avoids calling OpenAL from background
        // threads and centralizes AL.SourcePlay on the main thread.
        private volatile bool _playbackRestartRequested = false;
        
        /// <summary>Indique si le streaming est actuellement actif</summary>
        public bool IsStreamingActive => _streamingThread != null && _streamingThread.IsAlive;
        
        /// <summary>Position actuelle de lecture en secondes</summary>
        private float _currentTime = 0f;
        private int _totalSamplesRead = 0;
        
        /// <summary>Obtient le temps de lecture actuel</summary>
        public float CurrentTime => _currentTime;

        /// <summary>
        /// Indique si le thread de streaming a demandé au thread principal
        /// de relancer la lecture (AL.SourcePlay). L'appelant principal doit
        /// consommer la requête en appelant ClearPlaybackRestartRequest.
        /// </summary>
        public bool PlaybackRestartRequested => _playbackRestartRequested;

        public void ClearPlaybackRestartRequest()
        {
            _playbackRestartRequested = false;
        }
        
        /// <summary>Active ou désactive le mode loop pour le streaming</summary>
        public bool Loop
        {
            get => _loopRequested;
            set
            {
                _loopRequested = value;
                if (_activeDecoder != null)
                    _activeDecoder.IsLooping = value;
            }
        }

        private IAudioDecoder? CreateDecoderInstance()
        {
            string extension = Path.GetExtension(FilePath).ToLowerInvariant();
            try
            {
                if (extension == ".mp3") return new Mp3Decoder(FilePath);
                if (extension == ".ogg") return new OggDecoder(FilePath);
                if (extension == ".wav") return new WavDecoder(FilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[StreamingAudioClip] Failed to create decoder for {FilePath}");
            }
            return null;
        }

        private StreamingAudioClip(string filePath, string name)
        {
            // Use deterministic GUID based on file path to ensure same clip has same GUID across loads
            Guid = GenerateGuidFromPath(filePath);
            FilePath = filePath;
            Name = name;
            _buffers = new int[BufferCount];
        }

        /// <summary>
        /// Génère un GUID déterministe basé sur le chemin du fichier
        /// </summary>
        private static Guid GenerateGuidFromPath(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(filePath)));
            return new Guid(hash);
        }

        /// <summary>
        /// Charge un clip audio en streaming depuis un fichier
        /// </summary>
        public static StreamingAudioClip? LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Log.Error($"[StreamingAudioClip] File not found: {filePath}");
                return null;
            }

            var clip = new StreamingAudioClip(filePath, Path.GetFileNameWithoutExtension(filePath));

            try
            {
                string extension = Path.GetExtension(filePath).ToLowerInvariant();

                // Create a temporary decoder to read metadata, then dispose it.
                IAudioDecoder? tempDecoder = null;
                try
                {
                    if (extension == ".mp3") tempDecoder = new Mp3Decoder(filePath);
                    else if (extension == ".ogg") tempDecoder = new OggDecoder(filePath);
                    else if (extension == ".wav") tempDecoder = new WavDecoder(filePath);
                    else
                    {
                        Log.Error($"[StreamingAudioClip] Unsupported format: {extension}");
                        return null;
                    }

                    // Obtenir les métadonnées
                    clip.Frequency = tempDecoder.SampleRate;
                    clip.Channels = tempDecoder.Channels;
                    clip.Length = tempDecoder.TotalTime;
                }
                finally
                {
                    tempDecoder?.Dispose();
                }

                // Estimer la taille totale (approximatif pour le streaming)
                clip.SizeInBytes = (int)(clip.Length * clip.Frequency * clip.Channels * sizeof(short));

                // Déterminer le format OpenAL
                if (clip.Channels == 1)
                    clip.Format = ALFormat.Mono16;
                else if (clip.Channels == 2)
                    clip.Format = ALFormat.Stereo16;
                else
                    throw new Exception($"Unsupported channel count: {clip.Channels}");

                // Générer les buffers OpenAL
                AL.GenBuffers(BufferCount, clip._buffers);

                clip.IsLoaded = true;
                Log.Information($"[StreamingAudioClip] Loaded: {clip.Name} ({clip.Length:F2}s, {clip.Frequency}Hz, {clip.Channels}ch) - STREAMING");
                return clip;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[StreamingAudioClip] Failed to load: {filePath}");
                clip.Dispose();
                return null;
            }
        }

        /// <summary>
        /// Démarre le streaming vers une source OpenAL
        /// </summary>
        /// <summary>
        /// Starts streaming and returns the number of buffers initially queued (0 = failed)
        /// </summary>
        public int StartStreaming(int sourceId, bool resetPosition = true, bool startThread = true)
        {
            if (!IsLoaded)
            {
                Log.Warning("[StreamingAudioClip] Cannot start streaming - not loaded");
                return 0;
            }

            if (IsStreamingActive)
            {
                Log.Warning("[StreamingAudioClip] Already streaming");
                return 0;
            }
            // Create a fresh decoder instance for this streaming session so multiple
            // playbacks of the same clip do not interfere with each other.
            _activeDecoder = CreateDecoderInstance();
            if (_activeDecoder == null)
            {
                Log.Warning("[StreamingAudioClip] Failed to create decoder for streaming");
                return 0;
            }

            _activeDecoder.IsLooping = _loopRequested;

            _sourceId = sourceId;
            _stopStreaming = false;
            _isPaused = false;

            // CRITICAL FIX: Ensure the OpenAL source has NO queued buffers before starting
            // This prevents residual audio from previous playbacks causing a loop on the first 2 seconds
            try
            {
                AL.GetSource(sourceId, ALGetSourcei.BuffersQueued, out int queuedCount);
                if (queuedCount > 0)
                {
                    // Unqueue all buffers to ensure clean state
                    int[] bufferIds = new int[queuedCount];
                    AL.SourceUnqueueBuffers(sourceId, queuedCount, bufferIds);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[StreamingAudioClip] Failed to clear residual buffers from source");
            }

            if (resetPosition)
            {
                _currentTime = 0f;
                _totalSamplesRead = 0;
                _activeDecoder.Reset();
            }

            // Pré-remplir tous les buffers
            int filled = 0;
            for (int i = 0; i < BufferCount; i++)
            {
                if (FillBuffer(_buffers[i]))
                    filled++;
                else
                    break;
            }

            int minBuffers = Math.Min(6, BufferCount); // Need at least half the buffers
            // Try to ensure at least `minBuffers` are filled to reduce startup underruns
            int attempts = 0;
            while (filled < minBuffers && attempts < 10)
            {
                // attempt to fill remaining buffers
                if (FillBuffer(_buffers[filled]))
                    filled++;
                else
                    break;

                attempts++;
                if (filled >= minBuffers) break;
                Thread.Sleep(8);
            }

            if (filled == 0)
            {
                Log.Warning($"[StreamingAudioClip] No data available to queue for: {Name}");
                // Dispose decoder created for this session
                try { _activeDecoder?.Dispose(); } catch { }
                _activeDecoder = null;
                return 0;
            }

            // Attacher seulement les buffers remplis à la source
            int[] toQueue = new int[filled];
            Array.Copy(_buffers, toQueue, filled);
            AL.SourceQueueBuffers(sourceId, filled, toQueue);

            // Give the audio driver a moment to process queued buffers before play
            Thread.Sleep(10);

            // Optionally start the thread of streaming. Caller may choose to start it
            // after calling AL.SourcePlay to avoid a race where the thread sees the
            // source not yet playing and reports a false underrun.
            if (startThread)
            {
                _streamingThread = new Thread(StreamingThreadFunc)
                {
                    Name = $"AudioStreaming-{Name}",
                    IsBackground = true
                };
                _streamingThread.Start();
            }

            Log.Information($"[StreamingAudioClip] Started streaming: {Name} (queued {filled} buffers)");
            return filled;
        }

        /// <summary>
        /// Met en pause le streaming
        /// </summary>
        public void PauseStreaming()
        {
            if (!IsStreamingActive || _isPaused)
                return;

            _isPaused = true;
            
            if (_sourceId != -1)
            {
                AL.SourcePause(_sourceId);
            }
            
            Log.Debug($"[StreamingAudioClip] Paused streaming: {Name}");
        }

        /// <summary>
        /// Starts the internal streaming thread if not already running.
        /// This can be used to start the refill thread after the caller has
        /// called AL.SourcePlay to avoid a race that causes a false underrun.
        /// </summary>
        public void StartStreamingThread()
        {
            if (IsStreamingActive || _activeDecoder == null || _sourceId == -1)
                return;

            _stopStreaming = false;
            _isPaused = false;
            _streamingThread = new Thread(StreamingThreadFunc)
            {
                Name = $"AudioStreaming-{Name}",
                IsBackground = true
            };
            _streamingThread.Start();
        }

        /// <summary>
        /// Reprend le streaming après une pause
        /// </summary>
        public void ResumeStreaming()
        {
            if (!IsStreamingActive || !_isPaused)
                return;

            _isPaused = false;
            
            if (_sourceId != -1)
            {
                AL.SourcePlay(_sourceId);
            }
            
            Log.Debug($"[StreamingAudioClip] Resumed streaming: {Name}");
        }

        /// <summary>
        /// Arrête le streaming
        /// </summary>
        public void StopStreaming()
        {
            if (!IsStreamingActive)
                return;
            // Clear any pending requests from the streaming thread
            _playbackRestartRequested = false;
            _stopStreaming = true;
            _isPaused = false;
            _streamingThread?.Join(1000);
            _streamingThread = null;

            if (_sourceId != -1)
            {
                // Détacher tous les buffers
                AL.SourceStop(_sourceId);

                // CRITICAL FIX: After SourceStop, unqueue ALL queued buffers (both processed and unprocessed)
                // Previously we only unqueued processed buffers, but queued-but-not-yet-processed buffers
                // would remain and cause the "looping first 2 seconds" bug on next playback
                try
                {
                    AL.GetSource(_sourceId, ALGetSourcei.BuffersQueued, out int queued);
                    if (queued > 0)
                    {
                        // After AL.SourceStop, ALL queued buffers are marked as processed and can be unqueued
                        // Wait a bit to ensure OpenAL has processed the stop command
                        Thread.Sleep(5);

                        // Clamp to the number of allocated buffers to avoid InvalidValue
                        int toUnqueue = Math.Min(queued, _buffers.Length);
                        if (toUnqueue > 0)
                        {
                            int[] bufferIds = new int[toUnqueue];
                            AL.SourceUnqueueBuffers(_sourceId, toUnqueue, bufferIds);
                            var err = AL.GetError();
                            if (err != ALError.NoError)
                            {
                                Log.Warning($"[StreamingAudioClip] AL.Error after UnqueueBuffers: {err}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[StreamingAudioClip] Exception during UnqueueBuffers cleanup");
                }

                _sourceId = -1;
            }


            // Reset tracked playback time so UI shows 0%
            _currentTime = 0f;
            _totalSamplesRead = 0;

            // Dispose the decoder used by this streaming session
            try
            {
                _activeDecoder?.Dispose();
            }
            catch { }
            _activeDecoder = null;

            Log.Information($"[StreamingAudioClip] Stopped streaming: {Name}");
        }

        /// <summary>
        /// Thread de streaming : remplit les buffers vides en continu
        /// </summary>
        private void StreamingThreadFunc()
        {
            // CRITICAL FIX: Unity-style streaming pipeline
            // - Immediate underrun recovery (no waiting for 5 consecutive underruns)
            // - Batch buffer processing for efficiency
            // - Minimal logging to reduce thread blocking
            // - Proactive buffer refilling to prevent underruns

            bool firstIteration = true;
            int consecutiveUnderrunRecoveries = 0;

            // Give the main thread time to call AL.SourcePlay before we start checking state
            Thread.Sleep(20);

            while (!_stopStreaming && _activeDecoder != null)
            {
                if (_sourceId == -1)
                    break;

                // Si en pause, attendre
                if (_isPaused)
                {
                    Thread.Sleep(10);
                    continue;
                }

                // Vérifier combien de buffers ont été traités
                if (!AL.IsSource(_sourceId))
                {
                    Thread.Sleep(2);
                    continue;
                }

                AL.GetSource(_sourceId, ALGetSourcei.BuffersProcessed, out int processed);

                // OPTIMIZATION: Process all available buffers in batch instead of one-by-one
                if (processed > 0)
                {
                    // Clamp to prevent over-unqueueing
                    int toProcess = Math.Min(processed, BufferCount);
                    int[] processedBuffers = new int[toProcess];

                    try
                    {
                        // Unqueue all processed buffers at once
                        AL.SourceUnqueueBuffers(_sourceId, toProcess, processedBuffers);

                        // Refill and re-queue all buffers
                        for (int i = 0; i < toProcess; i++)
                        {
                            if (FillBuffer(processedBuffers[i]))
                            {
                                AL.SourceQueueBuffers(_sourceId, 1, new int[] { processedBuffers[i] });
                            }
                            else
                            {
                                // End of stream
                                if (_activeDecoder != null && _activeDecoder.IsLooping)
                                {
                                    _activeDecoder.Reset();
                                    _totalSamplesRead = 0;
                                    _currentTime = 0f;

                                    // Retry fill after loop reset
                                    if (FillBuffer(processedBuffers[i]))
                                    {
                                        AL.SourceQueueBuffers(_sourceId, 1, new int[] { processedBuffers[i] });
                                    }
                                    else
                                    {
                                        _stopStreaming = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    _stopStreaming = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"[StreamingAudioClip] Error processing buffers: {Name}");
                        Thread.Sleep(5);
                        continue;
                    }
                }

                // Check for underrun (source stopped playing but has buffers queued)
                if (!AL.IsSource(_sourceId))
                {
                    Thread.Sleep(2);
                    continue;
                }

                AL.GetSource(_sourceId, ALGetSourcei.SourceState, out int state);
                AL.GetSource(_sourceId, ALGetSourcei.BuffersQueued, out int queued);

                // CRITICAL FIX: IMMEDIATE underrun recovery (Unity-style)
                // Don't wait for multiple underruns - restart immediately when detected
                if (!firstIteration && state != (int)ALSourceState.Playing && !_stopStreaming && queued > 0)
                {
                    try
                    {
                        consecutiveUnderrunRecoveries++;

                        // Log only the first underrun
                        if (consecutiveUnderrunRecoveries == 1)
                        {
                            Log.Warning($"[StreamingAudioClip] Buffer underrun detected - recovering (queued={queued})");
                        }

                        // IMMEDIATE restart - don't wait for multiple underruns
                        _playbackRestartRequested = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[StreamingAudioClip] Exception during underrun recovery");
                    }
                }
                else if (state == (int)ALSourceState.Playing)
                {
                    // Reset counter when playing normally
                    if (consecutiveUnderrunRecoveries > 0)
                    {
                        consecutiveUnderrunRecoveries = 0;
                    }
                }

                firstIteration = false;

                // OPTIMIZATION: Very short sleep for responsive buffer refilling
                // Unity uses ~1ms audio tick rate for streaming
                Thread.Sleep(1);
            }

            Log.Debug($"[StreamingAudioClip] Streaming thread exiting: {Name}");
        }

        /// <summary>
        /// Remplit un buffer avec des données décodées
        /// </summary>
        private bool FillBuffer(int bufferId)
        {
            if (_activeDecoder == null)
                return false;

            short[] samples = new short[BufferSizeInSamples * Channels];
            int samplesRead = _activeDecoder.ReadSamples(samples, 0, samples.Length);

            if (samplesRead == 0)
            {
                // Reached EOF for this decoder session
                try
                {
                    // EOF info may be noisy during normal looping; keep as Debug
                    Log.Debug($"[StreamingAudioClip] FillBuffer: no samples read (EOF) - Name={Name}, CurrentTime={_currentTime:F3}, Looping={_activeDecoder?.IsLooping}");
                }
                catch { }
                return false;
            }
            
            // Mettre à jour le temps de lecture
            try
            {
                // Detailed per-buffer fill info is noisy; log at Debug level only
                Log.Debug($"[StreamingAudioClip] FillBuffer: buffer={bufferId}, samplesRead={samplesRead}, channels={Channels}, freq={Frequency}, currentTimeBefore={_currentTime:F3}");
            }
            catch { }

            _totalSamplesRead += samplesRead / Channels;
            _currentTime = (float)_totalSamplesRead / Frequency;

            // Charger les données dans le buffer OpenAL
            unsafe
            {
                fixed (short* ptr = samples)
                {
                    AL.BufferData(bufferId, Format, (IntPtr)ptr, samplesRead * sizeof(short), Frequency);
                }
            }

            return true;
        }

        public void Dispose()
        {
            StopStreaming();

            if (_buffers != null && _buffers.Length > 0 && _buffers[0] != 0)
            {
                AL.DeleteBuffers(_buffers);
                Array.Clear(_buffers, 0, _buffers.Length);
            }

            _activeDecoder?.Dispose();
            _activeDecoder = null;
            IsLoaded = false;
        }

        ~StreamingAudioClip()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Interface pour les décodeurs audio
    /// </summary>
    public interface IAudioDecoder : IDisposable
    {
        int SampleRate { get; }
        int Channels { get; }
        float TotalTime { get; }
        bool IsLooping { get; set; }

        /// <summary>Lit des samples PCM 16-bit</summary>
        int ReadSamples(short[] buffer, int offset, int count);

        /// <summary>Remet le décodeur au début</summary>
        void Reset();
    }
}
