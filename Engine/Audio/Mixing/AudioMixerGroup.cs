using System;
using System.Collections.Generic;
using Engine.Audio.Effects;

namespace Engine.Audio.Mixing
{
    /// <summary>
    /// Type d'effet de groupe audio
    /// </summary>
    public enum AudioMixerGroupEffectType
    {
        None,
        Reverb,
        Echo,
        LowPass,
        HighPass
    }

    /// <summary>
    /// Effet appliqué à un groupe de mixage
    /// </summary>
    public class AudioMixerGroupEffect
    {
        public AudioMixerGroupEffectType Type;
        public bool Enabled;
        public object? Settings; // ReverbSettings, EchoSettings, LowPassSettings, etc.

        // EFX handles (internal)
        internal EfxEffectHandle EffectHandle;
        internal EfxFilterHandle FilterHandle;
        internal EfxAuxSlotHandle AuxSlotHandle;

        public AudioMixerGroupEffect(AudioMixerGroupEffectType type)
        {
            Type = type;
            Enabled = true;
            Settings = CreateDefaultSettings(type);
        }

        private static object? CreateDefaultSettings(AudioMixerGroupEffectType type)
        {
            return type switch
            {
                AudioMixerGroupEffectType.Reverb => ReverbSettings.GenericPreset(),
                AudioMixerGroupEffectType.Echo => new EchoSettings(),
                AudioMixerGroupEffectType.LowPass => new LowPassSettings(),
                AudioMixerGroupEffectType.HighPass => new HighPassSettings(),
                _ => null
            };
        }
    }

    /// <summary>
    /// Groupe de mixage audio - permet de contrôler le volume de plusieurs sources
    /// Similaire à Unity AudioMixerGroup
    ///
    /// Extended with EFX support:
    /// - Can have bus-level effects (reverb, echo, filters)
    /// - Effects are applied to all sources routed to this group
    /// - Uses OpenAL auxiliary effect slots for efficient bus processing
    /// </summary>
    public sealed class AudioMixerGroup : IDisposable
    {
        public Guid Guid { get; private set; }
        public string Name { get; set; }

        private float _volume = 1.0f;
        private bool _mute = false;
        private bool _solo = false;

        public AudioMixerGroup? Parent { get; set; }

        /// <summary>
        /// Effets appliqués à ce groupe (bus effects)
        /// </summary>
        public List<AudioMixerGroupEffect> Effects { get; private set; } = new();

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public bool Mute
        {
            get => _mute;
            set => _mute = value;
        }

        public bool Solo
        {
            get => _solo;
            set => _solo = value;
        }

        /// <summary>
        /// Volume effectif (incluant le parent)
        /// </summary>
        public float EffectiveVolume
        {
            get
            {
                if (_mute) return 0f;

                float volume = _volume;
                if (Parent != null)
                    volume *= Parent.EffectiveVolume;

                return volume;
            }
        }

        public AudioMixerGroup(string name)
        {
            Guid = Guid.NewGuid();
            Name = name;
        }

        /// <summary>
        /// Ajoute un effet au groupe
        /// </summary>
        public AudioMixerGroupEffect AddEffect(AudioMixerGroupEffectType type)
        {
            var effect = new AudioMixerGroupEffect(type);
            Effects.Add(effect);
            CreateEffectHandles(effect);
            // Reapply effects to active sources in this group
            try
            {
                Engine.Audio.Core.AudioEngine.Instance.ReapplyMixerGroupEffects(Name);
            }
            catch { }
            return effect;
        }

        /// <summary>
        /// Retire un effet du groupe
        /// </summary>
        public void RemoveEffect(AudioMixerGroupEffect effect)
        {
            if (Effects.Remove(effect))
            {
                DestroyEffectHandles(effect);
                try
                {
                    Engine.Audio.Core.AudioEngine.Instance.ReapplyMixerGroupEffects(Name);
                }
                catch { }
            }
        }

        /// <summary>
        /// Crée les handles EFX pour un effet
        /// </summary>
        private void CreateEffectHandles(AudioMixerGroupEffect effect)
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported) return;

            try
            {
                switch (effect.Type)
                {
                    case AudioMixerGroupEffectType.Reverb:
                        if (effect.Settings is ReverbSettings reverbSettings)
                        {
                            effect.EffectHandle = AudioEfxBackend.Instance.CreateReverbEffect(reverbSettings);
                            effect.AuxSlotHandle = AudioEfxBackend.Instance.CreateAuxSlot(effect.EffectHandle);
                        }
                        break;

                    case AudioMixerGroupEffectType.Echo:
                        if (effect.Settings is EchoSettings echoSettings)
                        {
                            effect.EffectHandle = AudioEfxBackend.Instance.CreateEchoEffect(echoSettings);
                            effect.AuxSlotHandle = AudioEfxBackend.Instance.CreateAuxSlot(effect.EffectHandle);
                        }
                        break;

                    case AudioMixerGroupEffectType.LowPass:
                        if (effect.Settings is LowPassSettings lowPassSettings)
                        {
                            effect.FilterHandle = AudioEfxBackend.Instance.CreateLowPassFilter(lowPassSettings);
                        }
                        break;

                    case AudioMixerGroupEffectType.HighPass:
                        if (effect.Settings is HighPassSettings highPassSettings)
                        {
                            effect.FilterHandle = AudioEfxBackend.Instance.CreateHighPassFilter(highPassSettings);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AudioMixerGroup] Failed to create effect handles for {effect.Type}");
            }
        }

        /// <summary>
        /// Détruit les handles EFX d'un effet
        /// </summary>
        private void DestroyEffectHandles(AudioMixerGroupEffect effect)
        {
            try
            {
                if (effect.EffectHandle.IsValid)
                {
                    AudioEfxBackend.Instance.DestroyEffect(effect.EffectHandle);
                    effect.EffectHandle = EfxEffectHandle.Invalid;
                }

                if (effect.FilterHandle.IsValid)
                {
                    AudioEfxBackend.Instance.DestroyFilter(effect.FilterHandle);
                    effect.FilterHandle = EfxFilterHandle.Invalid;
                }

                if (effect.AuxSlotHandle.IsValid)
                {
                    AudioEfxBackend.Instance.DestroyAuxSlot(effect.AuxSlotHandle);
                    effect.AuxSlotHandle = EfxAuxSlotHandle.Invalid;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AudioMixerGroup] Failed to destroy effect handles");
            }
        }

        /// <summary>
        /// Met à jour les paramètres d'un effet existant
        /// </summary>
        public void UpdateEffect(AudioMixerGroupEffect effect)
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported) return;

            try
            {
                switch (effect.Type)
                {
                    case AudioMixerGroupEffectType.Reverb:
                        if (effect.Settings is ReverbSettings reverbSettings && effect.EffectHandle.IsValid)
                        {
                            AudioEfxBackend.Instance.UpdateReverbEffect(effect.EffectHandle, reverbSettings);
                        }
                        break;

                    case AudioMixerGroupEffectType.LowPass:
                        if (effect.Settings is LowPassSettings lowPassSettings && effect.FilterHandle.IsValid)
                        {
                            AudioEfxBackend.Instance.UpdateLowPassFilter(effect.FilterHandle, lowPassSettings);
                        }
                        break;

                    case AudioMixerGroupEffectType.HighPass:
                        if (effect.Settings is HighPassSettings highPassSettings && effect.FilterHandle.IsValid)
                        {
                            AudioEfxBackend.Instance.UpdateHighPassFilter(effect.FilterHandle, highPassSettings);
                        }
                        break;
                }
                // Reapply to active sources after updating effect parameters
                try
                {
                    Engine.Audio.Core.AudioEngine.Instance.ReapplyMixerGroupEffects(Name);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AudioMixerGroup] Failed to update effect {effect.Type}");
            }
        }

        /// <summary>
        /// Applique les effets du groupe à une source audio donnée
        /// </summary>
        internal (int sendsUsed, bool directFilterAttached) ApplyEffectsToSource(int sourceId, int baseSendIndex = 0)
        {
            if (!AudioEfxBackend.Instance.IsEFXSupported || sourceId <= 0) return (0, false);

            try
            {
                int sendIndex = baseSendIndex;
                int sendsUsed = 0;
            bool directAttached = false;

                foreach (var effect in Effects)
                {
                    if (!effect.Enabled) continue;

                    switch (effect.Type)
                    {
                        case AudioMixerGroupEffectType.Reverb:
                        case AudioMixerGroupEffectType.Echo:
                            // Attach aux slot to source
                            if (effect.AuxSlotHandle.IsValid && sendIndex < EFXManager.MaxAuxiliarySends)
                            {
                                AudioEfxBackend.Instance.AttachAuxSlotToSource(sourceId, effect.AuxSlotHandle, sendIndex);
                                sendIndex++;
                                sendsUsed++;
                            }
                            break;

                        case AudioMixerGroupEffectType.LowPass:
                        case AudioMixerGroupEffectType.HighPass:
                            // Attach direct filter to source (only one direct filter supported per source)
                            if (effect.FilterHandle.IsValid)
                            {
                                AudioEfxBackend.Instance.AttachDirectFilterToSource(sourceId, effect.FilterHandle);
                                directAttached = true;
                            }
                            break;
                    }
                }

                return (sendsUsed, directAttached);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"[AudioMixerGroup] Failed to apply effects to source {sourceId}");
            }
            return (0, false);
        }

        public void Dispose()
        {
            // Destroy all effect handles
            foreach (var effect in Effects.ToArray())
            {
                DestroyEffectHandles(effect);
            }
            Effects.Clear();
        }
    }
}
