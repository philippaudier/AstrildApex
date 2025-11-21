using System;
using System.Collections.Generic;
using OpenTK.Audio.OpenAL;
using Serilog;

namespace Engine.Audio.Core
{
    /// <summary>
    /// Moteur audio principal - Singleton
    /// Gère l'initialisation OpenAL, les contextes et la mise à jour globale
    /// </summary>
    public sealed class AudioEngine : IDisposable
    {
        private static AudioEngine? _instance;
        public static AudioEngine Instance => _instance ??= new AudioEngine();

        private ALDevice _device;
        private ALContext _context;
        private bool _initialized;
        private readonly List<Components.AudioSource> _activeSources = new();

        public AudioSettings Settings { get; private set; }
        public Mixing.AudioMixer? Mixer { get; set; }
        private bool _masterMuted = false;
        public bool IsInitialized => _initialized;

        private AudioEngine()
        {
            Settings = new AudioSettings();
        }

        /// <summary>
        /// Initialise le moteur audio OpenAL
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Log.Warning("[AudioEngine] Already initialized");
                return;
            }

            try
            {
                // Ouvrir le device audio par défaut
                _device = ALC.OpenDevice(null);
                if (_device == ALDevice.Null)
                {
                    throw new Exception("Failed to open OpenAL device");
                }

                // Créer le contexte audio
                _context = ALC.CreateContext(_device, (int[])null!);
                if (_context == ALContext.Null)
                {
                    throw new Exception("Failed to create OpenAL context");
                }

                // Activer le contexte
                if (!ALC.MakeContextCurrent(_context))
                {
                    throw new Exception("Failed to make context current");
                }

                // Configurer le listener par défaut
                AL.Listener(ALListener3f.Position, 0f, 0f, 0f);
                AL.Listener(ALListener3f.Velocity, 0f, 0f, 0f);
                float[] orientation = { 0f, 0f, -1f, 0f, 1f, 0f }; // Forward + Up
                unsafe
                {
                    fixed (float* ptr = orientation)
                    {
                        AL.Listener(ALListenerfv.Orientation, ptr);
                    }
                }

                // Configuration globale
                AL.DistanceModel(ALDistanceModel.InverseDistanceClamped);

                _initialized = true;
                Log.Information("[AudioEngine] Initialized successfully");
                Log.Information($"[AudioEngine] Device: {ALC.GetString(_device, AlcGetString.DeviceSpecifier)}");

                // Log detailed OpenAL information
                OpenALVersionChecker.LogOpenALInfo();

                // Initialiser le système d'effets EFX
                Effects.EFXManager.Initialize();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[AudioEngine] Failed to initialize");
                _initialized = false;
                throw;
            }
        }

        /// <summary>
        /// Mise à jour du moteur audio (appelé chaque frame)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_initialized) return;

            // Nettoyer les sources arrêtées
            _activeSources.RemoveAll(s => !s.IsPlaying);

            // Note: RefreshProperties() is now only called when properties actually change
            // Not every frame to avoid audio glitches

            // Vérifier les erreurs OpenAL
            var error = AL.GetError();
            if (error != ALError.NoError)
            {
                Log.Warning($"[AudioEngine] OpenAL Error: {error}");
            }
        }
        
        /// <summary>
        /// Obtient le niveau audio actuel d'une catégorie (0.0 à 1.0) pour les VU meters
        /// </summary>
        public float GetCategoryLevel(Components.AudioCategory category)
        {
            if (!_initialized) return 0f;
            
            float maxLevel = 0f;
            foreach (var source in _activeSources)
            {
                if (source.Category == category && source.IsPlaying)
                {
                    // Le niveau est approximé par le volume de la source
                    float level = source.Volume * GetCategoryVolume(category);
                    if (level > maxLevel)
                        maxLevel = level;
                }
            }
            
            return Math.Clamp(maxLevel, 0f, 1f);
        }

        /// <summary>
        /// Enregistre une source audio active
        /// </summary>
        internal void RegisterActiveSource(Components.AudioSource source)
        {
            if (!_activeSources.Contains(source))
            {
                _activeSources.Add(source);
            }
        }

        /// <summary>
        /// Re-apply mixer group effects to all currently active sources that belong to the named group
        /// </summary>
        public void ReapplyMixerGroupEffects(string groupName)
        {
            if (!_initialized) return;

            foreach (var src in _activeSources)
            {
                try
                {
                    if (src.Category.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        src.ApplyMixerGroupEffects();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Désenregistre une source audio
        /// </summary>
        internal void UnregisterActiveSource(Components.AudioSource source)
        {
            _activeSources.Remove(source);
        }

        /// <summary>
        /// Configure le listener global (position, orientation, vélocité)
        /// </summary>
        public void SetListenerPosition(OpenTK.Mathematics.Vector3 position)
        {
            if (!_initialized) return;
            AL.Listener(ALListener3f.Position, position.X, position.Y, position.Z);
        }

        public void SetListenerVelocity(OpenTK.Mathematics.Vector3 velocity)
        {
            if (!_initialized) return;
            AL.Listener(ALListener3f.Velocity, velocity.X, velocity.Y, velocity.Z);
        }

        public void SetListenerOrientation(OpenTK.Mathematics.Vector3 forward, OpenTK.Mathematics.Vector3 up)
        {
            if (!_initialized) return;
            float[] orientation = { forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z };
            unsafe
            {
                fixed (float* ptr = orientation)
                {
                    AL.Listener(ALListenerfv.Orientation, ptr);
                }
            }
        }

        /// <summary>
        /// Volume principal (0.0 à 1.0)
        /// </summary>
        public float MasterVolume
        {
            get => Settings.MasterVolume;
            set
            {
                // Allow master gain > 1.0 (up to 10x) for boosting output
                Settings.MasterVolume = Math.Clamp(value, 0f, 10f);
                if (!_masterMuted)
                {
                    AL.Listener(ALListenerf.Gain, Settings.MasterVolume);
                }
            }
        }

        public bool MasterMuted
        {
            get => _masterMuted;
            set
            {
                _masterMuted = value;
                if (_masterMuted)
                {
                    AL.Listener(ALListenerf.Gain, 0f);
                }
                else
                {
                    AL.Listener(ALListenerf.Gain, Settings.MasterVolume);
                }
            }
        }
        
        /// <summary>
        /// Obtient le volume d'une catégorie audio (Music, SFX, Voice, Ambient)
        /// </summary>
        public float GetCategoryVolume(Components.AudioCategory category)
        {
            float categoryVol = 1.0f;
            
            // Si on a un mixer, utiliser ses valeurs
            if (Mixer != null)
            {
                var groupName = category.ToString();
                categoryVol = Mixer.GetEffectiveGroupVolume(groupName);
            }
            else
            {
                // Sinon utiliser les valeurs dans Settings
                categoryVol = category switch
                {
                    Components.AudioCategory.Music => Settings.MusicVolume,
                    Components.AudioCategory.SFX => Settings.SFXVolume,
                    Components.AudioCategory.Voice => Settings.VoiceVolume,
                    Components.AudioCategory.Ambient => Settings.MusicVolume, // Utiliser Music par défaut pour Ambient
                    _ => 1.0f
                };
            }
            
            // Ne PAS multiplier par MasterVolume ici car c'est géré par OpenAL Listener Gain
            return categoryVol;
        }

        /// <summary>
        /// Modèle de distance pour l'atténuation 3D
        /// </summary>
        public ALDistanceModel DistanceModel
        {
            get => Settings.DistanceModel;
            set
            {
                Settings.DistanceModel = value;
                AL.DistanceModel(value);
            }
        }

        /// <summary>
        /// Facteur Doppler (effet de pitch basé sur la vélocité)
        /// </summary>
        public float DopplerFactor
        {
            get => Settings.DopplerFactor;
            set
            {
                Settings.DopplerFactor = Math.Clamp(value, 0f, 10f);
                AL.DopplerFactor(Settings.DopplerFactor);
            }
        }

        /// <summary>
        /// Vitesse du son (pour l'effet Doppler)
        /// </summary>
        public float SpeedOfSound
        {
            get => Settings.SpeedOfSound;
            set
            {
                Settings.SpeedOfSound = Math.Max(0.0001f, value);
                AL.SpeedOfSound(Settings.SpeedOfSound);
            }
        }

        /// <summary>
        /// Met en pause toutes les sources audio
        /// </summary>
        public void PauseAll()
        {
            foreach (var source in _activeSources)
            {
                source.Pause();
            }
        }

        /// <summary>
        /// Reprend toutes les sources audio
        /// </summary>
        public void ResumeAll()
        {
            foreach (var source in _activeSources)
            {
                source.UnPause();
            }
        }

        /// <summary>
        /// Arrête toutes les sources audio
        /// </summary>
        public void StopAll()
        {
            foreach (var source in _activeSources)
            {
                source.Stop();
            }
            _activeSources.Clear();
        }

        public void Dispose()
        {
            if (!_initialized) return;

            StopAll();

            if (_context != ALContext.Null)
            {
                ALC.MakeContextCurrent(ALContext.Null);
                ALC.DestroyContext(_context);
                _context = ALContext.Null;
            }

            if (_device != ALDevice.Null)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null;
            }

            _initialized = false;
            Log.Information("[AudioEngine] Disposed");
        }

        ~AudioEngine()
        {
            Dispose();
        }
    }
}
