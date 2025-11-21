using System;
using System.Collections.Generic;
using System.IO;
using Engine.Serialization;
using Engine.Scene;
using Engine.Physics;
using Engine.Assets;
using OpenTK.Mathematics;

namespace Engine.Components
{
    /// <summary>
    /// MeshCollider - Collider qui épouse la forme d'un mesh 3D
    /// Utilise le mesh du MeshRenderer ou un mesh custom pour les collisions précises
    /// </summary>
    public sealed class MeshCollider : Collider
    {
        [Engine.Serialization.Serializable("meshGuid")]
        public Guid? MeshGuid { get; set; } = null;

        [Engine.Serialization.Serializable("convex")]
        public bool Convex = false; // Pour l'instant, on fait du concave (exact)

        [Engine.Serialization.Serializable("useMeshRendererMesh")]
        public bool UseMeshRendererMesh = true; // Si true, utilise automatiquement le mesh du MeshRenderer

        // Cache des triangles du mesh pour les collisions
        private List<Triangle> _triangles = new();
        private bool _trianglesCached = false;

        // TODO: BVH (Bounding Volume Hierarchy) for accelerated raycasts
        // private BVHNode? _bvhRoot = null;

        // Debug counters
        private static int _totalRaycastsThisFrame = 0;
        private static int _totalTrianglesTestedThisFrame = 0;
        private static System.Diagnostics.Stopwatch _frameTimer = System.Diagnostics.Stopwatch.StartNew();
        
        /// <summary>
        /// Nombre de triangles actuellement cachés pour les collisions
        /// </summary>
        public int CachedTriangleCount => _triangles.Count;
        
        /// <summary>
        /// Indique si les triangles ont été mis en cache
        /// </summary>
        public bool IsTriangleCacheDirty => !_trianglesCached;

        /// <summary>
        /// Obtenir les triangles cachés du mesh pour le rendu du gizmo
        /// </summary>
        public List<Triangle>? GetCachedTriangles()
        {
            if (!_trianglesCached) return null;
            return _triangles;
        }

        public override void OnAttached()
        {
            base.OnAttached();
            CacheTriangles();
            UpdateWorldBounds();
            
            if (_triangles.Count == 0)
            {
                Console.WriteLine($"[MeshCollider] WARNING: No triangles cached for '{Entity?.Name ?? "Unknown"}'. Check that the mesh is properly loaded.");
            }
        }

        public override void Update(float deltaTime)
        {
            // NE RIEN FAIRE dans Update pour éviter les calculs constants
            // Les bounds sont mis à jour uniquement quand nécessaire (OnAttached, RefreshMesh)
            // Ceci évite les chutes de FPS
        }

        public override OBB GetWorldOBB()
        {
            var e = Entity;
            if (e == null)
            {
                return new OBB { Center = Vector3.Zero, HalfSize = Vector3.Zero, Orientation = Matrix3.Identity };
            }

            e.GetWorldTRS(out var wpos, out var wrot, out var wscl);

            // Calculer l'AABB du mesh en espace local
            var bounds = CalculateLocalBounds();

            // Appliquer le center
            var worldCenter = wpos + Vector3.Transform(Center * wscl, wrot);

            // Demi-taille avec scale
            var absScale = new Vector3(MathF.Abs(wscl.X), MathF.Abs(wscl.Y), MathF.Abs(wscl.Z));
            var half = bounds.Extents * absScale;

            // Orientation
            var ori = Matrix3.CreateFromQuaternion(wrot);

            return new OBB { Center = worldCenter, HalfSize = half, Orientation = ori };
        }

        public override bool Raycast(Engine.Physics.Ray ray, out Engine.Physics.RaycastHit hit)
        {
            hit = default;

            // Debug: Count raycasts per frame
            _totalRaycastsThisFrame++;
            if (_frameTimer.ElapsedMilliseconds > 1000)
            {
                Console.WriteLine($"[MeshCollider] {_totalRaycastsThisFrame} raycasts/sec, {_totalTrianglesTestedThisFrame:N0} triangles tested/sec");
                _totalRaycastsThisFrame = 0;
                _totalTrianglesTestedThisFrame = 0;
                _frameTimer.Restart();
            }

            if (!_trianglesCached)
            {
                CacheTriangles();
                if (!_trianglesCached) return false;
            }

            var e = Entity;
            if (e == null) return false;

            // OPTIMIZATION: Early broad-phase check with OBB to skip expensive triangle tests
            var obb = GetWorldOBB();
            if (!RayOBBIntersect(ray, obb))
            {
                return false; // Ray doesn't even hit the bounding box
            }

            _totalTrianglesTestedThisFrame += _triangles.Count; // Count when we actually test triangles

            e.GetWorldTRS(out var wpos, out var wrot, out var wscl);

            // Transformer le rayon en espace local du mesh
            var invRot = wrot.Inverted();
            var localOrigin = Vector3.Transform(ray.Origin - wpos, invRot);
            var localDir = Vector3.Transform(ray.Direction, invRot).Normalized();

            // Appliquer l'inverse du scale
            var invScale = new Vector3(
                MathF.Abs(wscl.X) > 0.0001f ? 1f / wscl.X : 1f,
                MathF.Abs(wscl.Y) > 0.0001f ? 1f / wscl.Y : 1f,
                MathF.Abs(wscl.Z) > 0.0001f ? 1f / wscl.Z : 1f
            );
            localOrigin *= invScale;
            // Ne pas normaliser localDir après scale car on veut garder la direction correcte

            float closestDist = float.MaxValue;
            Vector3 closestPoint = Vector3.Zero;
            Vector3 closestNormal = Vector3.UnitY;
            bool foundHit = false;

            // Tester chaque triangle (TODO: Use BVH/Octree for large meshes)
            foreach (var tri in _triangles)
            {
                if (RayTriangleIntersect(localOrigin, localDir, tri.V0, tri.V1, tri.V2, out float t, out Vector3 bary))
                {
                    if (t >= 0 && t < closestDist)
                    {
                        closestDist = t;
                        foundHit = true;

                        // Point d'intersection en espace local
                        closestPoint = localOrigin + localDir * t;

                        // Normale du triangle (en espace local)
                        var e1 = tri.V1 - tri.V0;
                        var e2 = tri.V2 - tri.V0;
                        closestNormal = Vector3.Cross(e1, e2).Normalized();
                    }
                }
            }

            if (foundHit)
            {
                // Reconvertir en espace monde
                closestPoint *= wscl; // Réappliquer le scale
                closestPoint = Vector3.Transform(closestPoint, wrot) + wpos;

                // Transformer la normale (utiliser le quaternion directement)
                closestNormal = Vector3.Transform(closestNormal, wrot).Normalized();

                // Distance en espace monde
                float worldDist = (closestPoint - ray.Origin).Length;

                hit = new RaycastHit
                {
                    ColliderComponent = this,
                    Component = this,
                    Entity = Entity,
                    Distance = worldDist,
                    Point = closestPoint,
                    Normal = closestNormal
                };
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cache les triangles du mesh pour les collisions
        /// </summary>
        private void CacheTriangles()
        {
            _triangles.Clear();
            _trianglesCached = false;

            MeshAsset? meshAsset = null;
            Guid targetGuid = Guid.Empty;

            // 1. Si UseMeshRendererMesh, récupérer le mesh du MeshRenderer
            if (UseMeshRendererMesh && Entity != null)
            {
                var meshRenderer = Entity.GetComponent<MeshRendererComponent>();
                if (meshRenderer != null && meshRenderer.CustomMeshGuid.HasValue)
                {
                    targetGuid = meshRenderer.CustomMeshGuid.Value;
                    Console.WriteLine($"[MeshCollider] Using mesh from MeshRenderer: {targetGuid}");
                }
                else
                {
                    Console.WriteLine($"[MeshCollider] MeshRenderer found but no CustomMeshGuid set for '{Entity?.Name ?? "Unknown"}'");
                }
            }

            // 2. Sinon, utiliser le MeshGuid spécifié
            if (targetGuid == Guid.Empty && MeshGuid.HasValue && MeshGuid.Value != Guid.Empty)
            {
                targetGuid = MeshGuid.Value;
                Console.WriteLine($"[MeshCollider] Using custom mesh GUID: {targetGuid}");
            }
            
            if (targetGuid == Guid.Empty)
            {
                Console.WriteLine($"[MeshCollider] No mesh GUID available for '{Entity?.Name ?? "Unknown"}'");
                return;
            }

            // Charger le MeshAsset depuis le fichier
            if (AssetDatabase.TryGet(targetGuid, out var record))
            {
                try
                {
                    // IMPORTANT: Pour les modèles 3D, le path dans AssetDatabase pointe vers le fichier source (.gltf, .fbx, etc.)
                    // mais le MeshAsset sérialisé est dans .meshasset. Il faut ajouter l'extension.
                    string meshAssetPath = record.Path;
                    if (!meshAssetPath.EndsWith(".meshasset", StringComparison.OrdinalIgnoreCase))
                    {
                        meshAssetPath += ".meshasset";
                    }

                    Console.WriteLine($"[MeshCollider] Loading mesh from: {meshAssetPath}");

                    if (!File.Exists(meshAssetPath))
                    {
                        Console.WriteLine($"[MeshCollider] ERROR: MeshAsset file not found: {meshAssetPath}");
                        Console.WriteLine($"[MeshCollider] Make sure the model has been imported properly.");
                        return;
                    }

                    meshAsset = MeshAsset.Load(meshAssetPath);
                    Console.WriteLine($"[MeshCollider] Mesh loaded: {meshAsset.Name}, SubMeshes: {meshAsset.SubMeshes.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MeshCollider] Erreur lors du chargement du mesh {record.Path}: {ex.Message}");
                    return;
                }
            }
            else
            {
                Console.WriteLine($"[MeshCollider] Mesh GUID {targetGuid} not found in AssetDatabase");
                return;
            }

            if (meshAsset == null)
            {
                Console.WriteLine($"[MeshCollider] Aucun mesh trouvé pour la collision sur {Entity?.Name ?? "Unknown"}");
                return;
            }

            // Extraire les triangles de tous les submeshes
            foreach (var subMesh in meshAsset.SubMeshes)
            {
                var vertices = subMesh.Vertices;
                var indices = subMesh.Indices;

                if (vertices == null || indices == null) continue;

                // Les vertices sont interleaved: Position(3) + Normal(3) + TexCoord(2) = 8 floats
                // Créer les triangles
                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (i + 2 >= indices.Length) break;

                    var i0 = indices[i];
                    var i1 = indices[i + 1];
                    var i2 = indices[i + 2];

                    // Vérifier que les indices sont valides
                    if (i0 * 8 + 2 >= vertices.Length || i1 * 8 + 2 >= vertices.Length || i2 * 8 + 2 >= vertices.Length) 
                        continue;

                    // Extraire les positions (3 premiers floats de chaque vertex)
                    var v0 = new Vector3(vertices[i0 * 8], vertices[i0 * 8 + 1], vertices[i0 * 8 + 2]);
                    var v1 = new Vector3(vertices[i1 * 8], vertices[i1 * 8 + 1], vertices[i1 * 8 + 2]);
                    var v2 = new Vector3(vertices[i2 * 8], vertices[i2 * 8 + 1], vertices[i2 * 8 + 2]);

                    _triangles.Add(new Triangle
                    {
                        V0 = v0,
                        V1 = v1,
                        V2 = v2
                    });
                }
            }

            _trianglesCached = _triangles.Count > 0;
            
            if (_triangles.Count > 0)
            {
                Console.WriteLine($"[MeshCollider] ✓ Successfully cached {_triangles.Count:N0} triangles for '{Entity?.Name ?? "Unknown"}'");
                Console.WriteLine($"[MeshCollider]   Collision will follow mesh geometry precisely.");
            }
            else
            {
                Console.WriteLine($"[MeshCollider] ⚠ WARNING: 0 triangles cached for '{Entity?.Name ?? "Unknown"}' - No collision will occur!");
            }
        }

        /// <summary>
        /// Calcule les bounds locaux du mesh basé sur les triangles réels
        /// </summary>
        private Bounds CalculateLocalBounds()
        {
            // Si pas de triangles, utiliser des bounds par défaut très petits
            if (_triangles.Count == 0)
            {
                return new Bounds { Center = Vector3.Zero, Extents = Vector3.One };
            }

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var tri in _triangles)
            {
                min = Vector3.ComponentMin(min, tri.V0);
                min = Vector3.ComponentMin(min, tri.V1);
                min = Vector3.ComponentMin(min, tri.V2);

                max = Vector3.ComponentMax(max, tri.V0);
                max = Vector3.ComponentMax(max, tri.V1);
                max = Vector3.ComponentMax(max, tri.V2);
            }

            var center = (min + max) * 0.5f;
            var extents = (max - min) * 0.5f;

            return new Bounds { Center = center, Extents = extents };
        }

        /// <summary>
        /// Test d'intersection rayon-triangle (algorithme Möller-Trumbore)
        /// </summary>
        private bool RayTriangleIntersect(Vector3 rayOrigin, Vector3 rayDir, Vector3 v0, Vector3 v1, Vector3 v2, 
            out float t, out Vector3 barycentric)
        {
            t = 0;
            barycentric = Vector3.Zero;

            const float EPSILON = 0.0000001f;

            var edge1 = v1 - v0;
            var edge2 = v2 - v0;

            var h = Vector3.Cross(rayDir, edge2);
            var a = Vector3.Dot(edge1, h);

            if (a > -EPSILON && a < EPSILON)
                return false; // Le rayon est parallèle au triangle

            var f = 1.0f / a;
            var s = rayOrigin - v0;
            var u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
                return false;

            var q = Vector3.Cross(s, edge1);
            var v = f * Vector3.Dot(rayDir, q);

            if (v < 0.0f || u + v > 1.0f)
                return false;

            // Calculer t
            t = f * Vector3.Dot(edge2, q);

            if (t > EPSILON)
            {
                barycentric = new Vector3(1.0f - u - v, u, v);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Forcer le recalcul des triangles (utile si le mesh change)
        /// </summary>
        public void RefreshMesh()
        {
            _trianglesCached = false;
            CacheTriangles();
            UpdateWorldBounds();
        }

        /// <summary>
        /// Triangle du mesh en coordonnées mondiales
        /// </summary>
        public struct Triangle
        {
            public Vector3 V0, V1, V2;
        }

        /// <summary>
        /// Fast Ray-OBB intersection test for broad-phase culling
        /// </summary>
        private bool RayOBBIntersect(Engine.Physics.Ray ray, Engine.Physics.OBB obb)
        {
            // Transform ray to OBB local space
            var R = obb.Orientation;
            var invR = R.Transposed(); // Inverse of rotation matrix = transpose

            var localOrigin = invR * (ray.Origin - obb.Center);
            var localDir = invR * ray.Direction;

            // Now test against AABB in local space
            var min = -obb.HalfSize;
            var max = obb.HalfSize;

            float tMin = 0f;
            float tMax = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                float origin = i == 0 ? localOrigin.X : i == 1 ? localOrigin.Y : localOrigin.Z;
                float dir = i == 0 ? localDir.X : i == 1 ? localDir.Y : localDir.Z;
                float minVal = i == 0 ? min.X : i == 1 ? min.Y : min.Z;
                float maxVal = i == 0 ? max.X : i == 1 ? max.Y : max.Z;

                if (MathF.Abs(dir) < 0.0001f)
                {
                    // Ray is parallel to slab, check if origin is within bounds
                    if (origin < minVal || origin > maxVal)
                        return false;
                }
                else
                {
                    float t1 = (minVal - origin) / dir;
                    float t2 = (maxVal - origin) / dir;

                    if (t1 > t2)
                    {
                        float temp = t1;
                        t1 = t2;
                        t2 = temp;
                    }

                    tMin = MathF.Max(tMin, t1);
                    tMax = MathF.Min(tMax, t2);

                    if (tMin > tMax)
                        return false;
                }
            }

            return tMax >= 0; // Hit if tMax is positive
        }
    }
}
