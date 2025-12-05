using System;
using OpenTK.Mathematics;
using Engine.Physics;
using Engine.Inspector;
using Engine.Serialization;

namespace Engine.Components
{
    /// <summary>
    /// Simple and robust character controller for player movement.
    /// 
    /// ARCHITECTURE:
    /// - Gravity is applied every frame
    /// - Ground detection uses a single downward raycast
    /// - Horizontal movement uses CapsuleCast for collision detection
    /// - Vertical movement snaps to ground when falling onto it
    /// - Sliding along walls is handled by projecting motion onto collision normal
    /// 
    /// NO complex features:
    /// - No rotation alignment to ground normal
    /// - No multi-point ground sampling
    /// - No movement accumulator
    /// - No step-up system
    /// - No smooth climb/descend
    /// 
    /// This controller is SIMPLE, PREDICTABLE, and ROBUST.
    /// </summary>
    public sealed class CharacterController : Component
    {
        // === CONFIGURATION ===
        
        [Serialization.Serializable("height")]
        [Editable] public float Height = 1.8f;

        [Serialization.Serializable("radius")]
        [Editable] public float Radius = 0.35f;

        [Serialization.Serializable("gravity")]
        [Editable] public float Gravity = 9.81f;

        [Serialization.Serializable("groundCheckDistance")]
        [Editable] public float GroundCheckDistance = 0.1f;

        [Serialization.Serializable("skinWidth")]
        [Editable] public float SkinWidth = 0.02f;

        [Serialization.Serializable("slopeLimit")]
        [Editable] public float SlopeLimit = 45f; // Max walkable slope in degrees

        // === STATE ===
        
        public bool IsGrounded { get; private set; }
        public Vector3 Velocity { get; private set; }

        private Vector3 _velocity = Vector3.Zero;

        // === PUBLIC API ===

        /// <summary>
        /// Move the character by the specified motion vector.
        /// This should be called once per frame with your desired movement.
        /// </summary>
        public void Move(Vector3 motion)
        {
            if (Entity?.Transform == null) return;

            // Only apply horizontal motion through Move()
            // Vertical motion is handled by gravity and AddImpulse()
            Vector3 horizontalMotion = new Vector3(motion.X, 0, motion.Z);

            if (horizontalMotion.LengthSquared > 0.0001f)
            {
                ApplyHorizontalMovement(horizontalMotion);
            }
        }

        /// <summary>
        /// Add an instantaneous vertical impulse (e.g., for jumping).
        /// </summary>
        public void AddImpulse(float verticalImpulse)
        {
            _velocity.Y += verticalImpulse;
            IsGrounded = false;
        }

        // === INTERNAL UPDATE ===

        public override void FixedUpdate(float dt)
        {
            if (Entity?.Transform == null || dt <= 0) return;

            Vector3 position = Entity.Transform.Position;

            // 1. Apply gravity
            if (!IsGrounded)
            {
                _velocity.Y -= Gravity * dt;
            }

            // 2. Apply vertical velocity
            position.Y += _velocity.Y * dt;

            // 3. Check for ground
            bool hitGround = CheckGround(position, out float groundY, out Vector3 groundNormal);

            if (hitGround)
            {
                float characterBottom = position.Y - (Height * 0.5f);
                float distanceToGround = characterBottom - groundY;

                // If we're falling and hit the ground, snap to it
                if (_velocity.Y <= 0 && distanceToGround <= 0.05f)
                {
                    position.Y = groundY + (Height * 0.5f);
                    _velocity.Y = 0;
                    IsGrounded = true;
                }
                else
                {
                    IsGrounded = false;
                }
            }
            else
            {
                IsGrounded = false;
            }

            // 4. Update position
            Entity.Transform.Position = position;
            Velocity = _velocity;
        }

        // === GROUND DETECTION ===

        private bool CheckGround(Vector3 position, out float groundY, out Vector3 groundNormal)
        {
            groundY = 0f;
            groundNormal = Vector3.UnitY;

            float halfHeight = Height * 0.5f;
            float rayLength = halfHeight + GroundCheckDistance;

            // Cast ray from center downward
            Vector3 rayOrigin = position;
            Vector3 rayDirection = -Vector3.UnitY;

            if (Physics.Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayLength, 
                layerMask: ~0, query: QueryTriggerInteraction.Ignore))
            {
                // Ignore self
                if (hit.Entity == Entity)
                    return false;

                // Check if surface is walkable (slope limit)
                float normalAngle = Vector3.CalculateAngle(hit.Normal, Vector3.UnitY) * (180f / MathF.PI);
                if (normalAngle > SlopeLimit)
                    return false; // Too steep

                groundY = hit.Point.Y;
                groundNormal = hit.Normal;
                return true;
            }

            return false;
        }

        // === HORIZONTAL MOVEMENT ===

        private void ApplyHorizontalMovement(Vector3 motion)
        {
            if (Entity?.Transform == null) return;

            Vector3 startPosition = Entity.Transform.Position;
            float halfHeight = Height * 0.5f;

            // Capsule endpoints for collision detection
            Vector3 capsuleBottom = startPosition + Vector3.UnitY * (-halfHeight + Radius);
            Vector3 capsuleTop = startPosition + Vector3.UnitY * (halfHeight - Radius);

            Vector3 direction = motion.Normalized();
            float distance = motion.Length;

            // Use CapsuleCast to detect obstacles
            bool hitObstacle = Physics.CollisionSystem.CapsuleCast(
                capsuleBottom,
                capsuleTop,
                Radius,
                direction,
                distance + SkinWidth,
                out RaycastHit hit,
                layerMask: ~0,
                qti: QueryTriggerInteraction.Ignore
            );

            Vector3 finalMotion;

            if (hitObstacle && hit.Entity != Entity)
            {
                // Move as close as possible to obstacle
                float safeDistance = MathF.Max(0, hit.Distance - SkinWidth);
                Vector3 moveToContact = direction * safeDistance;

                // Calculate remaining motion after hitting the obstacle
                float remainingDistance = distance - safeDistance;
                
                if (remainingDistance > 0.001f)
                {
                    // Slide along the obstacle surface
                    Vector3 remainingMotion = direction * remainingDistance;
                    Vector3 slideMotion = ProjectOnPlane(remainingMotion, hit.Normal);

                    // Only slide horizontally (no climbing)
                    slideMotion.Y = Math.Clamp(slideMotion.Y, -0.1f, 0.1f);

                    finalMotion = moveToContact + slideMotion;
                }
                else
                {
                    finalMotion = moveToContact;
                }
            }
            else
            {
                // No obstacle - free movement
                finalMotion = motion;
            }

            // Apply horizontal movement only (preserve Y)
            Vector3 newPosition = startPosition;
            newPosition.X += finalMotion.X;
            newPosition.Z += finalMotion.Z;

            Entity.Transform.Position = newPosition;
        }

        // === HELPERS ===

        /// <summary>
        /// Project a vector onto a plane defined by its normal.
        /// </summary>
        private Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
        {
            float distance = Vector3.Dot(vector, planeNormal);
            return vector - planeNormal * distance;
        }

    }
}
