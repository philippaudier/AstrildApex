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
        private List<Physics.Triangle> _triangles = new();
        private bool _trianglesCached = false;

        // BVH (Bounding Volume Hierarchy) for accelerated raycasts
        private Physics.BVH? _bvh = null;
        private bool _bvhBuilt = false;

        // Transform caching to avoid recalculating every raycast
        private Vector3 _cachedWorldPos;
        private Quaternion _cachedWorldRot;
        private Vector3 _cachedWorldScale;
        private Quaternion _cachedInvRot;
        private Vector3 _cachedInvScale;
        private bool _transformCached = false;

        // PERF FIX: Debug counters disabled (were never used after logging removal)
        // private static int _totalRaycastsThisFrame = 0;
        // private static int _totalTrianglesTestedThisFrame = 0;
        // private static System.Diagnostics.Stopwatch _frameTimer = System.Diagnostics.Stopwatch.StartNew();
        
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
        public List<Physics.Triangle>? GetCachedTriangles()
        {
            if (!_trianglesCached) return null;
            return _triangles;
        }

        public override void OnAttached()
        {
            base.OnAttached();
            CacheTriangles();
            UpdateWorldBounds();

            // PERF FIX: Removed per-attach log (happens every scene load)
            // if (_triangles.Count == 0)
            // {
            //     Console.WriteLine($"[MeshCollider] WARNING: No triangles cached for '{Entity?.Name ?? "Unknown"}'. Check that the mesh is properly loaded.");
            // }
        }

        public override void Update(float deltaTime)
        {
            // Cache transform once per frame to avoid recalculating for every raycast
            if (Entity != null)
            {
                Entity.GetWorldTRS(out _cachedWorldPos, out _cachedWorldRot, out _cachedWorldScale);
                _cachedInvRot = _cachedWorldRot.Inverted();
                _cachedInvScale = new Vector3(
                    MathF.Abs(_cachedWorldScale.X) > 0.0001f ? 1f / _cachedWorldScale.X : 1f,
                    MathF.Abs(_cachedWorldScale.Y) > 0.0001f ? 1f / _cachedWorldScale.Y : 1f,
                    MathF.Abs(_cachedWorldScale.Z) > 0.0001f ? 1f / _cachedWorldScale.Z : 1f
                );
                _transformCached = true;
            }
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

            // PERF FIX: Debug counter disabled (was never used after logging removal)
            // _totalRaycastsThisFrame++;

            if (!_trianglesCached)
            {
                CacheTriangles();
                if (!_trianglesCached) return false;
            }

            // Build BVH if not built yet
            // For large meshes (>50k triangles), use fallback until BVH is ready
            if (!_bvhBuilt && _triangles.Count > 0)
            {
                // Build immediately for small meshes (< 10k triangles)
                if (_triangles.Count < 10000)
                {
                    BuildBVH();
                }
                else if (_bvh == null)
                {
                    // Large mesh: build in background (first frame uses fallback)
                    System.Threading.Tasks.Task.Run(() => BuildBVH());
                    // Use fallback this frame
                }
            }

            var e = Entity;
            if (e == null) return false;

            // OPTIMIZATION: Early broad-phase check with OBB to skip expensive triangle tests
            var obb = GetWorldOBB();
            if (!RayOBBIntersect(ray, obb))
            {
                return false; // Ray doesn't even hit the bounding box
            }

            // Use cached transform if available, otherwise compute
            Vector3 wpos, wscl, invScale;
            Quaternion wrot, invRot;

            if (_transformCached)
            {
                wpos = _cachedWorldPos;
                wrot = _cachedWorldRot;
                wscl = _cachedWorldScale;
                invRot = _cachedInvRot;
                invScale = _cachedInvScale;
            }
            else
            {
                e.GetWorldTRS(out wpos, out wrot, out wscl);
                invRot = wrot.Inverted();
                invScale = new Vector3(
                    MathF.Abs(wscl.X) > 0.0001f ? 1f / wscl.X : 1f,
                    MathF.Abs(wscl.Y) > 0.0001f ? 1f / wscl.Y : 1f,
                    MathF.Abs(wscl.Z) > 0.0001f ? 1f / wscl.Z : 1f
                );
            }

            // Transformer le rayon en espace local du mesh
            var localOrigin = Vector3.Transform(ray.Origin - wpos, invRot);
            var localDir = Vector3.Transform(ray.Direction, invRot).Normalized();

            // Appliquer l'inverse du scale
            localOrigin *= invScale;

            // Use BVH for fast traversal (10-50 triangle tests instead of 10,000+)
            bool foundHit = false;
            float closestDist = float.MaxValue;
            Vector3 closestNormal = Vector3.UnitY;
            int trianglesTested = 0;

            if (_bvh != null && _bvhBuilt)
            {
                var localRay = new Physics.Ray { Origin = localOrigin, Direction = localDir };
                foundHit = _bvh.Traverse(localRay, _triangles, out closestDist, out closestNormal, out trianglesTested);
            }
            else
            {
                // Fallback: brute force (for very small meshes < 10 triangles)
                foreach (var tri in _triangles)
                {
                    trianglesTested++;
                    if (RayTriangleIntersect(localOrigin, localDir, tri.V0, tri.V1, tri.V2, out float t, out Vector3 bary))
                    {
                        if (t >= 0 && t < closestDist)
                        {
                            closestDist = t;
                            foundHit = true;

                            var e1 = tri.V1 - tri.V0;
                            var e2 = tri.V2 - tri.V0;
                            closestNormal = Vector3.Cross(e1, e2).Normalized();
                        }
                    }
                }
            }

            // PERF FIX: Removed per-second raycast stats logging (caused FPS drops)
            // Debug stats
            // _totalTrianglesTestedThisFrame += trianglesTested;
            // if (_frameTimer.ElapsedMilliseconds > 1000)
            // {
            //     float reductionPercent = _triangles.Count > 0 ? (1f - (float)trianglesTested / _triangles.Count) * 100f : 0f;
            //     Console.WriteLine($"[MeshCollider] {_totalRaycastsThisFrame} raycasts/sec, {_totalTrianglesTestedThisFrame:N0} triangles tested/sec (BVH reduced by {reductionPercent:F1}%)");
            //     _totalRaycastsThisFrame = 0;
            //     _totalTrianglesTestedThisFrame = 0;
            //     _frameTimer.Restart();
            // }

            if (foundHit)
            {
                // Reconvertir en espace monde
                Vector3 closestPoint = localOrigin + localDir * closestDist;
                closestPoint *= wscl;
                closestPoint = Vector3.Transform(closestPoint, wrot) + wpos;

                // Transformer la normale
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
                    // PERF FIX: Removed log (called on every mesh load)
                }
            }

            // 2. Sinon, utiliser le MeshGuid spécifié
            if (targetGuid == Guid.Empty && MeshGuid.HasValue && MeshGuid.Value != Guid.Empty)
            {
                targetGuid = MeshGuid.Value;
            }

            if (targetGuid == Guid.Empty)
            {
                // PERF FIX: Removed log (called on every collision check attempt)
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

                    // PERF FIX: Removed per-load logging
                    if (!File.Exists(meshAssetPath))
                    {
                        // Mesh asset file not found - silently fail
                        return;
                    }

                    meshAsset = MeshAsset.Load(meshAssetPath);
                }
                catch
                {
                    // PERF FIX: Removed exception logging (happens during normal operation)
                    return;
                }
            }
            else
            {
                // PERF FIX: Removed "not found" logging
                return;
            }

            if (meshAsset == null)
            {
                // PERF FIX: Removed null mesh logging
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

                    _triangles.Add(new Physics.Triangle
                    {
                        V0 = v0,
                        V1 = v1,
                        V2 = v2
                    });
                }
            }

            _trianglesCached = _triangles.Count > 0;
            _bvhBuilt = false; // Need to rebuild BVH

            // PERF FIX: Removed success/warning logging (called on every mesh load)
        }

        /// <summary>
        /// Build BVH acceleration structure for fast raycasting
        /// Uses cache to avoid rebuilding on every load
        /// </summary>
        private void BuildBVH()
        {
            if (_triangles.Count == 0)
            {
                _bvhBuilt = false;
                return;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            _bvh = new Physics.BVH();
            _bvh.Build(_triangles);
            _bvhBuilt = true;
            sw.Stop();

            // PERF FIX: Removed BVH build logging (happens on every mesh load)
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
            _bvhBuilt = false;
            CacheTriangles();
            UpdateWorldBounds();
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
