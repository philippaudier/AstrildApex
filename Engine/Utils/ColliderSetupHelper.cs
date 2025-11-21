using System;
using Engine.Components;
using Engine.Scene;

namespace Engine.Utils
{
    /// <summary>
    /// Utilitaires pour configurer automatiquement les colliders sur les entités
    /// </summary>
    public static class ColliderSetupHelper
    {
        /// <summary>
        /// Vérifie si une entité a au moins un collider (excluant CharacterController)
        /// </summary>
        public static bool HasCollider(Entity entity)
        {
            if (entity == null) return false;

            // Vérifier tous les types de colliders
            return entity.HasComponent<BoxCollider>() ||
                   entity.HasComponent<SphereCollider>() ||
                   entity.HasComponent<CapsuleCollider>() ||
                   entity.HasComponent<MeshCollider>() ||
                   entity.HasComponent<HeightfieldCollider>();
        }

        /// <summary>
        /// Ajoute automatiquement un MeshCollider à une entité si elle a un MeshRenderer mais pas de collider
        /// </summary>
        /// <returns>True si un collider a été ajouté</returns>
        public static bool EnsureCollider(Entity entity, bool forceAdd = false)
        {
            if (entity == null) return false;

            // Si l'entité a déjà un collider et qu'on ne force pas, ne rien faire
            if (!forceAdd && HasCollider(entity))
                return false;

            // Si l'entité a un MeshRenderer avec un mesh custom, ajouter un MeshCollider
            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
            if (meshRenderer != null && meshRenderer.IsUsingCustomMesh())
            {
                var meshCollider = entity.AddComponent<MeshCollider>();
                meshCollider.UseMeshRendererMesh = true;
                
                Console.WriteLine($"[ColliderSetupHelper] Auto-added MeshCollider to entity '{entity.Name}'");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parcourt récursivement une hiérarchie d'entités et ajoute des colliders si nécessaire
        /// </summary>
        /// <param name="root">Entité racine de la hiérarchie</param>
        /// <param name="addToChildren">Si true, ajoute aussi aux enfants</param>
        /// <returns>Nombre de colliders ajoutés</returns>
        public static int EnsureCollidersRecursive(Entity root, bool addToChildren = true)
        {
            if (root == null) return 0;

            int count = 0;

            // Ajouter au root si nécessaire
            if (EnsureCollider(root))
                count++;

            // Ajouter aux enfants si demandé
            if (addToChildren && root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    count += EnsureCollidersRecursive(child, addToChildren);
                }
            }

            return count;
        }

        /// <summary>
        /// Suggère le meilleur type de collider pour une entité basé sur sa géométrie
        /// </summary>
        public static Type SuggestColliderType(Entity entity)
        {
            if (entity == null) return typeof(BoxCollider);

            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
            if (meshRenderer == null)
                return typeof(BoxCollider);

            // Si c'est un mesh importé, suggérer MeshCollider
            if (meshRenderer.IsUsingCustomMesh())
                return typeof(MeshCollider);

            // Selon le type de mesh primitif
            switch (meshRenderer.Mesh)
            {
                case MeshKind.Sphere:
                    return typeof(SphereCollider);

                case MeshKind.Capsule:
                    return typeof(CapsuleCollider);

                case MeshKind.Cube:
                case MeshKind.Plane:
                case MeshKind.Quad:
                default:
                    return typeof(BoxCollider);
            }
        }

        /// <summary>
        /// Configure les paramètres d'un collider pour correspondre approximativement à la géométrie
        /// </summary>
        public static void ConfigureColliderFromGeometry(Entity entity, Collider collider)
        {
            if (entity == null || collider == null) return;

            var meshRenderer = entity.GetComponent<MeshRendererComponent>();
            if (meshRenderer == null) return;

            // Pour les MeshColliders, s'assurer qu'ils utilisent le bon mesh
            if (collider is MeshCollider meshCollider)
            {
                meshCollider.UseMeshRendererMesh = true;
                meshCollider.RefreshMesh();
            }

            // Pour les autres colliders, ajuster la taille basée sur le scale de l'entité
            var scale = entity.Transform.Scale;

            if (collider is BoxCollider boxCollider)
            {
                // Ajuster la taille pour correspondre au scale
                boxCollider.Size = new OpenTK.Mathematics.Vector3(
                    MathF.Abs(scale.X),
                    MathF.Abs(scale.Y),
                    MathF.Abs(scale.Z)
                );
            }
            else if (collider is SphereCollider sphereCollider)
            {
                // Utiliser le plus grand composant du scale
                sphereCollider.Radius = MathF.Max(MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Y)), MathF.Abs(scale.Z)) * 0.5f;
            }
            else if (collider is CapsuleCollider capsuleCollider)
            {
                capsuleCollider.Radius = MathF.Max(MathF.Abs(scale.X), MathF.Abs(scale.Z)) * 0.5f;
                capsuleCollider.Height = MathF.Abs(scale.Y);
            }
        }
    }
}
