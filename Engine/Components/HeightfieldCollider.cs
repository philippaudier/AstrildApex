using Engine.Serialization;
using Engine.Scene;
using Engine.Physics;
using OpenTK.Mathematics;

namespace Engine.Components
{
    /// <summary>
    /// Simple heightfield collider that samples a Terrain on the same entity (or global) to compute collisions/raycast.
    /// For now it only implements AABB bounds and is used by the broadphase and raycasts via CollisionSystem (AABB-level).
    /// Narrow-phase raycast helpers are provided by this component for custom queries.
    /// </summary>
    public sealed class HeightfieldCollider : Collider
    {
    [Engine.Serialization.Serializable("terrainGenerator")]
    public Terrain? TerrainRef = null; // Optional direct reference (remaps on scene clone)

        // Cached mesh resolution used by the collider so we can adapt sampling and any internal data
        [Engine.Serialization.Serializable("meshResolution")]
        public int MeshResolution = 0;

        // Cached world-space spacing between terrain samples (terrain width / (resolution-1)).
        // Updated whenever the terrain is regenerated or resolution changes.
        private float _sampleSpacing = 1f;

        public override void OnAttached()
        {
            base.OnAttached();
            UpdateWorldBounds();
        }

        public override void Update(float deltaTime)
        {
            // Nothing dynamic; but update bounds if transform changes
            UpdateWorldBounds();

            // If the terrain resolution changed (e.g. user regenerated terrain with a different MeshResolution),
            // sync our cached resolution and sampling spacing so raycasts / normals use the correct scale.
            Terrain? tg = TerrainRef ?? (Entity != null ? Entity.GetComponent<Terrain>() : null);
            if (tg != null)
            {
                if (tg.MeshResolution != MeshResolution)
                    SyncResolutionWithTerrain(tg);
            }
        }

        /// <summary>
        /// Synchronize collider internal parameters with the provided Terrain instance.
        /// This updates the cached mesh resolution and the sample spacing used for normal computation.
        /// Call this after the terrain has been regenerated.
        /// </summary>
        public void SyncResolutionWithTerrain(Terrain tg)
        {
            if (tg == null) return;
            int res = Math.Max(2, tg.MeshResolution);
            MeshResolution = res;
            _sampleSpacing = MathF.Abs(tg.TerrainWidth) / MathF.Max(1, res - 1);
            Console.WriteLine($"[HeightfieldCollider] Synced to terrain resolution {MeshResolution}, sampleSpacing={_sampleSpacing}");
        }

        public override OBB GetWorldOBB()
        {
            // We provide an AABB covering the terrain area for broadphase; narrow-phase uses sampling.
            var e = Entity;
            if (e == null) return new OBB { Center = Vector3.Zero, HalfSize = Vector3.Zero, Orientation = Matrix3.Identity };

            // Center at entity world position
            e.GetWorldTRS(out var wpos, out var wrot, out var wscl);

            // Try to get actual terrain dimensions if available
            Terrain? tg = TerrainRef ?? (Entity != null ? Entity.GetComponent<Terrain>() : null);
            Vector3 half;
            Vector3 center = wpos;

            if (tg != null)
            {
                // Use actual terrain dimensions
                float halfWidth = MathF.Abs(tg.TerrainWidth) * 0.5f * MathF.Abs(wscl.X);
                float halfLength = MathF.Abs(tg.TerrainLength) * 0.5f * MathF.Abs(wscl.Z);
                float halfHeight = MathF.Abs(tg.TerrainHeight) * 0.5f * MathF.Abs(wscl.Y);
                half = new Vector3(halfWidth, halfHeight, halfLength);

                // IMPORTANT: Terrain heights range from 0 to TerrainHeight (not centered at entity position)
                // So we need to offset the center upward by halfHeight
                center = wpos + new Vector3(0, halfHeight, 0);
            }
            else
            {
                // Fallback: use a large extents
                half = new Vector3(10000f) * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
            }

            return new OBB { Center = center, HalfSize = half, Orientation = Matrix3.Identity };
        }

        /// <summary>
        /// Sample the terrain height at world coordinates by finding the Terrain instance to consult.
        /// Returns true if a terrain generator was found and height set.
        /// </summary>
        public bool TrySampleHeight(float worldX, float worldZ, out float height)
        {
            Terrain? tg = null;
            if (TerrainRef != null) tg = TerrainRef;
            else if (Entity != null) tg = Entity.GetComponent<Terrain>();
            if (tg == null) { height = 0f; return false; }
            height = tg.SampleHeight(worldX, worldZ);
            return true;
        }

        public override bool Raycast(Engine.Physics.Ray ray, out Engine.Physics.RaycastHit hit)
        {
            hit = default;
            // Resolve Terrain reference
            Terrain? tg = TerrainRef ?? (Entity != null ? Entity.GetComponent<Terrain>() : null);
            if (tg == null) return false;

            // Ray-march: find t where ray.y crosses terrain height at ray.xz
            float t = 0f;
            float step = 0.5f;
            float maxT = 1000f;
            for (int iter = 0; iter < 200 && t <= maxT; iter++)
            {
                var p = ray.Origin + ray.Direction * t;
                float h = tg.SampleHeight(p.X, p.Z);
                if (p.Y <= h)
                {
                    // found intersection region between t-step and t; refine with binary search
                    float a = MathF.Max(0, t - step);
                    float b = t;
                    for (int k = 0; k < 8; k++)
                    {
                        float m = (a + b) * 0.5f;
                        var pm = ray.Origin + ray.Direction * m;
                        float hm = tg.SampleHeight(pm.X, pm.Z);
                        if (pm.Y <= hm) b = m; else a = m;
                    }
                    float tf = (a + b) * 0.5f;
                    var pf = ray.Origin + ray.Direction * tf;
                    float hf = tg.SampleHeight(pf.X, pf.Z);
                    // Compute normal by sampling small offsets around hit
                    // Compute a safe sampling epsilon based on terrain resolution and size.
                    // Use the terrain spacing between vertices as baseline: terrainWidth / (meshResolution-1)
                    // Use cached spacing when available to avoid computing every raycast.
                    float sampleSpacing = _sampleSpacing;
                    if (sampleSpacing <= 0f)
                    {
                        try
                        {
                            sampleSpacing = MathF.Abs(tg.TerrainWidth) / MathF.Max(1, tg.MeshResolution - 1);
                        }
                        catch { sampleSpacing = 1f; }
                        _sampleSpacing = sampleSpacing;
                        MeshResolution = Math.Max(2, tg.MeshResolution);
                    }
                    float eps = MathF.Max(0.1f, sampleSpacing);
                    float hx = tg.SampleHeight(pf.X + eps, pf.Z) - tg.SampleHeight(pf.X - eps, pf.Z);
                    float hz = tg.SampleHeight(pf.X, pf.Z + eps) - tg.SampleHeight(pf.X, pf.Z - eps);
                    var n = new OpenTK.Mathematics.Vector3(-hx, 2f * eps, -hz);
                    if (n.LengthSquared > 1e-6f) n.Normalize(); else n = new OpenTK.Mathematics.Vector3(0,1,0);
                    hit = new Engine.Physics.RaycastHit { ColliderComponent = this, Component = this, Entity = Entity, Point = pf, Normal = n, Distance = tf };
                    return true;
                }
                t += step;
                // accelerate step if far away
                if (t > 10f) step = 1.0f;
            }
            return false;
        }
    }
}
