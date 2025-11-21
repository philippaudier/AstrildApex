using System;
using System.Collections.Generic;
using Engine.Audio.Effects;
using Engine.Inspector;
using Engine.Serialization;
using OpenTK.Mathematics;
using Serilog;

namespace Engine.Audio.Components
{
    /// <summary>
    /// Reverb Zone Component - Unity-like 3D reverb zone
    ///
    /// Creates a spatial region where reverb is applied to 3D audio sources.
    /// Sources within the zone will have reverb applied based on their distance:
    /// - Inside InnerRadius: Full reverb (blend = 1.0)
    /// - Between InnerRadius and OuterRadius: Interpolated reverb (blend = 0.0 to 1.0)
    /// - Outside OuterRadius: No reverb (blend = 0.0)
    ///
    /// Usage:
    /// 1. Add this component to an entity in your scene
    /// 2. Set InnerRadius and OuterRadius to define the reverb zone
    /// 3. Configure ReverbPreset or manually adjust reverb parameters
    /// 4. AudioSource components with SpatialBlend > 0 will automatically receive reverb
    /// </summary>
    public sealed class ReverbZoneComponent : Engine.Components.Component
    {
        // Static list of all active reverb zones (for efficient lookup)
        private static readonly List<ReverbZoneComponent> _activeZones = new();

        // --- Serializable Properties ---

        [Engine.Serialization.Serializable("innerRadius")]
        [Editable("Inner Radius")]
        public float InnerRadius { get; set; } = 10.0f;

        [Engine.Serialization.Serializable("outerRadius")]
        [Editable("Outer Radius")]
        public float OuterRadius { get; set; } = 20.0f;

        [Engine.Serialization.Serializable("preset")]
        [Editable("Reverb Preset")]
        public ReverbPreset Preset { get; set; } = ReverbPreset.Generic;

        [Engine.Serialization.Serializable("enabled")]
        [Editable("Enabled")]
        public new bool Enabled { get; set; } = true;

        // --- Runtime Properties ---

        private ReverbSettings _reverbSettings;
        private EfxEffectHandle _effectHandle;
        private EfxAuxSlotHandle _auxSlotHandle;
        private bool _efxInitialized = false;

        /// <summary>
        /// Reverb presets (matches ReverbEffect.ReverbPreset)
        /// </summary>
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

        /// <summary>
        /// Current reverb settings
        /// </summary>
        public ReverbSettings ReverbSettings => _reverbSettings;

        /// <summary>
        /// EFX auxiliary slot handle (for attaching to sources)
        /// </summary>
        internal EfxAuxSlotHandle AuxSlotHandle => _auxSlotHandle;

        public ReverbZoneComponent()
        {
            _reverbSettings = ReverbSettings.GenericPreset();
        }

        public override void OnAttached()
        {
            base.OnAttached();

            // Initialize EFX objects
            InitializeEFX();

            // Register this zone globally
            if (!_activeZones.Contains(this))
            {
                _activeZones.Add(this);
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();

            // Ensure EFX is initialized
            if (!_efxInitialized)
            {
                InitializeEFX();
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            // Keep EFX objects alive but the zone won't affect sources
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            // Cleanup EFX objects
            CleanupEFX();

            // Unregister this zone globally
            _activeZones.Remove(this);
        }

        /// <summary>
        /// Initialize OpenAL EFX effect and auxiliary slot
        /// </summary>
        private void InitializeEFX()
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported)
            {
                Log.Warning("[ReverbZone] EFX not supported - reverb zone will be inactive");
                return;
            }

            if (_efxInitialized) return;

            try
            {
                // Apply preset to settings
                ApplyPreset(Preset);

                // Create reverb effect
                _effectHandle = AudioEfxBackend.Instance.CreateReverbEffect(_reverbSettings);

                // Create auxiliary effect slot
                _auxSlotHandle = AudioEfxBackend.Instance.CreateAuxSlot(_effectHandle);

                _efxInitialized = true;
                Log.Information($"[ReverbZone] Initialized on entity '{Entity?.Name}' with preset {Preset}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ReverbZone] Failed to initialize EFX");
            }
        }

        /// <summary>
        /// Cleanup OpenAL EFX objects
        /// </summary>
        private void CleanupEFX()
        {
            try
            {
                if (_auxSlotHandle.IsValid)
                {
                    AudioEfxBackend.Instance.DestroyAuxSlot(_auxSlotHandle);
                    _auxSlotHandle = EfxAuxSlotHandle.Invalid;
                }

                if (_effectHandle.IsValid)
                {
                    AudioEfxBackend.Instance.DestroyEffect(_effectHandle);
                    _effectHandle = EfxEffectHandle.Invalid;
                }

                _efxInitialized = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ReverbZone] Failed to cleanup EFX");
            }
        }

        /// <summary>
        /// Apply a reverb preset to the settings
        /// </summary>
        private void ApplyPreset(ReverbPreset preset)
        {
            _reverbSettings = preset switch
            {
                ReverbPreset.Room => ReverbSettings.RoomPreset(),
                ReverbPreset.Cathedral => ReverbSettings.CathedralPreset(),
                ReverbPreset.Cave => ReverbSettings.CavePreset(),
                ReverbPreset.LivingRoom => new ReverbSettings
                {
                    DecayTime = 0.5f,
                    LateReverbGain = 0.8f,
                    Density = 1.0f
                },
                ReverbPreset.Hall => new ReverbSettings
                {
                    DecayTime = 3.0f,
                    LateReverbGain = 1.5f,
                    ReflectionsDelay = 0.02f
                },
                ReverbPreset.Arena => new ReverbSettings
                {
                    DecayTime = 5.0f,
                    LateReverbGain = 2.5f,
                    ReflectionsDelay = 0.05f
                },
                ReverbPreset.Hangar => new ReverbSettings
                {
                    DecayTime = 4.0f,
                    LateReverbGain = 2.0f,
                    ReflectionsGain = 0.1f
                },
                ReverbPreset.Underwater => new ReverbSettings
                {
                    Density = 1.0f,
                    Diffusion = 0.7f,
                    Gain = 0.2f,
                    DecayTime = 1.5f,
                    LateReverbGain = 1.2f
                },
                _ => ReverbSettings.GenericPreset()
            };

            // Update EFX effect with new settings
            if (_effectHandle.IsValid)
            {
                AudioEfxBackend.Instance.UpdateReverbEffect(_effectHandle, _reverbSettings);
            }
        }

        /// <summary>
        /// Update reverb preset (called from inspector)
        /// </summary>
        public void SetPreset(ReverbPreset preset)
        {
            Preset = preset;
            ApplyPreset(preset);
        }

        /// <summary>
        /// Update reverb settings manually
        /// </summary>
        public void UpdateReverbSettings(ReverbSettings settings)
        {
            _reverbSettings = settings;
            if (_effectHandle.IsValid)
            {
                AudioEfxBackend.Instance.UpdateReverbEffect(_effectHandle, _reverbSettings);
            }
        }

        /// <summary>
        /// Calculate reverb blend factor for a given position (0 = no reverb, 1 = full reverb)
        /// </summary>
        public float CalculateBlendFactor(Vector3 position)
        {
            if (!Enabled || Entity?.Transform == null)
                return 0f;

            Entity.GetWorldTRS(out var zonePosition, out _, out _);
            float distance = (position - zonePosition).Length;

            if (distance <= InnerRadius)
            {
                // Inside inner radius: full reverb
                return 1.0f;
            }
            else if (distance >= OuterRadius)
            {
                // Outside outer radius: no reverb
                return 0f;
            }
            else
            {
                // Between inner and outer: linear interpolation
                float blend = 1.0f - (distance - InnerRadius) / (OuterRadius - InnerRadius);
                return Math.Clamp(blend, 0f, 1f);
            }
        }

        /// <summary>
        /// Get all active reverb zones in the scene
        /// </summary>
        public static IReadOnlyList<ReverbZoneComponent> GetActiveZones()
        {
            return _activeZones;
        }

        /// <summary>
        /// Find the dominant reverb zone for a given position
        /// Returns the zone with the highest blend factor, or null if no zones affect this position
        /// </summary>
        public static ReverbZoneComponent? FindDominantZone(Vector3 position, out float blendFactor)
        {
            ReverbZoneComponent? dominantZone = null;
            float maxBlend = 0f;

            foreach (var zone in _activeZones)
            {
                if (!zone.Enabled) continue;

                float blend = zone.CalculateBlendFactor(position);
                if (blend > maxBlend)
                {
                    maxBlend = blend;
                    dominantZone = zone;
                }
            }

            blendFactor = maxBlend;
            return dominantZone;
        }

        /// <summary>
        /// Apply this reverb zone to an audio source
        /// </summary>
        internal void ApplyToSource(int sourceId, int sendIndex, float blendFactor)
        {
            if (!_efxInitialized || !_auxSlotHandle.IsValid || sourceId <= 0)
                return;

            try
            {
                // Modulate the aux slot gain based on blend factor
                AudioEfxBackend.Instance.UpdateAuxSlotGain(_auxSlotHandle, blendFactor);

                // Attach aux slot to source
                AudioEfxBackend.Instance.AttachAuxSlotToSource(sourceId, _auxSlotHandle, sendIndex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[ReverbZone] Failed to apply to source {sourceId}");
            }
        }
    }

    /// <summary>
    /// Extension methods for integrating ReverbZones with AudioSource
    /// </summary>
    public static class ReverbZoneExtensions
    {
        /// <summary>
        /// Apply reverb zones to an audio source based on its 3D position
        /// Should be called from AudioSource.Play() or Update()
        /// </summary>
        public static void ApplyReverbZones(this AudioSource source, int sourceId)
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported) return;
            if (sourceId <= 0 || source.Entity?.Transform == null) return;

            // Only apply to 3D sources
            if (source.SpatialBlend < 0.1f) return;

            try
            {
                // Get source world position
                source.Entity.GetWorldTRS(out var position, out _, out _);

                // Find dominant reverb zone
                var zone = ReverbZoneComponent.FindDominantZone(position, out float blendFactor);

                if (zone != null && blendFactor > 0.01f)
                {
                    // Apply zone to source
                    // Use send index 2 for reverb zones (0-1 reserved for mixer group effects)
                    zone.ApplyToSource(sourceId, sendIndex: 2, blendFactor);
                }
                else
                {
                    // No zone active, detach reverb
                    AudioEfxBackend.Instance.DetachAuxSlotFromSource(sourceId, sendIndex: 2);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ReverbZone] Failed to apply zones to source");
            }
        }
    }
}
