using System;
using OpenTK.Mathematics;
using Engine.Physics;
using Engine.Serialization;

namespace Engine.Components
{
    /// <summary>
    /// Modern character controller with two modes: Kinematic (manual control) and Physics (rigidbody).
    ///
    /// KINEMATIC MODE:
    /// - Manual movement via Move() method
    /// - Capsule-based collision detection
    /// - Ground detection, slope handling, step-up
    /// - Gravity applied manually
    /// - Perfect for player controllers
    ///
    /// PHYSICS MODE (Future - requires BulletSharp):
    /// - Full rigidbody simulation
    /// - Forces, impulses, mass, friction
    /// - Physics constraints
    /// - Perfect for NPCs and physics-driven characters
    ///
    /// USAGE:
    /// var cc = entity.AddComponent&lt;CharacterController&gt;();
    /// cc.Mode = CharacterControllerMode.Kinematic;
    /// cc.Move(inputVector * speed * Time.DeltaTime);
    /// if (cc.IsGrounded && Input.Jump) cc.Jump(jumpForce);
    /// </summary>
    public class CharacterController : Component
    {
        // ===== MODE SELECTION =====

        /// <summary>Controller mode - Kinematic or Physics</summary>
        [Engine.Serialization.SerializableAttribute("mode")]
        public CharacterControllerMode Mode { get; set; } = CharacterControllerMode.Kinematic;

        // ===== SHAPE CONFIGURATION =====

        /// <summary>Capsule height (total height including hemispheres)</summary>
        [Engine.Serialization.SerializableAttribute("height")]
        public float Height
        {
            get => _height;
            set => _height = MathF.Max(Radius * 2.0f + 0.001f, value);
        }
        private float _height = 2.0f;

        /// <summary>Capsule radius</summary>
        [Engine.Serialization.SerializableAttribute("radius")]
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = MathF.Max(0.001f, value);
                if (_height < _radius * 2.0f)
                    _height = _radius * 2.0f + 0.001f;
            }
        }
        private float _radius = 0.5f;

        /// <summary>Center offset from entity position</summary>
        [Engine.Serialization.SerializableAttribute("center")]
        public Vector3 Center { get; set; } = new Vector3(0, 1.0f, 0);

        // ===== COLLISION SETTINGS =====

        /// <summary>Collision layer of this controller</summary>
        [Engine.Serialization.SerializableAttribute("layer")]
        public int Layer { get; set; } = CollisionLayers.Player;

        /// <summary>Layers this controller can collide with</summary>
        [Engine.Serialization.SerializableAttribute("collisionMask")]
        public int CollisionMask { get; set; } = ~0; // Collide with everything by default

        // ===== KINEMATIC MOVEMENT SETTINGS =====

        /// <summary>Maximum slope angle in degrees that can be walked on</summary>
        [Engine.Serialization.SerializableAttribute("slopeLimit")]
        public float SlopeLimit { get; set; } = 45f;

        /// <summary>Maximum height the controller can step up automatically</summary>
        [Engine.Serialization.SerializableAttribute("stepHeight")]
        public float StepHeight { get; set; } = 0.3f;

        /// <summary>Collision margin to prevent penetration</summary>
        [Engine.Serialization.SerializableAttribute("skinWidth")]
        public float SkinWidth { get; set; } = 0.02f;

        /// <summary>Enable gravity in kinematic mode</summary>
        [Engine.Serialization.SerializableAttribute("enableGravity")]
        public bool EnableGravity { get; set; } = true;

        /// <summary>Gravity vector (default: -20 on Y axis)</summary>
        [Engine.Serialization.SerializableAttribute("gravity")]
        public Vector3 Gravity { get; set; } = new Vector3(0, -20f, 0);

        // ===== STATE (READ-ONLY) =====

        /// <summary>Is the controller currently grounded?</summary>
        public bool IsGrounded { get; private set; } = false;

        /// <summary>Current velocity (affected by gravity, jumps, external forces)</summary>
        public Vector3 Velocity { get; private set; } = Vector3.Zero;

        /// <summary>Normal of the ground surface (if grounded)</summary>
        public Vector3 GroundNormal { get; private set; } = Vector3.UnitY;

        /// <summary>Distance to ground (0 if grounded)</summary>
        public float GroundDistance { get; private set; } = 0f;

        // ===== INTERNAL CONSTANTS =====

        private const int MaxBounces = 4; // Max collision resolution iterations
        private const float MinMoveDistance = 0.001f; // Ignore tiny movements
        private const float GroundCheckDistance = 0.1f; // How far to check for ground

        // ===== COMPONENT LIFECYCLE =====

        public override void FixedUpdate(float deltaTime)
        {
            base.FixedUpdate(deltaTime);

            if (Entity == null || Mode != CharacterControllerMode.Kinematic)
                return;

            // Apply gravity
            if (EnableGravity && !IsGrounded)
            {
                Velocity += Gravity * deltaTime;
            }

            // Apply velocity-based movement
            if (Velocity.LengthSquared > MinMoveDistance * MinMoveDistance)
            {
                Move(Velocity * deltaTime);
            }

            // Decay velocity when grounded (friction)
            if (IsGrounded)
            {
                // Keep only horizontal velocity, zero out vertical
                Velocity = new Vector3(Velocity.X * 0.9f, 0f, Velocity.Z * 0.9f);
            }
        }

        // ===== PUBLIC API - KINEMATIC MODE =====

        /// <summary>
        /// Move the character by a displacement vector with collision detection (Kinematic mode only).
        /// This is the main movement method - use this instead of directly modifying transform.
        /// </summary>
        /// <param name="motion">Desired movement in world space</param>
        public void Move(Vector3 motion)
        {
            if (Entity == null) return;
            if (Mode != CharacterControllerMode.Kinematic) return;
            if (motion.LengthSquared < MinMoveDistance * MinMoveDistance) return;

            // 1. Check ground before moving (but skip if we're jumping upward to prevent canceling the jump)
            if (Velocity.Y <= 0.1f) // Only check ground if not jumping upward
            {
                CheckGround();
            }

            // 2. Move with collision resolution

            Vector3 finalPosition = Entity.Transform.Position;
            Vector3 remainingMotion = motion;

            for (int bounce = 0; bounce < MaxBounces && remainingMotion.LengthSquared > MinMoveDistance * MinMoveDistance; bounce++)
            {
                // Try to move
                if (!TryMove(finalPosition, remainingMotion, out Vector3 newPosition, out Vector3 hitNormal, out float travelDistance))
                {
                    // No collision - move full distance
                    finalPosition = newPosition;
                    break;
                }

                // Collision occurred - slide along surface
                finalPosition = newPosition;

                // Calculate remaining motion after slide
                float remainingDistance = remainingMotion.Length * (1.0f - travelDistance);
                Vector3 slideDirection = Vector3.Normalize(remainingMotion - Vector3.Dot(remainingMotion, hitNormal) * hitNormal);
                remainingMotion = slideDirection * remainingDistance;

                // If sliding up a steep slope, stop
                float slopeAngle = Vector3.CalculateAngle(Vector3.UnitY, hitNormal) * (180f / MathF.PI);
                if (slopeAngle > SlopeLimit)
                {
                    break;
                }
            }

            // 3. Apply final position
            Entity.Transform.Position = finalPosition;

            // 4. Check ground after moving (but skip if we're jumping upward to prevent canceling the jump)
            if (Velocity.Y <= 0.1f) // Only check ground if not jumping upward
            {
                CheckGround();

                // 5. Snap to ground if on slope
                if (IsGrounded && motion.Y <= 0)
                {
                    SnapToGround();
                }
            }
        }

        /// <summary>
        /// Set the character's velocity (for jumps, knockback, etc.)
        /// </summary>
        public void SetVelocity(Vector3 velocity)
        {
            Velocity = velocity;
        }

        /// <summary>
        /// Add to the character's velocity (accumulative forces)
        /// </summary>
        public void AddVelocity(Vector3 velocity)
        {
            Velocity += velocity;
        }

        /// <summary>
        /// Perform a simple jump with the given force
        /// </summary>
        public void Jump(float jumpForce)
        {
            if (!IsGrounded || Mode != CharacterControllerMode.Kinematic)
                return;

            Velocity = new Vector3(Velocity.X, jumpForce, Velocity.Z);
            IsGrounded = false;
        }

        // ===== INTERNAL METHODS - KINEMATIC MODE =====

        /// <summary>
        /// Try to move from current position by motion vector.
        /// Returns true if collision occurred.
        /// </summary>
        private bool TryMove(Vector3 currentPos, Vector3 motion, out Vector3 newPosition, out Vector3 hitNormal, out float travelDistance)
        {
            newPosition = currentPos + motion;
            hitNormal = Vector3.UnitY;
            travelDistance = 1.0f;

            float distance = motion.Length;
            if (distance < MinMoveDistance)
                return false;

            Vector3 direction = motion / distance;

            // Cast capsule along motion
            if (CapsuleCast(currentPos, direction, distance + SkinWidth, out RaycastHit hit))
            {
                // IMPORTANT: Ignore ground collisions when moving horizontally or upward
                // This prevents the character from getting stuck on the ground it's standing on
                bool isGroundHit = hit.Normal.Y > 0.7f; // Hit normal is mostly upward (ground)
                bool isHorizontalOrUpwardMovement = motion.Y >= -0.001f; // Moving horizontally or upward (not falling)

                if (isGroundHit && isHorizontalOrUpwardMovement && hit.Distance < SkinWidth * 2f)
                {
                    // Ignore this ground collision - we're standing on it or jumping away from it
                    return false; // No collision - proceed with full movement
                }

                // Hit something - move to hit point (minus skin width)
                float safeDistance = MathF.Max(0, hit.Distance - SkinWidth);
                newPosition = currentPos + direction * safeDistance;
                hitNormal = hit.Normal;
                travelDistance = safeDistance / distance;
                return true;
            }

            // No hit - safe to move
            return false;
        }

        /// <summary>
        /// Cast the character's capsule shape along a direction.
        /// Approximated using sphere sweep at multiple heights.
        /// </summary>
        private bool CapsuleCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            // Capsule is approximated by 3 sphere sweeps at different heights
            // For a capsule: total height includes the two hemisphere caps
            // Cylinder height = Height - 2*Radius
            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;

            // Bottom sphere, middle sphere, top sphere (relative to Center)
            Vector3 capsuleCenter = origin + Center;
            Vector3 bottom = capsuleCenter - Vector3.UnitY * halfCylinderHeight;
            Vector3 middle = capsuleCenter;
            Vector3 top = capsuleCenter + Vector3.UnitY * halfCylinderHeight;

            RaycastHit? closestHit = null;
            float closestDistance = float.MaxValue;

            // Check bottom sphere
            if (PhysicsManager.Instance.SphereCast(bottom, Radius, direction, out RaycastHit bottomHit, distance, CollisionMask))
            {
                if (bottomHit.Distance < closestDistance)
                {
                    closestDistance = bottomHit.Distance;
                    closestHit = bottomHit;
                }
            }

            // Check middle sphere
            if (PhysicsManager.Instance.SphereCast(middle, Radius, direction, out RaycastHit middleHit, distance, CollisionMask))
            {
                if (middleHit.Distance < closestDistance)
                {
                    closestDistance = middleHit.Distance;
                    closestHit = middleHit;
                }
            }

            // Check top sphere
            if (PhysicsManager.Instance.SphereCast(top, Radius, direction, out RaycastHit topHit, distance, CollisionMask))
            {
                if (topHit.Distance < closestDistance)
                {
                    closestDistance = topHit.Distance;
                    closestHit = topHit;
                }
            }

            if (closestHit.HasValue)
            {
                hit = closestHit.Value;
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// Check if character is grounded
        /// </summary>
        private void CheckGround()
        {
            if (Entity == null)
            {
                IsGrounded = false;
                return;
            }

            // Start from the bottom sphere center of the capsule
            Vector3 origin = GetBottomPosition();
            float checkDistance = GroundCheckDistance + SkinWidth;

            if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, -Vector3.UnitY, out RaycastHit hit, checkDistance, CollisionMask))
            {
                float slopeAngle = Vector3.CalculateAngle(Vector3.UnitY, hit.Normal) * (180f / MathF.PI);

                if (slopeAngle <= SlopeLimit)
                {
                    IsGrounded = true;
                    GroundNormal = hit.Normal;
                    GroundDistance = hit.Distance;
                    return;
                }
            }

            IsGrounded = false;
            GroundNormal = Vector3.UnitY;
            GroundDistance = float.MaxValue;
        }

        /// <summary>
        /// Snap character to ground when on slopes (prevents bouncing)
        /// </summary>
        private void SnapToGround()
        {
            if (Entity == null || !IsGrounded)
                return;

            // Start from the bottom sphere center of the capsule
            Vector3 origin = GetBottomPosition();
            float snapDistance = StepHeight;

            if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, -Vector3.UnitY, out RaycastHit hit, snapDistance, CollisionMask))
            {
                if (hit.Distance > SkinWidth)
                {
                    // Snap down
                    Entity.Transform.Position -= Vector3.UnitY * (hit.Distance - SkinWidth);
                }
            }
        }

        // ===== HELPER METHODS =====

        /// <summary>Get the capsule's bottom sphere center position (lowest point of cylinder + radius below)</summary>
        public Vector3 GetBottomPosition()
        {
            if (Entity == null) return Vector3.Zero;

            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
            return Entity.Transform.Position + Center - Vector3.UnitY * halfCylinderHeight;
        }

        /// <summary>Get the capsule's center position</summary>
        public Vector3 GetCenterPosition()
        {
            if (Entity == null) return Vector3.Zero;

            return Entity.Transform.Position + Center;
        }

        /// <summary>Get the capsule's top sphere center position (highest point of cylinder + radius above)</summary>
        public Vector3 GetTopPosition()
        {
            if (Entity == null) return Vector3.Zero;

            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
            return Entity.Transform.Position + Center + Vector3.UnitY * halfCylinderHeight;
        }

        /// <summary>Get the absolute bottom position (including the bottom hemisphere)</summary>
        public Vector3 GetAbsoluteBottomPosition()
        {
            return GetBottomPosition() - Vector3.UnitY * Radius;
        }

        /// <summary>Get the absolute top position (including the top hemisphere)</summary>
        public Vector3 GetAbsoluteTopPosition()
        {
            return GetTopPosition() + Vector3.UnitY * Radius;
        }
    }

    /// <summary>
    /// Character controller mode selection
    /// </summary>
    public enum CharacterControllerMode
    {
        /// <summary>Manual kinematic control with collision detection</summary>
        Kinematic = 0,

        /// <summary>Full physics simulation with rigidbody (requires BulletSharp - future)</summary>
        Physics = 1
    }
}
