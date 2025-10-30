using System;
using OpenTK.Mathematics;
using Engine.Physics;
using Engine.Inspector;
using Engine.Serialization;

namespace Engine.Components
{
    /// <summary>
    /// Contrôleur de personnage simple et fonctionnel
    /// Gère le déplacement, la gravité et les collisions de base
    /// </summary>
    public sealed class CharacterController : Component
    {
        // Configuration
        [Serialization.Serializable("height")]
        public float Height = 1.8f;

        [Serialization.Serializable("radius")]
        public float Radius = 0.35f;

        [Serialization.Serializable("stepOffset")]
        public float StepOffset = 0.3f;

        [Serialization.Serializable("gravity")]
        public float Gravity = 9.81f;

        [Serialization.Serializable("groundCheckDistance")]
        public float GroundCheckDistance = 3.0f;

        [Serialization.Serializable("skinWidth")]
        public float SkinWidth = 0.02f;

        [Serialization.Serializable("groundOffset")][Editable]
        public float GroundOffset = 0.0f;  // Set to 0 - capsule bottom should be exactly on ground

        // Debug
        [Serialization.Serializable("debugPhysics")] [Editable]
        public bool DebugPhysics = false;

        // État public
        public bool IsGrounded { get; private set; } = false;
        public Vector3 Velocity { get; private set; } = Vector3.Zero;

        // État interne
        private Vector3 _velocity = Vector3.Zero;
        private bool _wasGroundedLastFrame = false;
    // When set >0, skip ground snapping for that many Update frames to avoid cancelling jump impulses
    private int _suppressSnapFrames = 0;
    [Serialization.Serializable("maxSlopeAngleDeg")] public float MaxSlopeAngleDeg = 45f;
    [Serialization.Serializable("maxClimbPerFrame")] public float MaxClimbPerFrame = 0.5f;
    [Serialization.Serializable("climbSmoothSpeed")][Editable]
    public float ClimbSmoothSpeed = 6f; // meters per second for smoothing climbs

    [Serialization.Serializable("descendSmoothSpeed")][Editable]
    public float DescendSmoothSpeed = 12f; // meters per second for smoothing descents

    [Serialization.Serializable("snapEpsilon")][Editable]
    public float SnapEpsilon = 0.02f; // when within this distance, consider snapped

        /// <summary>Déplace le contrôleur. dt is the frame delta time (seconds).</summary>
        public void Move(Vector3 motion, float dt)
        {
            if (Entity?.Transform == null) return;

            // Debug disabled: if (DebugPhysics)
            //     Console.WriteLine($"[CharacterController] Move called with: {motion:F3}");


            // Séparer mouvement horizontal et vertical
            var horizontalMotion = new Vector3(motion.X, 0, motion.Z);
            var verticalMotion = motion.Y;

            // Le mouvement vertical dans Move() ne devrait PAS s'additionner - ignorer complètement
            // La vélocité verticale est gérée uniquement par la gravité et AddVerticalImpulse
            // Si un script veut forcer un mouvement vertical, il doit utiliser AddVerticalImpulse()

            // Seulement traiter les impulsions significatives (sauts)
            if (verticalMotion > 1e-5f)
            {
                _velocity.Y += verticalMotion; // Additionner uniquement les impulsions positives (sauts)
                _suppressSnapFrames = 3;
                IsGrounded = false;
            }
            // Ignorer les mouvements verticaux négatifs ou nuls passés à Move()

            // Déplacement horizontal direct (simplifié)
            if (horizontalMotion.LengthSquared > 0.001f)
            {
                ApplyHorizontalMovement(horizontalMotion, dt);
            }
        }

        /// <summary>
        /// Apply an instantaneous vertical impulse (e.g. a jump). This updates the internal velocity and
        /// ensures ground snapping is suppressed for a few frames.
        /// </summary>
        public void AddVerticalImpulse(float impulse)
        {
            if (Entity?.Transform == null) return;

            if (DebugPhysics) Console.WriteLine($"[CharacterController] AddVerticalImpulse called: {impulse:F3}");


            _velocity.Y += impulse;
            _suppressSnapFrames = 6;
            IsGrounded = false;
        }

        public override void Update(float dt)
        {
            if (Entity?.Transform == null || dt <= 0) return;

            // Appliquer la gravité si pas au sol
            if (!IsGrounded)
            {
                _velocity.Y -= Gravity * dt;
            }

            // Appliquer la vitesse verticale
            if (MathF.Abs(_velocity.Y) > 0.001f)
            {
                ApplyVerticalMovement(_velocity.Y * dt);
            }

            // Mettre à jour la vélocité publique
            Velocity = _velocity;


            // Clear per-frame suppression after update
            if (_suppressSnapFrames > 0)
            {
                _suppressSnapFrames--;
            }
        }

    private void ApplyHorizontalMovement(Vector3 horizontalMotion, float dt)
        {
            if (Entity?.Transform == null) return;

            var startPos = Entity.Transform.Position;
            var motionLength = horizontalMotion.Length;

            if (motionLength < 0.001f) return;

            // Perform collision-aware movement with sliding
            var finalMotion = ComputeSafeMovement(startPos, horizontalMotion, dt);
            Entity.Transform.Position = startPos + finalMotion;

            // Ground snapping for smooth slope following
            var afterPos = Entity.Transform.Position;

            // Check ground at current position AND slightly ahead for better descending
            bool groundHit = CheckGround(afterPos, out float groundY);

            // Also check ahead for descending slopes
            if (horizontalMotion.LengthSquared > 0.001f)
            {
                var ahead = afterPos + horizontalMotion.Normalized() * Radius;
                if (CheckGround(ahead, out float aheadGroundY))
                {
                    // Use the lower ground (important for descending)
                    if (aheadGroundY < groundY || !groundHit)
                    {
                        groundY = aheadGroundY;
                        groundHit = true;
                    }
                }
            }

            if (groundHit && _suppressSnapFrames == 0 && _velocity.Y <= 0.01f)
            {
                float halfHeight = Height * 0.5f;
                float characterBottom = afterPos.Y - halfHeight;
                float distanceToGround = characterBottom - groundY;

                // Consider grounded if within reasonable range
                // IMPORTANT: Never allow character to sink below ground (distanceToGround must be >= 0)
                if (distanceToGround <= GroundOffset + 0.5f && distanceToGround >= -0.05f) // Allow tiny tolerance for floating point
                {
                    IsGrounded = true;
                    _velocity.Y = 0f;

                    // Calculate correct ground position
                    float targetY = groundY + halfHeight + GroundOffset;
                    float currentY = afterPos.Y;
                    float yDiff = targetY - currentY;

                    // If sinking below ground, snap immediately (no smoothing)
                    if (distanceToGround < 0f)
                    {
                        afterPos.Y = targetY;
                    }
                    // Otherwise use smooth ground snapping to follow terrain
                    else
                    {
                        // Use different smoothing speeds for ascending vs descending
                        float smoothSpeed = yDiff > 0 ? ClimbSmoothSpeed : DescendSmoothSpeed;

                        // If very close, snap instantly; otherwise smooth
                        if (MathF.Abs(yDiff) < SnapEpsilon)
                        {
                            afterPos.Y = targetY;
                        }
                        else
                        {
                            float maxMove = smoothSpeed * dt;
                            float actualMove = MathHelper.Clamp(yDiff, -maxMove, maxMove);
                            afterPos.Y += actualMove;
                        }
                    }

                    Entity.Transform.Position = afterPos;
                }
                else if (distanceToGround < 0f)
                {
                    // Character is sinking into ground - force correct position immediately
                    IsGrounded = true;
                    _velocity.Y = 0f;
                    afterPos.Y = groundY + halfHeight + GroundOffset;
                    Entity.Transform.Position = afterPos;
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

        }

        private void ApplyVerticalMovement(float verticalDelta)
        {
            if (Entity?.Transform == null) return;

            var currentPos = Entity.Transform.Position;
            float halfHeight = Height * 0.5f;

            _wasGroundedLastFrame = IsGrounded;

            // NO CEILING COLLISION DETECTION

            var newY = currentPos.Y + verticalDelta;

            // Check for ground collision when moving down or stationary
            bool groundHit = CheckGround(new Vector3(currentPos.X, newY, currentPos.Z), out float groundY);

            if (groundHit)
            {
                float characterBottom = newY - halfHeight;

                // If falling and touching ground, snap to ground
                if (_velocity.Y <= 0 && characterBottom <= groundY + 0.01f)
                {
                    newY = groundY + halfHeight + GroundOffset;
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

            Entity.Transform.Position = new Vector3(currentPos.X, newY, currentPos.Z);
        }

        private bool CheckGround(Vector3 position, out float groundY)
        {
            groundY = 0f;

            float halfHeight = Height * 0.5f;
            float checkDistance = halfHeight + GroundCheckDistance;

            // Raycast from center of character downward
            var rayOrigin = position;
            var ray = new Ray { Origin = rayOrigin, Direction = Vector3.UnitY * -1 };

            var hits = Physics.CollisionSystem.RaycastAll(ray, checkDistance, ~0, QueryTriggerInteraction.Ignore);


            if (hits?.Count > 0)
            {
                // Trouver le hit le plus proche qui n'est pas nous-même
                RaycastHit? closestHit = null;
                float closestDistance = float.MaxValue;

                foreach (var hit in hits)
                {
                    if (hit.ColliderComponent?.Entity == Entity) continue; // Ignorer notre propre collider

                    if (hit.Distance < closestDistance)
                    {
                        closestDistance = hit.Distance;
                        closestHit = hit;
                    }
                }

                if (closestHit.HasValue)
                {
                    groundY = closestHit.Value.Point.Y;


                    return true;
                }
            }


            return false;
        }

        // Sample ground and return both ground Y and normal (from closest hit)
        private bool SampleGround(Vector3 position, out float groundY, out Vector3 groundNormal)
        {
            groundY = 0f;
            groundNormal = Vector3.UnitY;

            float halfHeight = Height * 0.5f;
            float checkDistance = halfHeight + GroundCheckDistance;

            // Raycast from center downward
            var rayOrigin = position;
            var ray = new Ray { Origin = rayOrigin, Direction = Vector3.UnitY * -1 };
            var hits = Physics.CollisionSystem.RaycastAll(ray, checkDistance, ~0, QueryTriggerInteraction.Ignore);

            if (hits?.Count > 0)
            {
                RaycastHit? closestHit = null;
                float closestDistance = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.ColliderComponent?.Entity == Entity) continue;
                    if (hit.Distance < closestDistance)
                    {
                        closestDistance = hit.Distance;
                        closestHit = hit;
                    }
                }

                if (closestHit.HasValue)
                {
                    groundY = closestHit.Value.Point.Y;
                    groundNormal = closestHit.Value.Normal;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Compute safe movement - NO COLLISION DETECTION.
        /// Just returns the desired motion as-is.
        /// </summary>
        private Vector3 ComputeSafeMovement(Vector3 startPos, Vector3 desiredMotion, float dt)
        {
            // NO COLLISION DETECTION - just move
            return desiredMotion;
        }

        /// <summary>
        /// Project a motion vector onto a plane defined by its normal.
        /// This removes the component of motion perpendicular to the plane.
        /// </summary>
        private Vector3 ProjectMotionOnPlane(Vector3 motion, Vector3 planeNormal)
        {
            float distance = Vector3.Dot(motion, planeNormal);
            return motion - planeNormal * distance;
        }
    }
}