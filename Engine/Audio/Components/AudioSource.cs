using System;
using Engine.Audio.Assets;
using Engine.Audio.Core;
using Engine.Inspector;
using Engine.Serialization;
using OpenTK.Audio.OpenAL;
using OpenTK.Mathematics;
using Serilog;

namespace Engine.Audio.Components
{
    /// <summary>
    /// Catégories de mixage audio (comme Unity)
    /// </summary>
    public enum AudioCategory
    {
        Master,
        Music,
        SFX,
        Voice,
        Ambient
    }

    /// <summary>
    /// Composant AudioSource - Joue des clips audio sur une entité
    /// API similaire à Unity AudioSource
    /// </summary>
    public sealed class AudioSource : Engine.Components.Component
    {
        private int _sourceId = -1;
        private IAudioClip? _clip;
        private StreamingAudioClip? _streamingClip;
        private bool _isPlaying;
        private bool _isPaused;
        private bool _deferredPlayOnAwake = false; // Flag to defer PlayOnAwake to Update()
        private readonly List<int> _effectSlots = new(); // OpenAL effect slots

        /// <summary>
        /// Per-source filters (Unity-like AudioLowPassFilter/AudioHighPassFilter)
        /// Stored in `Filters` (serializable below).
        /// </summary>

        // --- Propriétés sérialisables ---

        [Engine.Serialization.Serializable("clip")]
        public Guid? ClipGuid { get; set; }
        
        [Engine.Serialization.Serializable("filters")]
        public List<AudioSourceFilter>? Filters { get; set; } = new();

        [Engine.Serialization.Serializable("volume")]
        [Editable("Volume")]
        public float Volume
        {
            get => VolumeBacking;
            set
            {
                VolumeBacking = value;
                UpdateSourceProperties();
            }
        }
        private float VolumeBacking = 1.0f;

        [Engine.Serialization.Serializable("pitch")]
        [Editable("Pitch")]
        public float Pitch
        {
            get => PitchBacking;
            set
            {
                PitchBacking = value;
                UpdateSourceProperties();
            }
        }
        private float PitchBacking = 1.0f;

        [Engine.Serialization.Serializable("loop")]
        [Editable("Loop")]
        public bool Loop
        {
            get => LoopBacking;
            set
            {
                LoopBacking = value;
                // For streaming clips, ensure decoder loop flag updated
                if (_streamingClip != null)
                    _streamingClip.Loop = LoopBacking;
            }
        }
        private bool LoopBacking = false;

        [Engine.Serialization.Serializable("playOnAwake")]
        [Editable("Play On Awake")]
        public bool PlayOnAwake
        {
            get => PlayOnAwakeBacking;
            set
            {
                PlayOnAwakeBacking = value;
            }
        }
        private bool PlayOnAwakeBacking = false;

        [Engine.Serialization.Serializable("spatialBlend")]
        [Editable("Spatial Blend")]
        public float SpatialBlend
        {
            get => SpatialBlendBacking;
            set
            {
                SpatialBlendBacking = value;
                UpdateSourceProperties();
            }
        }
        private float SpatialBlendBacking = 1.0f; // 0 = 2D, 1 = 3D

        [Engine.Serialization.Serializable("minDistance")]
        [Editable("Min Distance")]
        public float MinDistance
        {
            get => MinDistanceBacking;
            set
            {
                MinDistanceBacking = value;
                UpdateSourceProperties();
            }
        }
        private float MinDistanceBacking = 1.0f;

        [Engine.Serialization.Serializable("maxDistance")]
        [Editable("Max Distance")]
        public float MaxDistance
        {
            get => MaxDistanceBacking;
            set
            {
                MaxDistanceBacking = value;
                UpdateSourceProperties();
            }
        }
        private float MaxDistanceBacking = 500.0f;

        [Engine.Serialization.Serializable("rolloffFactor")]
        [Editable("Rolloff Factor")]
        public float RolloffFactor
        {
            get => RolloffFactorBacking;
            set
            {
                RolloffFactorBacking = value;
                UpdateSourceProperties();
            }
        }
        private float RolloffFactorBacking = 1.0f;

        [Engine.Serialization.Serializable("category")]
        [Editable("Category")]
        public AudioCategory Category
        {
            get => CategoryBacking;
            set
            {
                CategoryBacking = value;
                // Re-apply mixer group effects when category changes
                ApplyMixerGroupEffects();
            }
        }
        private AudioCategory CategoryBacking = AudioCategory.SFX;

        [Engine.Serialization.Serializable("priority")]
        [Editable("Priority")]
        public int Priority
        {
            get => PriorityBacking;
            set
            {
                PriorityBacking = value;
            }
        }
        private int PriorityBacking = 128; // 0 = highest, 256 = lowest

        [Engine.Serialization.Serializable("dopplerLevel")]
        [Editable("Doppler Level")]
        public float DopplerLevel
        {
            get => DopplerLevelBacking;
            set
            {
                DopplerLevelBacking = value;
                UpdateSourceProperties();
            }
        }
        private float DopplerLevelBacking = 1.0f;

        [Engine.Serialization.Serializable("mute")]
        [Editable("Mute")]
        public bool Mute
        {
            get => MuteBacking;
            set
            {
                MuteBacking = value;
                UpdateSourceProperties();
            }
        }
        private bool MuteBacking = false;

        // --- Propriétés runtime ---

        public IAudioClip? Clip
        {
            get => _clip;
            set
            {
                _clip = value;
                _streamingClip = value as StreamingAudioClip;
                ClipGuid = value?.Guid;
                if (_sourceId != -1 && value != null && value.IsLoaded && !value.IsStreaming)
                {
                    // For non-streaming clips, assign the buffer directly
                    AL.Source(_sourceId, ALSourcei.Buffer, value.BufferId);
                }
                // Streaming clips are handled in Play() method
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (_sourceId == -1) return false;
                // Avoid calling OpenAL on an invalid source id
                try
                {
                    if (!AL.IsSource(_sourceId))
                    {
                        _sourceId = -1;
                        return false;
                    }

                    AL.GetSource(_sourceId, ALGetSourcei.SourceState, out int state);
                    return state == (int)ALSourceState.Playing;
                }
                catch
                {
                    _sourceId = -1;
                    return false;
                }
            }
        }

        public bool IsPaused => _isPaused;
        
        // Legacy per-source effects were removed in favor of the EFX filter/mixer system.

        /// <summary>
        /// Position de lecture actuelle en secondes
        /// </summary>
        public float Time
        {
            get
            {
                if (_sourceId == -1) return 0f;
                
                // Pour les clips streaming, utiliser le temps track\u00e9 par le d\u00e9codeur
                if (_streamingClip != null)
                {
                    return _streamingClip.CurrentTime;
                }
                
                // Pour les clips normaux, utiliser SecOffset d'OpenAL
                try
                {
                    if (!AL.IsSource(_sourceId))
                    {
                        _sourceId = -1;
                        return 0f;
                    }

                    AL.GetSource(_sourceId, ALSourcef.SecOffset, out float seconds);
                    return seconds;
                }
                catch
                {
                    _sourceId = -1;
                    return 0f;
                }
            }
        }

        public override void OnAttached()
        {
            base.OnAttached();

            // CRITICAL: Reset all cached values after cloning to ensure properties are applied
            ResetPropertyCaches();

            // Load clip from GUID if available but clip is null (happens after deserialization)
            if (_clip == null && ClipGuid.HasValue && ClipGuid.Value != Guid.Empty)
            {
                LoadClipFromGuid();
            }

            // CRITICAL FIX: If this is a StreamingAudioClip that's already streaming (happens during
            // scene cloning when Edit Mode sources share clips with Play Mode), force stop it to
            // ensure decoder is in clean state. This prevents the "looping first 2 seconds" bug.
            if (_clip is StreamingAudioClip streamingClip && streamingClip.IsStreamingActive)
            {
                try
                {
                    streamingClip.StopStreaming();
                    Log.Information($"[AudioSource] Force-stopped streaming clip during OnAttached: {_clip.Name}");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[AudioSource] Failed to stop streaming clip during OnAttached");
                }
            }

            if (!AudioEngine.Instance.IsInitialized)
            {
                Log.Warning("[AudioSource] AudioEngine not initialized");
                return;
            }

            // Générer une source OpenAL
            _sourceId = AL.GenSource();

            // CRITICAL FIX: Ensure the newly created OpenAL source is in a clean state
            // OpenAL can reuse source IDs and they might have residual queued buffers
            // This prevents the "looping first 2 seconds" bug on first play mode entry
            try
            {
                // Stop the source in case it was reused and still playing
                if (AL.IsSource(_sourceId))
                {
                    AL.SourceStop(_sourceId);

                    // Clear any queued buffers from previous usage
                    AL.GetSource(_sourceId, ALGetSourcei.BuffersQueued, out int queuedCount);
                    if (queuedCount > 0)
                    {
                        int[] bufferIds = new int[queuedCount];
                        AL.SourceUnqueueBuffers(_sourceId, queuedCount, bufferIds);
                        Log.Information($"[AudioSource] Cleared {queuedCount} residual buffers from reused OpenAL source on entity: {Entity?.Name}");
                    }

                    // Reset source to initial state
                    AL.Source(_sourceId, ALSourcef.SecOffset, 0f);
                    AL.Source(_sourceId, ALSourcei.Buffer, 0);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSource] Failed to clean newly created OpenAL source");
            }

            UpdateSourceProperties();
        }

        public override void OnEnable()
        {
            base.OnEnable();

            // Load clip from GUID if it's still null after OnAttached (safety fallback)
            if (_clip == null && ClipGuid.HasValue && ClipGuid.Value != Guid.Empty)
            {
                LoadClipFromGuid();
            }

            // CRITICAL: Defer PlayOnAwake to avoid freezing during scene initialization
            // Do not auto-play on enable when running in the Editor - PlayOnAwake
            // should only apply when actually running the game/player. The Editor
            // may load scenes while the user is browsing and should not trigger
            // immediate playback which can cause underruns or spurious restarts.
            if (PlayOnAwake && _clip != null && (!Engine.Core.RuntimeEnvironment.IsEditor || Engine.Core.RuntimeEnvironment.IsPlayMode))
            {
                // Defer playback to next Update() to avoid calling OpenAL during component initialization
                _deferredPlayOnAwake = true;
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            Stop();
        }
        
        public override void OnDestroy()
        {
            base.OnDestroy();

            Stop();

            // legacy per-source effects removed; no cleanup required here

            // Nettoyer tous les filtres
            ClearFilters();

            // S'assurer que la source est désenregistrée
            AudioEngine.Instance.UnregisterActiveSource(this);

            if (_sourceId != -1)
            {
                try
                {
                    if (AL.IsSource(_sourceId))
                    {
                        AL.DeleteSource(_sourceId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[AudioSource] Error deleting OpenAL source");
                }
                _sourceId = -1;
            }
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            // Handle deferred PlayOnAwake (called after scene initialization is complete)
            if (_deferredPlayOnAwake)
            {
                _deferredPlayOnAwake = false;
                try
                {
                    Play();
                    Log.Information($"[AudioSource] Deferred PlayOnAwake executed for entity: {Entity?.Name}");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[AudioSource] Deferred PlayOnAwake failed");
                }
            }

            // Try to initialize source if it wasn't created yet (e.g., AudioEngine was initialized after OnAttached)
            if (_sourceId == -1 && AudioEngine.Instance.IsInitialized)
            {
                _sourceId = AL.GenSource();
                UpdateSourceProperties();
                Log.Information($"[AudioSource] Late initialization of OpenAL source on entity: {Entity?.Name}");
            }

            if (_sourceId == -1 || Entity?.Transform == null) return;

            // Mettre à jour la position 3D
            if (SpatialBlend > 0f)
            {
                Entity.GetWorldTRS(out var pos, out var rot, out var scale);
                AL.Source(_sourceId, ALSource3f.Position, pos.X, pos.Y, pos.Z);

                // TODO: Calculer la vélocité pour l'effet Doppler
                // AL.Source(_sourceId, ALSource3f.Velocity, vel.X, vel.Y, vel.Z);
            }

            // Update reverb zones for moving 3D sources
            if (_isPlaying && SpatialBlend > 0.1f)
            {
                ReverbZoneExtensions.ApplyReverbZones(this, _sourceId);
            }

            // Vérifier si le son s'est arrêté
            if (_isPlaying && !IsPlaying && !_isPaused)
            {
                _isPlaying = false;
                AudioEngine.Instance.UnregisterActiveSource(this);
            }

            // If the streaming clip requested a playback restart (thread-safe flag),
            // perform the actual AL.SourcePlay on the main thread to avoid calling
            // OpenAL from background threads.
            try
            {
                if (_streamingClip != null && _streamingClip.PlaybackRestartRequested && AL.IsSource(_sourceId))
                {
                    AL.SourcePlay(_sourceId);
                    _streamingClip.ClearPlaybackRestartRequest();
                    Log.Information($"[AudioSource] Performed AL.SourcePlay on main thread for streaming clip: {_streamingClip.Name}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSource] Failed to perform playback restart on main thread");
            }
        }

        /// <summary>
        /// Joue le clip audio
        /// </summary>
        public void Play(bool resetPosition = true)
        {
            // Prevent multiple simultaneous Play() calls
            if (_isPlaying && !resetPosition)
            {
                Log.Debug($"[AudioSource] Already playing, ignoring redundant Play() call for: {Entity?.Name}");
                return;
            }

            // Ensure we have a valid OpenAL source. Try late-initialization if possible.
            if (_sourceId == -1)
            {
                if (!AudioEngine.Instance.IsInitialized)
                {
                    Log.Warning("[AudioSource] Cannot play - AudioEngine not initialized");
                    return;
                }

                try
                {
                    _sourceId = AL.GenSource();
                    // Reset caches after late initialization
                    ResetPropertyCaches();
                    UpdateSourceProperties();
                    Log.Information($"[AudioSource] Late initialization of OpenAL source for entity: {Entity?.Name}");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[AudioSource] Failed to generate OpenAL source during Play()");
                    return;
                }
            }

            if (_sourceId == -1 || _clip == null || !_clip.IsLoaded)
            {
                Log.Warning($"[AudioSource] Cannot play - sourceId={_sourceId}, clip={((_clip == null) ? "null" : (_clip.IsLoaded ? "loaded" : "not loaded"))} on entity: {Entity?.Name}");
                return;
            }

            // Capture local reference to satisfy nullable analysis and avoid repeated null checks
            var clip = _clip!;
            Log.Information($"[AudioSource] Play: entity={Entity?.Name}, clip={(clip?.Name ?? "null")}, streaming={(clip?.IsStreaming ?? false)}, resetPosition={resetPosition}");

            // Warn if spatialization requested but clip is not mono (OpenAL will not spatialize stereo sources)
            if (SpatialBlend > 0f && _clip != null && _clip.IsLoaded)
            {
                try
                {
                    int channels = -1;
                    if (_clip is AudioClip ac)
                        channels = ac.Channels;
                    else if (_streamingClip != null)
                        channels = _streamingClip.Channels;

                    if (channels > 1)
                    {
                        var clipName = clip?.Name ?? "<unknown>";
                        Log.Warning($"[AudioSource] Spatialization requested but clip '{clipName}' has {channels} channels. OpenAL spatialization requires mono sources.");
                    }
                }
                catch { }
            }

            // Stop if already playing to reset position
            if (IsPlaying)
            {
                AL.SourceStop(_sourceId);

                // CRITICAL FIX: For streaming clips, ensure all queued buffers are removed
                // when stopping before restarting. This prevents residual buffers from
                // causing the "looping first 2 seconds" bug.
                if (_streamingClip != null && _streamingClip.IsStreamingActive)
                {
                    _streamingClip.StopStreaming();
                }
                else
                {
                    // For non-streaming clips or if streaming is not active, manually clear any queued buffers
                    try
                    {
                        AL.GetSource(_sourceId, ALGetSourcei.BuffersQueued, out int queued);
                        if (queued > 0)
                        {
                            // After SourceStop, all buffers can be unqueued
                            int[] bufferIds = new int[queued];
                            AL.SourceUnqueueBuffers(_sourceId, queued, bufferIds);
                            Log.Information($"[AudioSource] Cleared {queued} residual buffers before Play()");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[AudioSource] Failed to clear residual buffers before Play()");
                    }
                }
            }

            // Reset playback position to beginning only if requested
            if (resetPosition)
            {
                AL.Source(_sourceId, ALSourcef.SecOffset, 0f);
            }

            // Appliquer les propriétés
            UpdateSourceProperties();

            // Apply mixer group effects (if any)
            ApplyMixerGroupEffects();

            // Apply per-source filters (if any)
            ApplySourceFilters();

            // Apply reverb zones based on 3D position (if spatial)
            if (SpatialBlend > 0.1f)
            {
                ReverbZoneExtensions.ApplyReverbZones(this, _sourceId);
            }

            // Handle streaming vs non-streaming clips differently
            if (clip!.IsStreaming && _streamingClip != null)
            {
                // Stop any existing streaming before starting new one
                if (_streamingClip.IsStreamingActive)
                {
                    Log.Warning($"[AudioSource] Stopping existing stream before starting new one for: {clip!.Name}");
                    _streamingClip.StopStreaming();
                }

                // For streaming clips, set loop on decoder and start the streaming thread
                _streamingClip.Loop = Loop;
                int queued = _streamingClip.StartStreaming(_sourceId, resetPosition, startThread: false);
                if (queued > 0)
                {
                    AL.SourcePlay(_sourceId);
                    // Start the streaming thread after initiating playback to avoid a
                    // race where the thread sees the source not yet playing and logs
                    // an underrun immediately.
                    _streamingClip.StartStreamingThread();
                    Log.Information($"[AudioSource] Started streaming playback: {clip!.Name}");
                }
                else
                {
                    Log.Warning($"[AudioSource] Streaming failed to start: {clip!.Name}");
                }
            }
            else
            {
                // For non-streaming clips, attach the buffer and play
                AL.Source(_sourceId, ALSourcei.Buffer, clip!.BufferId);
                AL.SourcePlay(_sourceId);
            }

            _isPlaying = true;
            _isPaused = false;

            AudioEngine.Instance.RegisterActiveSource(this);
        }

        /// <summary>
        /// Joue un clip audio one-shot (sans boucle)
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volumeScale = 1.0f)
        {
            if (_sourceId == -1 || clip == null || !clip.IsLoaded)
                return;

            // Créer une source temporaire pour le one-shot
            int tempSource = AL.GenSource();
            AL.Source(tempSource, ALSourcei.Buffer, clip.BufferId);
            AL.Source(tempSource, ALSourcef.Gain, Volume * volumeScale * GetCategoryVolume());
            AL.Source(tempSource, ALSourcef.Pitch, Pitch);
            AL.Source(tempSource, ALSourceb.Looping, false);

            if (SpatialBlend > 0f && Entity?.Transform != null)
            {
                Entity.GetWorldTRS(out var pos, out _, out _);
                AL.Source(tempSource, ALSource3f.Position, pos.X, pos.Y, pos.Z);
                AL.Source(tempSource, ALSourcef.ReferenceDistance, MinDistance);
                AL.Source(tempSource, ALSourcef.MaxDistance, MaxDistance);
            }
            else
            {
                AL.Source(tempSource, ALSourceb.SourceRelative, true);
            }

            AL.SourcePlay(tempSource);

            // TODO: Gérer le nettoyage des sources temporaires
        }

        /// <summary>
        /// Met en pause la lecture
        /// </summary>
        public void Pause()
        {
            if (_sourceId == -1 || !_isPlaying) return;

            // Pause streaming if applicable
            if (_streamingClip != null)
            {
                _streamingClip.PauseStreaming();
            }
            else
            {
                AL.SourcePause(_sourceId);
            }
            
            _isPaused = true;
        }

        /// <summary>
        /// Reprend la lecture après une pause
        /// </summary>
        public void UnPause()
        {
            if (_sourceId == -1 || !_isPaused) return;

            // Resume streaming if applicable
            if (_streamingClip != null)
            {
                _streamingClip.ResumeStreaming();
            }
            else
            {
                AL.SourcePlay(_sourceId);
            }
            
            _isPaused = false;
        }

        /// <summary>
        /// Arrête la lecture
        /// </summary>
        public void Stop()
        {
            if (_sourceId == -1) return;
            if (!AL.IsSource(_sourceId))
            {
                _sourceId = -1;
                return;
            }
            try
            {
                Log.Information($"[AudioSource] Stop: entity={Entity?.Name}, sourceId={_sourceId}\nCaller:\n{Environment.StackTrace}");
            }
            catch
            {
                Log.Information($"[AudioSource] Stop: entity={Entity?.Name}, sourceId={_sourceId}");
            }
            
            // Vérifier que la source est toujours valide avant d'interagir avec
            if (!AL.IsSource(_sourceId))
            {
                _sourceId = -1;
                _isPlaying = false;
                _isPaused = false;
                return;
            }

            AL.SourceStop(_sourceId);
            var err = AL.GetError();
            if (err != ALError.NoError)
            {
                Log.Warning($"[AudioSource] AL.Error after SourceStop: {err}");
            }

            // Reset playback position to beginning
            AL.Source(_sourceId, ALSourcef.SecOffset, 0f);
            err = AL.GetError();
            if (err != ALError.NoError)
            {
                Log.Warning($"[AudioSource] AL.Error after Set SecOffset: {err}");
            }

            // Stop streaming if it's a streaming clip
            if (_streamingClip != null)
            {
                _streamingClip.StopStreaming();

                // Ensure OpenAL source is rewound and has no queued buffers to avoid
                // weird resumed-state when restarting streaming playback.
                    try
                    {
                        if (AL.IsSource(_sourceId))
                        {
                            AL.SourceRewind(_sourceId);
                            var err2 = AL.GetError();
                            if (err2 != ALError.NoError)
                                Log.Warning($"[AudioSource] AL.Error after SourceRewind: {err2}");

                            AL.Source(_sourceId, ALSourcei.Buffer, 0);
                            err2 = AL.GetError();
                            if (err2 != ALError.NoError)
                                Log.Warning($"[AudioSource] AL.Error after unassign Buffer: {err2}");

                            AL.Source(_sourceId, ALSourcef.SecOffset, 0f);
                            err2 = AL.GetError();
                            if (err2 != ALError.NoError)
                                Log.Warning($"[AudioSource] AL.Error after resetting offset: {err2}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[AudioSource] Exception during Stop cleanup");
                    }
            }

            _isPlaying = false;
            _isPaused = false;

            AudioEngine.Instance.UnregisterActiveSource(this);
        }

        /// <summary>
        /// Met à jour toutes les propriétés de la source OpenAL
        /// </summary>
        private void UpdateSourceProperties()
        {
            RefreshProperties();
        }
        
        // Cached values to avoid unnecessary OpenAL calls
        private float _lastGain = -1f;
        private float _lastPitch = -1f;
        private bool _lastLoop = false;
        private float _lastRolloff = -1f;
        private bool _lastSpatialMode = false;

        /// <summary>
        /// Rafraîchit les propriétés de la source OpenAL
        /// Now uses dirty checking to avoid redundant OpenAL calls
        /// </summary>
        public void RefreshProperties()
        {
            if (_sourceId == -1) return;

            // Vérifier que la source est toujours valide
            if (!AL.IsSource(_sourceId))
            {
                Log.Warning($"[AudioSource] Source {_sourceId} is no longer valid on entity: {Entity?.Name}");
                _sourceId = -1;
                return;
            }

            try
            {
                float effectiveVolume = Mute ? 0f : Volume * GetCategoryVolume();

                // Only update if changed (or if cache was reset with -1)
                if (_lastGain < 0f || Math.Abs(effectiveVolume - _lastGain) > 0.001f)
                {
                    AL.Source(_sourceId, ALSourcef.Gain, effectiveVolume);
                    _lastGain = effectiveVolume;
                }

                float clampedPitch = Math.Clamp(Pitch, 0.5f, 2.0f);
                // CRITICAL: Also update if _lastPitch is negative (cache was reset)
                if (_lastPitch < 0f || Math.Abs(clampedPitch - _lastPitch) > 0.001f)
                {
                    AL.Source(_sourceId, ALSourcef.Pitch, clampedPitch);
                    _lastPitch = clampedPitch;
                }

                // For streaming clips, set loop on decoder; for non-streaming clips, set on OpenAL source
                if (_streamingClip != null)
                {
                    if (_streamingClip.Loop != Loop)
                    {
                        _streamingClip.Loop = Loop;
                        _lastLoop = Loop;
                    }
                }
                else
                {
                    if (_lastLoop != Loop)
                    {
                        AL.Source(_sourceId, ALSourceb.Looping, Loop);
                        _lastLoop = Loop;
                    }
                }

                if (Math.Abs(RolloffFactor - _lastRolloff) > 0.001f)
                {
                    AL.Source(_sourceId, ALSourcef.RolloffFactor, RolloffFactor);
                    _lastRolloff = RolloffFactor;
                }

                // Configuration spatiale
                bool isSpatial = SpatialBlend > 0f;
                if (isSpatial)
                {
                    if (!_lastSpatialMode)
                    {
                        AL.Source(_sourceId, ALSourceb.SourceRelative, false);
                        _lastSpatialMode = true;
                    }

                    AL.Source(_sourceId, ALSourcef.ReferenceDistance, MinDistance);
                    AL.Source(_sourceId, ALSourcef.MaxDistance, MaxDistance);

                    if (Entity?.Transform != null)
                    {
                        Entity.GetWorldTRS(out var pos, out _, out _);
                        AL.Source(_sourceId, ALSource3f.Position, pos.X, pos.Y, pos.Z);
                    }
                }
                else
                {
                    if (_lastSpatialMode)
                    {
                        // Son 2D - position relative au listener
                        AL.Source(_sourceId, ALSourceb.SourceRelative, true);
                        AL.Source(_sourceId, ALSource3f.Position, 0f, 0f, 0f);
                        _lastSpatialMode = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSource] Error in RefreshProperties");
                _sourceId = -1;
            }
        }

        /// <summary>
        /// Obtient le volume de la catégorie
        /// </summary>
        private float GetCategoryVolume()
        {
            return AudioEngine.Instance.GetCategoryVolume(Category);
        }
        
        // Legacy AddEffect/RemoveEffect/ClearEffects removed.

        /// <summary>
        /// Applique les effets du groupe de mixage à cette source
        /// </summary>
        internal void ApplyMixerGroupEffects()
        {
            if (_sourceId == -1 || !Engine.Audio.Effects.AudioEfxBackend.Instance.IsEFXSupported) return;
            try
            {
                // Detach any previously attached direct filter and auxiliary sends
                // to ensure removed effects do not persist on the OpenAL source.
                var backend = Engine.Audio.Effects.AudioEfxBackend.Instance;
                // Detach direct filter (if any)
                backend.DetachDirectFilterFromSource(_sourceId);
                // Detach all auxiliary sends up to device max
                for (int i = 0; i < Engine.Audio.Effects.EFXManager.MaxAuxiliarySends; i++)
                {
                    backend.DetachAuxSlotFromSource(_sourceId, i);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSource] Failed while detaching previous EFX attachments");
            }

            try
            {
                // Reset flag tracking whether mixer attached a direct filter
                _mixerAttachedDirectFilter = false;
                // Get mixer group for this source's category
                var mixer = AudioEngine.Instance.Mixer;
                if (mixer == null) return;

                var groupName = Category.ToString();
                var mixerGroup = mixer.GetGroup(groupName);
                if (mixerGroup != null)
                {
                    // Apply mixer group effects to this source.
                    // Walk up the parent chain so parent-group effects (e.g., Master) also apply.
                    int sendIndex = 0;
                    var group = mixerGroup;
                    while (group != null)
                    {
                        try
                        {
                            var (used, directAttached) = group.ApplyEffectsToSource(_sourceId, sendIndex);
                            sendIndex += used;
                            if (directAttached)
                                _mixerAttachedDirectFilter = true;
                            group = group.Parent;
                        }
                        catch { break; }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSource] Failed to apply mixer group effects");
            }
        }

        // Tracks whether the mixer chain attached a direct filter to this source
        private bool _mixerAttachedDirectFilter = false;

        /// <summary>
        /// Applique les filtres directs de cette source
        /// </summary>
        private void ApplySourceFilters()
        {
            if (_sourceId == -1 || !Engine.Audio.Effects.AudioEfxBackend.Instance.IsEFXSupported) return;

            try
            {
                // Find the first enabled filter and apply it
                // Note: OpenAL only supports one direct filter per source
                // If multiple filters are needed, they should be combined or prioritized
                var activeFilter = Filters?.FirstOrDefault(f => f.Enabled);
                if (activeFilter != null && activeFilter.FilterHandle.IsValid)
                {
                    Engine.Audio.Effects.AudioEfxBackend.Instance.AttachDirectFilterToSource(_sourceId, activeFilter.FilterHandle);
                }
                else
                {
                    // No per-source filter active. Only detach the direct filter
                    // if the mixer did NOT attach one (to avoid removing mixer filters).
                    if (!_mixerAttachedDirectFilter)
                    {
                        Engine.Audio.Effects.AudioEfxBackend.Instance.DetachDirectFilterFromSource(_sourceId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AudioSource] Failed to apply source filters");
            }
        }

        /// <summary>
        /// Nettoie les filtres de cette source
        /// </summary>
        private void ClearFilters()
        {
            if (Filters == null) return;
            foreach (var filter in Filters.ToArray())
            {
                AudioSourceFilterExtensions.DestroyFilterHandle(filter);
            }
            Filters.Clear();
        }

        /// <summary>
        /// Loads the audio clip from ClipGuid if it's set
        /// This is called after deserialization to restore the clip reference
        /// </summary>
        private void LoadClipFromGuid()
        {
            if (!ClipGuid.HasValue || ClipGuid.Value == Guid.Empty)
                return;

            try
            {
                // Try to get the clip from AudioImporter cache
                var clip = Engine.Audio.Assets.AudioImporter.GetClipByGuid(ClipGuid.Value);
                if (clip != null)
                {
                    _clip = clip;
                    _streamingClip = clip as StreamingAudioClip;
                    Log.Information($"[AudioSource] Loaded clip from GUID: {clip.Name} on entity: {Entity?.Name}");
                }
                else
                {
                    // Try to load from AssetDatabase if it's registered
                    if (Engine.Assets.AssetDatabase.TryGet(ClipGuid.Value, out var record))
                    {
                        clip = Engine.Audio.Assets.AudioImporter.LoadClip(record.Path);
                        if (clip != null)
                        {
                            _clip = clip;
                            _streamingClip = clip as StreamingAudioClip;
                            Log.Information($"[AudioSource] Loaded clip from AssetDatabase: {clip.Name} on entity: {Entity?.Name}");
                        }
                        else
                        {
                            Log.Warning($"[AudioSource] Failed to load clip from path: {record.Path}");
                        }
                    }
                    else
                    {
                        Log.Warning($"[AudioSource] Clip GUID not found in AudioImporter or AssetDatabase: {ClipGuid.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"[AudioSource] Failed to load clip from GUID: {ClipGuid?.ToString() ?? "null"}");
            }
        }

        /// <summary>
        /// Resets all cached property values to force OpenAL updates on next refresh
        /// CRITICAL: Must be called after scene cloning to ensure all properties are applied
        /// </summary>
        private void ResetPropertyCaches()
        {
            _lastGain = -1f;
            _lastPitch = -1f;
            _lastLoop = false;
            _lastRolloff = -1f;
            _lastSpatialMode = false;
        }
    }
}
