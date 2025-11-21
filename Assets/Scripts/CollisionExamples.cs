// Exemple d'utilisation du système de collision amélioré
// Ce fichier montre comment ajouter des colliders aux modèles importés

using Engine.Components;
using Engine.Scene;
using Engine.Utils;
using System;

namespace Examples
{
    /// <summary>
    /// Exemples d'utilisation du MeshCollider et du système de collision
    /// </summary>
    public static class CollisionExamples
    {
        /// <summary>
        /// Exemple 1 : Ajouter un MeshCollider à un modèle 3D importé
        /// </summary>
        public static void Example1_AddMeshColliderToImportedModel(Entity modelEntity)
        {
            Console.WriteLine("=== Exemple 1: Ajouter un MeshCollider ===");

            // Méthode 1 : Automatique via helper
            bool added = ColliderSetupHelper.EnsureCollider(modelEntity);
            if (added)
            {
                Console.WriteLine($"✓ MeshCollider ajouté automatiquement à '{modelEntity.Name}'");
            }

            // Méthode 2 : Manuelle
            if (!modelEntity.HasComponent<MeshCollider>())
            {
                var meshCollider = modelEntity.AddComponent<MeshCollider>();
                meshCollider.UseMeshRendererMesh = true; // Utilise automatiquement le mesh du MeshRenderer
                Console.WriteLine($"✓ MeshCollider ajouté manuellement à '{modelEntity.Name}'");
            }
        }

        /// <summary>
        /// Exemple 2 : Ajouter des MeshColliders à toute une scène de ville
        /// </summary>
        public static void Example2_AddCollidersToEntireCity(Entity cityRootEntity)
        {
            Console.WriteLine("=== Exemple 2: Ajouter des colliders à une ville entière ===");

            // Ajoute récursivement des colliders à tous les enfants
            int count = ColliderSetupHelper.EnsureCollidersRecursive(cityRootEntity, addToChildren: true);
            Console.WriteLine($"✓ {count} MeshColliders ajoutés à la ville '{cityRootEntity.Name}'");
        }

        /// <summary>
        /// Exemple 3 : Configurer un CharacterController pour fonctionner correctement
        /// </summary>
        public static void Example3_SetupCharacterController(Entity playerEntity)
        {
            Console.WriteLine("=== Exemple 3: Configurer un CharacterController ===");

            var controller = playerEntity.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = playerEntity.AddComponent<CharacterController>();
            }

            // Configuration recommandée
            controller.Height = 1.8f;
            controller.Radius = 0.35f;
            controller.StepOffset = 0.3f;
            controller.Gravity = 9.81f;
            controller.GroundCheckDistance = 3.0f;
            controller.SkinWidth = 0.02f;
            controller.GroundOffset = 0.0f;
            controller.ClimbSmoothSpeed = 6f;
            controller.DescendSmoothSpeed = 12f;
            controller.MaxSlopeAngleDeg = 45f;

            // Activer le debug pour diagnostiquer les problèmes
            controller.DebugPhysics = false; // Mettre à true pour voir les logs

            Console.WriteLine($"✓ CharacterController configuré sur '{playerEntity.Name}'");
        }

        /// <summary>
        /// Exemple 4 : Déplacement d'un personnage avec collision
        /// </summary>
        public static void Example4_MovePlayerWithCollision(Entity playerEntity, float deltaTime)
        {
            var controller = playerEntity.GetComponent<CharacterController>();
            if (controller == null) return;

            // Entrées clavier (exemple)
            float moveX = 0f; // -1 à 1 (gauche/droite)
            float moveZ = 0f; // -1 à 1 (avant/arrière)
            
            // Exemple avec des touches fictives
            // if (Input.IsKeyPressed(Key.W)) moveZ = 1f;
            // if (Input.IsKeyPressed(Key.S)) moveZ = -1f;
            // if (Input.IsKeyPressed(Key.A)) moveX = -1f;
            // if (Input.IsKeyPressed(Key.D)) moveX = 1f;

            // Vitesse de déplacement
            float speed = 5f;

            // Calculer le mouvement
            var forward = playerEntity.Transform.Forward;
            var right = playerEntity.Transform.Right;
            
            var motion = (forward * moveZ + right * moveX) * speed * deltaTime;

            // Appliquer le mouvement (avec collision automatique)
            controller.Move(motion, deltaTime);

            // Saut (exemple)
            // if (Input.IsKeyPressed(Key.Space) && controller.IsGrounded)
            // {
            //     controller.AddVerticalImpulse(5f); // Force du saut
            // }

            // Vérifier l'état
            if (controller.IsGrounded)
            {
                // Le joueur est au sol
            }
            else
            {
                // Le joueur est en l'air (saut ou chute)
            }
        }

        /// <summary>
        /// Exemple 5 : Vérifier et corriger les collisions manquantes dans une scène
        /// </summary>
        public static void Example5_AuditSceneCollisions(Scene.Scene scene)
        {
            Console.WriteLine("=== Exemple 5: Audit des collisions dans la scène ===");

            int totalEntities = 0;
            int entitiesWithMesh = 0;
            int entitiesWithCollider = 0;
            int collidersAdded = 0;

            // Parcourir toutes les entités
            foreach (var entity in scene.GetAllEntities())
            {
                totalEntities++;

                // Vérifier si l'entité a un mesh renderer
                var meshRenderer = entity.GetComponent<MeshRendererComponent>();
                if (meshRenderer != null && meshRenderer.IsUsingCustomMesh())
                {
                    entitiesWithMesh++;

                    // Vérifier si elle a un collider
                    if (ColliderSetupHelper.HasCollider(entity))
                    {
                        entitiesWithCollider++;
                    }
                    else
                    {
                        // Ajouter automatiquement
                        if (ColliderSetupHelper.EnsureCollider(entity))
                        {
                            collidersAdded++;
                            Console.WriteLine($"  ⚠️ Ajouté un collider manquant à '{entity.Name}'");
                        }
                    }
                }
            }

            Console.WriteLine($"\n📊 Résumé de l'audit:");
            Console.WriteLine($"  - Entités totales: {totalEntities}");
            Console.WriteLine($"  - Entités avec mesh custom: {entitiesWithMesh}");
            Console.WriteLine($"  - Entités avec collider: {entitiesWithCollider + collidersAdded}");
            Console.WriteLine($"  - Colliders ajoutés: {collidersAdded}");

            if (collidersAdded > 0)
            {
                Console.WriteLine($"\n✅ {collidersAdded} colliders manquants ont été ajoutés!");
            }
            else
            {
                Console.WriteLine("\n✅ Toutes les entités ont déjà des colliders appropriés!");
            }
        }

        /// <summary>
        /// Exemple 6 : Utiliser un MeshCollider avec un mesh personnalisé
        /// </summary>
        public static void Example6_CustomMeshCollider(Entity entity, Guid customMeshGuid)
        {
            Console.WriteLine("=== Exemple 6: MeshCollider avec mesh personnalisé ===");

            var meshCollider = entity.AddComponent<MeshCollider>();
            
            // Ne pas utiliser le mesh du MeshRenderer
            meshCollider.UseMeshRendererMesh = false;
            
            // Spécifier un mesh spécifique pour les collisions (version simplifiée)
            meshCollider.MeshGuid = customMeshGuid;
            
            // Forcer le recalcul
            meshCollider.RefreshMesh();

            Console.WriteLine($"✓ MeshCollider configuré avec mesh personnalisé");
        }

        /// <summary>
        /// Exemple 7 : Gérer différents types de colliders selon le contexte
        /// </summary>
        public static void Example7_SmartColliderSelection(Entity entity)
        {
            Console.WriteLine("=== Exemple 7: Sélection intelligente du collider ===");

            // Suggérer le meilleur type
            var suggestedType = ColliderSetupHelper.SuggestColliderType(entity);
            Console.WriteLine($"  Type suggéré pour '{entity.Name}': {suggestedType.Name}");

            // Ajouter le collider approprié
            if (suggestedType == typeof(MeshCollider))
            {
                var meshCollider = entity.AddComponent<MeshCollider>();
                ColliderSetupHelper.ConfigureColliderFromGeometry(entity, meshCollider);
            }
            else if (suggestedType == typeof(BoxCollider))
            {
                var boxCollider = entity.AddComponent<BoxCollider>();
                ColliderSetupHelper.ConfigureColliderFromGeometry(entity, boxCollider);
            }
            else if (suggestedType == typeof(SphereCollider))
            {
                var sphereCollider = entity.AddComponent<SphereCollider>();
                ColliderSetupHelper.ConfigureColliderFromGeometry(entity, sphereCollider);
            }
            // etc...

            Console.WriteLine($"✓ Collider {suggestedType.Name} ajouté et configuré");
        }
    }
}
