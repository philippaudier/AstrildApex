using System;

namespace Engine.Audio.Effects
{
    /// <summary>
    /// Classe de base pour tous les effets audio
    /// Les effets utilisent l'API OpenAL Effects Extension (EFX)
    /// </summary>
    public abstract class AudioEffect : IDisposable
    {
        public Guid Guid { get; protected set; }
        public string Name { get; set; }

        protected int _effectId = -1;
        protected int _slotId = -1;

        public bool IsEnabled { get; set; } = true;
        public bool IsCreated => _effectId != -1;

        protected AudioEffect(string name)
        {
            Guid = Guid.NewGuid();
            Name = name;
        }

        /// <summary>
        /// Initialise l'effet OpenAL
        /// </summary>
        public abstract void Create();

        /// <summary>
        /// Applique l'effet à une source audio
        /// </summary>
        public abstract void Apply(int sourceId);
        
        /// <summary>
        /// Retire l'effet d'une source audio
        /// </summary>
        public virtual void Remove(int sourceId)
        {
            // Implementation par défaut (peut être override dans les classes dérivées)
            if (_slotId != -1 && sourceId != -1)
            {
                // TODO: Détacher le slot de la source avec EFX
                // AL.Source(sourceId, ALSourcei.EfxDirectFilter, 0);
            }
        }

        /// <summary>
        /// Met à jour les paramètres de l'effet
        /// </summary>
        public abstract void UpdateParameters();

        public virtual void Dispose()
        {
            // TODO: Implémenter avec OpenAL EFX
            // AL.DeleteEffect(_effectId);
            // AL.DeleteAuxiliaryEffectSlot(_slotId);
        }
    }
}
