using Engine.Serialization;
using Engine.Scene;
using Engine.Physics;
using OpenTK.Mathematics;

namespace Engine.Components
{
    public abstract class Collider : Component
    {
    [Engine.Serialization.Serializable("isTrigger")]
        public bool IsTrigger = false;

    [Engine.Serialization.Serializable("layer")]
        public int Layer = 0; // 0..31

    [Engine.Serialization.Serializable("center")]
        public Vector3 Center = Vector3.Zero; // local center

        // Cached world-space AABB (for broadphase)
        public Bounds WorldAABB { get; internal set; }

        public abstract OBB GetWorldOBB();

        /// <summary>
        /// Optional narrow-phase raycast: colliders can override to provide accurate ray hits beyond AABB test.
        /// Default implementation returns false.
        /// </summary>
        public virtual bool Raycast(Engine.Physics.Ray ray, out Engine.Physics.RaycastHit hit)
        {
            hit = default;
            return false;
        }

        public override void OnAttached()
        {
            base.OnAttached();
            Physics.CollisionSystem.Register(this);
            UpdateWorldBounds();
        }

        public override void OnDetached()
        {
            Physics.CollisionSystem.Unregister(this);
            base.OnDetached();
        }

        public override void OnEnable()
        {
            Physics.CollisionSystem.MarkDirty(this);
        }

        public override void OnDisable()
        {
            Physics.CollisionSystem.MarkDirty(this);
        }

        internal void UpdateWorldBounds()
        {
            var obb = GetWorldOBB();
            WorldAABB = Physics.CollisionSystem.ComputeAABB(obb);
        }
    }
}
