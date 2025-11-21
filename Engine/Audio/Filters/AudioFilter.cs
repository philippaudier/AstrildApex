using System;

namespace Engine.Audio.Filters
{
    /// <summary>
    /// Classe de base pour les filtres audio (Low-pass, High-pass, Band-pass)
    /// </summary>
    public abstract class AudioFilter : IDisposable
    {
        public Guid Guid { get; protected set; }
        public string Name { get; set; }

        protected int _filterId = -1;

        public bool IsEnabled { get; set; } = true;
        public bool IsCreated => _filterId != -1;

        protected AudioFilter(string name)
        {
            Guid = Guid.NewGuid();
            Name = name;
        }

        /// <summary>
        /// Initialise le filtre OpenAL
        /// </summary>
        public abstract void Create();

        /// <summary>
        /// Applique le filtre à une source audio
        /// </summary>
        public abstract void Apply(int sourceId);

        /// <summary>
        /// Met à jour les paramètres du filtre
        /// </summary>
        public abstract void UpdateParameters();

        public virtual void Dispose()
        {
            // TODO: Implémenter avec OpenAL EFX
            // AL.DeleteFilter(_filterId);
        }
    }
}
