using System;

namespace Engine.Serialization
{
    /// <summary>
    /// Marque une propriété ou un champ comme sérialisable pour la sauvegarde de scènes
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class SerializableAttribute : Attribute
    {
        /// <summary>
        /// Nom à utiliser dans le JSON (optionnel)
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Indique si cette propriété est obligatoire pour la désérialisation
        /// </summary>
        public bool Required { get; set; } = false;
        
        /// <summary>
        /// Version minimale supportée (pour la compatibilité ascendante)
        /// </summary>
        public int MinVersion { get; set; } = 1;
        
        public SerializableAttribute() { }
        
        public SerializableAttribute(string name)
        {
            Name = name;
        }
    }
}