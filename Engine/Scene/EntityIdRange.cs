namespace Engine.Scene
{
    /// <summary>
    /// Centralise la définition des plages d'IDs d'entités et fournit des utilitaires 
    /// pour éviter les constantes magiques dispersées dans le code.
    /// </summary>
    public static class EntityIdRange
    {
        // === Plages d'IDs ===
        
        /// <summary>ID minimum pour une entité valide</summary>
        public const uint MinEntityId = 1000;
        
        /// <summary>ID maximum pour une entité valide (exclusif)</summary>
        public const uint MaxEntityId = 10000;
        
        /// <summary>ID de début pour la plage des gizmos</summary>
        public const uint GizmoIdStart = 900000;
        
        /// <summary>ID réservé spécial pour tous les gizmos (compatibilité)</summary>
        public const uint GizmoReservedId = 999999;
        
        // === Utilitaires de validation ===
        
        /// <summary>
        /// Vérifie si un ID correspond à une entité valide dans la plage autorisée.
        /// </summary>
        /// <param name="id">ID à vérifier</param>
        /// <returns>True si l'ID est dans la plage des entités</returns>
        public static bool IsEntityId(uint id)
        {
            return id >= MinEntityId && id < MaxEntityId;
        }
        
        /// <summary>
        /// Vérifie si un ID correspond à la plage réservée aux gizmos.
        /// </summary>
        /// <param name="id">ID à vérifier</param>
        /// <returns>True si l'ID est dans la plage des gizmos</returns>
        public static bool IsGizmoRangeId(uint id)
        {
            return id >= GizmoIdStart || id == GizmoReservedId;
        }
        
        /// <summary>
        /// Vérifie si un ID est valide (entité ou gizmo) et non nul.
        /// </summary>
        /// <param name="id">ID à vérifier</param>
        /// <returns>True si l'ID est valide</returns>
        public static bool IsValidId(uint id)
        {
            return id != 0 && (IsEntityId(id) || IsGizmoRangeId(id));
        }
        
        /// <summary>
        /// Calcule le nombre maximum d'entités supportées avec la plage actuelle.
        /// </summary>
        public static uint MaxSupportedEntities => MaxEntityId - MinEntityId;
        
        /// <summary>
        /// Affiche des informations de debug sur les plages d'IDs configurées.
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"EntityIdRange: Entities[{MinEntityId}-{MaxEntityId-1}] ({MaxSupportedEntities} max), " +
                   $"Gizmos[{GizmoIdStart}+], Reserved[{GizmoReservedId}]";
        }
    }
}