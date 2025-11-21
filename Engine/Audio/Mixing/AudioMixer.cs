using System;
using System.Collections.Generic;
using Serilog;

namespace Engine.Audio.Mixing
{
    /// <summary>
    /// Mixeur audio principal - gère les groupes de mixage
    /// Similaire à Unity AudioMixer
    /// </summary>
    public sealed class AudioMixer
    {
        public Guid Guid { get; private set; }
        public string Name { get; set; }

        private readonly Dictionary<string, AudioMixerGroup> _groups = new();
        private AudioMixerGroup _masterGroup;

        public AudioMixerGroup MasterGroup => _masterGroup;

        /// <summary>
        /// Returns true if any group has Solo enabled
        /// </summary>
        public bool IsAnySoloActive()
        {
            foreach (var g in _groups.Values)
            {
                if (g.Solo) return true;
            }
            return false;
        }

        /// <summary>
        /// Get effective volume for a group, taking into account mute, parent and solo state
        /// </summary>
        public float GetEffectiveGroupVolume(string groupName)
        {
            if (!_groups.TryGetValue(groupName, out var group))
                return 1.0f;

            // If any solo is active, non-solo groups are effectively muted
            if (IsAnySoloActive() && !group.Solo)
                return 0f;

            return group.EffectiveVolume;
        }

        public AudioMixer(string name)
        {
            Guid = Guid.NewGuid();
            Name = name;

            // Créer le groupe Master par défaut
            _masterGroup = new AudioMixerGroup("Master");
            _groups["Master"] = _masterGroup;
        }

        /// <summary>
        /// Crée un nouveau groupe de mixage
        /// </summary>
        public AudioMixerGroup CreateGroup(string name, AudioMixerGroup? parent = null)
        {
            if (_groups.ContainsKey(name))
            {
                Log.Warning($"[AudioMixer] Group '{name}' already exists");
                return _groups[name];
            }

            var group = new AudioMixerGroup(name)
            {
                Parent = parent ?? _masterGroup
            };

            _groups[name] = group;
            Log.Information($"[AudioMixer] Created group: {name}");
            return group;
        }

        /// <summary>
        /// Obtient un groupe par son nom
        /// </summary>
        public AudioMixerGroup? GetGroup(string name)
        {
            _groups.TryGetValue(name, out var group);
            return group;
        }

        /// <summary>
        /// Obtient tous les groupes
        /// </summary>
        public IEnumerable<AudioMixerGroup> GetAllGroups()
        {
            return _groups.Values;
        }

        /// <summary>
        /// Définit le volume d'un groupe
        /// </summary>
        public void SetGroupVolume(string groupName, float volume)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Volume = volume;
            }
        }

        /// <summary>
        /// Obtient le volume d'un groupe
        /// </summary>
        public float GetGroupVolume(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                return group.Volume;
            }
            return 1.0f;
        }

        /// <summary>
        /// Mute/unmute un groupe
        /// </summary>
        public void SetGroupMute(string groupName, bool mute)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Mute = mute;
            }
        }
    }
}
