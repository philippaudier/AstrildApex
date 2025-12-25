using System;
using OpenTK.Mathematics;
using Engine.Components;
using Engine.Serialization;
using Engine.Core;

namespace Engine.Physics
{
    /// <summary>
    /// Kinematic character controller for player movement with collision detection.
    /// Simple, efficient, and easy to extend.
    ///
    /// FEATURES:
    /// - Collision-aware movement (Move() method)
    /// - Ground detection with slope handling
    /// - Step-up for stairs
    /// - Gravity application
    /// - Velocity-based physics (jump, knockback, etc.)
    ///
    /// USAGE:
    /// var cc = entity.AddComponent&lt;KinematicCharacterController&gt;();
    /// cc.Move(inputVector * speed * Time.FixedDeltaTime);
    /// if (cc.IsGrounded && Input.Jump) cc.SetVelocity(Vector3.UnitY * jumpForce);
    ///
    /// INSPIRED BY: Unity CharacterController, Unreal CharacterMovementComponent
    /// </summary>
    public sealed class KinematicCharacterController : Component
    {
        // ===== SHAPE CONFIGURATION =====

        [Engine.Serialization.SerializableAttribute("height")]
        private float _height = 2.0f;

        [Engine.Serialization.SerializableAttribute("radius")]
        private float _radius = 0.5f;

        /// <summary>Capsule height (total height including caps)</summary>
        public float Height
        {
            get => _height;
            set => _height = MathF.Max(Radius * 2.0f + 0.001f, value);
        }

        /// <summary>Capsule radius</summary>
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

        // ===== COLLISION SETTINGS =====

        [Engine.Serialization.SerializableAttribute("layer")]
        public int Layer = CollisionLayers.Player;

        [Engine.Serialization.SerializableAttribute("collisionMask")]
        public int CollisionMask = ~0; // Collide with everything by default

        [Engine.Serialization.SerializableAttribute("skinWidth")]
        public float SkinWidth = 0.02f; // Collision margin

        // ===== MOVEMENT SETTINGS =====

        [Engine.Serialization.SerializableAttribute("slopeLimit")]
        public float SlopeLimit = 45f; // Max angle in degrees for walkable slopes

        [Engine.Serialization.SerializableAttribute("stepHeight")]
        public float StepHeight = 0.3f; // Max height for auto step-up

        [Engine.Serialization.SerializableAttribute("gravity")]
        public Vector3 Gravity = new Vector3(0, -20f, 0);

        [Engine.Serialization.SerializableAttribute("enableGravity")]
        public bool EnableGravity = true;

        // ===== STATE (READ-ONLY) =====

        /// <summary>Is the controller currently grounded?</summary>
        public bool IsGrounded { get; private set; } = false;

        /// <summary>Current velocity (affected by gravity, jumps, etc.)</summary>
        public Vector3 Velocity { get; private set; } = Vector3.Zero;

        /// <summary>Normal of the ground surface (if grounded)</summary>
        public Vector3 GroundNormal { get; private set; } = Vector3.UnitY;

        /// <summary>Distance to ground (0 if grounded)</summary>
        public float GroundDistance { get; private set; } = 0f;

        // ===== INTERNAL STATE =====

        private const int MaxBounces = 4; // Max collision resolution iterations
        private const float MinMoveDistance = 0.001f; // Ignore tiny movements
        private const float GroundCheckDistance = 0.1f; // How far to check for ground

        // ===== COMPONENT LIFECYCLE =====

        public override void FixedUpdate(float deltaTime)
        {
            base.FixedUpdate(deltaTime);

            if (Entity == null) return;

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

        // ===== PUBLIC API =====

        /// <summary>
        /// Move the character controller by a displacement vector with collision detection.
        /// This is the main movement method - use this instead of directly modifying transform.
        /// </summary>
        /// <param name="motion">Desired movement in world space</param>
        public void Move(Vector3 motion)
        {
            if (Entity == null) return;
            if (motion.LengthSquared < MinMoveDistance * MinMoveDistance) return;

            // 1. Check ground before moving
            CheckGround();

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

            // 4. Check ground after moving
            CheckGround();

            // 5. Snap to ground if on slope
            if (IsGrounded && motion.Y <= 0)
            {
                SnapToGround();
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
            if (!IsGrounded) return;
            Velocity = new Vector3(Velocity.X, jumpForce, Velocity.Z);
            IsGrounded = false;
        }

        // ===== INTERNAL METHODS =====

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
            {
                return false;
            }

            Vector3 direction = motion / distance;

            // Cast capsule along motion
            if (CapsuleCast(currentPos, direction, distance + SkinWidth, out RaycastHit hit))
            {
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
        /// Cast the character's capsule shape along a direction
        /// </summary>
        private bool CapsuleCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            // Simple sphere sweep approximation (can be improved with multiple sphere samples)
            Vector3 sphereCenter = origin + Vector3.UnitY * (Height * 0.5f - Radius);
            return PhysicsManager.Instance.SphereCast(sphereCenter, Radius, direction, out hit, distance, CollisionMask);
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

            Vector3 origin = Entity.Transform.Position + Vector3.UnitY * (Radius + 0.01f);
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
            if (Entity == null || !IsGrounded) return;

            Vector3 origin = Entity.Transform.Position + Vector3.UnitY * (Radius + 0.01f);
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

        /// <summary>
        /// Get the capsule's bottom position
        /// </summary>
        public Vector3 GetBottomPosition()
        {
            if (Entity == null) return Vector3.Zero;
            return Entity.Transform.Position;
        }

        /// <summary>
        /// Get the capsule's center position
        /// </summary>
        public Vector3 GetCenterPosition()
        {
            if (Entity == null) return Vector3.Zero;
            return Entity.Transform.Position + Vector3.UnitY * (Height * 0.5f);
        }

        /// <summary>
        /// Get the capsule's top position
        /// </summary>
        public Vector3 GetTopPosition()
        {
            if (Entity == null) return Vector3.Zero;
            return Entity.Transform.Position + Vector3.UnitY * Height;
        }
    }
}
