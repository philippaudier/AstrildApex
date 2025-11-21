using System;
using OpenTK.Mathematics;
using Engine.Physics;
using Engine.Inspector;
using Engine.Serialization;
using Engine.Utils;

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
        public float SkinWidth = 0.04f;

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

    [Serialization.Serializable("groundAlignSpeed")][Editable]
    public float GroundAlignSpeed = 12f; // how quickly the character's up aligns to ground normal

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

            var currentPos = Entity.Transform.Position;
            float halfHeight = Height * 0.5f;

            // Appliquer la gravité
            _velocity.Y -= Gravity * dt;

            // Vérifier le sol en échantillonnant plusieurs points pour lisser les transitions
            bool groundHit = SampleGroundAverage(currentPos, out float groundY, out Vector3 groundNormal);

            if (DebugPhysics)
            {
                Console.WriteLine($"[CC] Update - Pos: {currentPos.Y:F2}, VelY: {_velocity.Y:F2}, GroundHit: {groundHit}, GroundY: {groundY:F2}, IsGrounded: {IsGrounded}");
            }
            
            if (groundHit)
            {
                // groundY est la position du sol détecté
                float characterBottom = currentPos.Y - halfHeight;
                float distanceToGround = characterBottom - groundY;

                // targetY = la position Y centrale où le personnage doit se trouver pour être "sur" le sol
                float targetY = groundY + halfHeight + GroundOffset;

                // Si on est très près du sol ou en dessous et qu'on ne monte pas
                if (_suppressSnapFrames == 0 && distanceToGround <= SnapEpsilon && _velocity.Y <= 0)
                {
                    // Considéré comme au sol
                    IsGrounded = true;

                    // Lissage pour descendre/monter doucement sur des pentes
                    var pos = Entity.Transform.Position;
                    float currentY = pos.Y;

                    if (targetY < currentY - SnapEpsilon)
                    {
                        // On descend : limiter la descente par DescendSmoothSpeed
                        float maxDown = DescendSmoothSpeed * dt;
                        float newY = MathF.Max(targetY, currentY - maxDown);
                        Entity.Transform.Position = new Vector3(pos.X, newY, pos.Z);
                    }
                    else if (targetY > currentY + SnapEpsilon)
                    {
                        // On monte : limiter la montée par ClimbSmoothSpeed
                        float maxUp = ClimbSmoothSpeed * dt;
                        float newY = MathF.Min(targetY, currentY + maxUp);
                        Entity.Transform.Position = new Vector3(pos.X, newY, pos.Z);
                    }
                    else
                    {
                        // Proche de la cible : snap final
                        Entity.Transform.Position = new Vector3(pos.X, targetY, pos.Z);
                    }

                    // Annuler la vélocité verticale quand on est au sol
                    _velocity.Y = 0;
                }
                else
                {
                    // En l'air - appliquer la gravité normalement
                    IsGrounded = false;
                    float newY = currentPos.Y + _velocity.Y * dt;

                    // Vérifier si on va traverser le sol ce frame
                    float newBottom = newY - halfHeight;
                    if (newBottom <= groundY + 0.01f && _velocity.Y < 0)
                    {
                        // On atterrit ce frame - snap immédiat au sol
                        newY = targetY;
                        _velocity.Y = 0;
                        IsGrounded = true;
                    }

                    // IMPORTANT: Ne modifier QUE le Y, pas X et Z !
                    var pos = Entity.Transform.Position;
                    Entity.Transform.Position = new Vector3(pos.X, newY, pos.Z);
                }
            }
            else
            {
                // Pas de sol détecté - continuer de tomber
                IsGrounded = false;
                float newY = currentPos.Y + _velocity.Y * dt;
                
                // IMPORTANT: Ne modifier QUE le Y, pas X et Z !
                var pos = Entity.Transform.Position;
                Entity.Transform.Position = new Vector3(pos.X, newY, pos.Z);
            }

            // Final depenetration check ONLY when not grounded to prevent lateral clipping
            // When grounded, the ground snapping system handles vertical position
            if (!IsGrounded)
            {
                Vector3 depenetrationOffset = DepenetrateFromOverlaps(Entity.Transform.Position);
                if (depenetrationOffset.LengthSquared > 0.0001f)
                {
                    var pos = Entity.Transform.Position;
                    Entity.Transform.Position = pos + depenetrationOffset;
                }
            }

            Velocity = _velocity;

            if (_suppressSnapFrames > 0)
            {
                _suppressSnapFrames--;
            }
        }
        
        /// <summary>
        /// Vérifie le sol et met à jour IsGrounded (appelé chaque frame)
        /// </summary>
        private void CheckAndUpdateGroundState()
        {
            if (Entity?.Transform == null) return;
            
            var currentPos = Entity.Transform.Position;
            float halfHeight = Height * 0.5f;
            
            // Vérifier le sol depuis la position actuelle
            bool groundHit = CheckGround(currentPos, out float groundY);
            
            if (groundHit)
            {
                float characterBottom = currentPos.Y - halfHeight;
                float distanceToGround = characterBottom - groundY;
                
                // Tolérance pour être considéré "au sol"
                const float groundTolerance = 0.1f;
                
                if (distanceToGround <= groundTolerance && distanceToGround >= -0.05f)
                {
                    IsGrounded = true;

                    // Ne pas snapper immédiatement pour de petites différences : laisser Update(dt)
                    // gérer le lissage vertical afin d'éviter un effet "escalier" sur les pentes.
                    // Si on est clairement en train de pénétrer (sous une petite marge), forcer correction.
                    if (distanceToGround < -0.04f)
                    {
                        float targetY = groundY + halfHeight + GroundOffset;
                        Entity.Transform.Position = new Vector3(currentPos.X, targetY, currentPos.Z);
                    }
                }
                else if (distanceToGround > groundTolerance)
                {
                    IsGrounded = false;
                }
                else if (distanceToGround < -0.05f)
                {
                    // Pénètre dans le sol - forcer correction
                    IsGrounded = true;
                    float targetY = groundY + halfHeight + GroundOffset;
                    Entity.Transform.Position = new Vector3(currentPos.X, targetY, currentPos.Z);
                    _velocity.Y = 0;
                }
            }
            else
            {
                IsGrounded = false;
            }
        }

    private void ApplyHorizontalMovement(Vector3 horizontalMotion, float dt)
        {
            if (Entity?.Transform == null) return;

            var startPos = Entity.Transform.Position;
            var motionLength = horizontalMotion.Length;

            if (motionLength < 0.001f) return;

            // Sample ground normal at current position to project motion onto the surface tangent
            if (!SampleGroundAverage(startPos, out float groundY, out Vector3 groundNormal))
            {
                // fallback to standard movement if no ground sample
                var finalMotionFallback = ComputeSafeMovement(startPos, horizontalMotion, dt);
                var newPosFallback = startPos + new Vector3(finalMotionFallback.X, 0, finalMotionFallback.Z);
                Entity.Transform.Position = newPosFallback;
                return;
            }

            // Do NOT project the horizontal input for motion — preserve original horizontal direction
            // so the player moves straight in world-space (prevents curving on spheres).
            var finalMotion = ComputeSafeMovement(startPos, horizontalMotion, dt);
            
            // Apply the motion (only X and Z, Y will be adjusted to follow ground)
            var newPosXZ = startPos + new Vector3(finalMotion.X, 0, finalMotion.Z);
            var pos = Entity.Transform.Position;
            Entity.Transform.Position = new Vector3(newPosXZ.X, pos.Y, newPosXZ.Z);

            // After horizontal move, decide whether to adjust Y/rotation to follow surface.
            // If snapping is suppressed (recent jump) or we have a significant vertical velocity,
            // do not modify Y or rotation here so vertical movement from gravity/jump is preserved.
            if (_suppressSnapFrames > 0 || MathF.Abs(_velocity.Y) > 0.05f)
            {
                // Keep horizontal position applied; leave Y/rotation for vertical update step.
                return;
            }

            // After horizontal move, sample ground at new position and adjust Y to follow surface
            if (SampleGroundAverage(new Vector3(newPosXZ.X, pos.Y, newPosXZ.Z), out float newGroundY, out Vector3 newGroundNormal))
            {
                float halfHeight = Height * 0.5f;
                float targetY = newGroundY + halfHeight + GroundOffset;
                float currentY = Entity.Transform.Position.Y;

                // IMPORTANT: Only adjust Y if the ground is roughly at or below us
                // If the ground is significantly above us, we're likely at an edge - don't snap up
                float groundDelta = targetY - currentY;

                

                if (groundDelta > ClimbSmoothSpeed * dt * 2.0f)
                {
                    // Ground is too far above - likely detecting side of sphere/wall, not floor
                    // Don't adjust Y, let gravity handle it
                    return;
                }

                if (targetY < currentY - SnapEpsilon)
                {
                    float maxDown = DescendSmoothSpeed * dt;
                    float newY = MathF.Max(targetY, currentY - maxDown);
                    Entity.Transform.Position = new Vector3(newPosXZ.X, newY, newPosXZ.Z);
                }
                else if (targetY > currentY + SnapEpsilon)
                {
                    float maxUp = ClimbSmoothSpeed * dt;
                    float newY = MathF.Min(targetY, currentY + maxUp);
                    Entity.Transform.Position = new Vector3(newPosXZ.X, newY, newPosXZ.Z);
                }
                else
                {
                    Entity.Transform.Position = new Vector3(newPosXZ.X, targetY, newPosXZ.Z);
                }

                // Align entity up to ground normal while preserving yaw based on desired movement
                var rot = Entity.Transform.Rotation;
                // Prefer using the horizontal movement direction to compute yaw so the character
                // doesn't steer while climbing a curved surface (like a sphere).
                float yaw;
                var moveDirXZ = new Vector3(horizontalMotion.X, 0f, horizontalMotion.Z);
                if (moveDirXZ.LengthSquared > 1e-6f)
                {
                    moveDirXZ.Normalize();
                    yaw = MathF.Atan2(moveDirXZ.X, moveDirXZ.Z);
                }
                else
                {
                    var forward = Vector3.Transform(Vector3.UnitZ, rot);
                    yaw = MathF.Atan2(forward.X, forward.Z);
                }
                var yawQuat = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);

                float upDot = Vector3.Dot(Vector3.UnitY, newGroundNormal);
                upDot = MathF.Max(-1f, MathF.Min(1f, upDot));
                var axis = Vector3.Cross(Vector3.UnitY, newGroundNormal);
                float axisLen = axis.Length;
                Quaternion targetRot;
                if (axisLen < 1e-6f || upDot > 0.9999f)
                {
                    targetRot = yawQuat;
                }
                else
                {
                    var axisN = axis / axisLen;
                    float angle = MathF.Acos(upDot);
                    var tilt = Quaternion.FromAxisAngle(axisN, angle);
                    targetRot = Quaternion.Normalize(tilt * yawQuat);
                }

                // Optionally smooth rotation (simple lerp-like by slerp fraction approximated with nlerp)
                if (GroundAlignSpeed > 0f)
                {
                    float t = MathF.Min(1f, GroundAlignSpeed * dt);
                    // normalized linear interpolation (nlerp)
                    var lerp = Quaternion.Normalize(new Quaternion(
                        rot.X * (1 - t) + targetRot.X * t,
                        rot.Y * (1 - t) + targetRot.Y * t,
                        rot.Z * (1 - t) + targetRot.Z * t,
                        rot.W * (1 - t) + targetRot.W * t
                    ));
                    Entity.Transform.Rotation = lerp;
                }
                else
                {
                    Entity.Transform.Rotation = targetRot;
                }
            }
            else
            {
                // No ground sample after move; keep position and let gravity handle vertical
            }
        }

        private void ApplyVerticalMovement(float verticalDelta)
        {
            if (Entity?.Transform == null) return;

            var currentPos = Entity.Transform.Position;
            float halfHeight = Height * 0.5f;

            _wasGroundedLastFrame = IsGrounded;

            // NO CEILING COLLISION DETECTION

            // verticalDelta est en unités Y : négatif = descend, positif = monte
            var newY = currentPos.Y + verticalDelta;
            DebugLogger.Log($"[CC] ApplyVerticalMovement: currentY={currentPos.Y:F3}, delta={verticalDelta:F3}, newY={newY:F3}");

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
                    DebugLogger.Log($"[CC] Landed on ground at Y={newY:F3}");
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
            // Raycast depuis AU-DESSUS de la tête jusqu'en bas
            float rayLength = (halfHeight * 2) + GroundCheckDistance;

            // Raycast from above the head downward
            var rayOrigin = position + new Vector3(0, halfHeight + 0.2f, 0);
            var ray = new Ray { Origin = rayOrigin, Direction = new Vector3(0, -1, 0) };

            var hits = Physics.CollisionSystem.RaycastAll(ray, rayLength, ~0, QueryTriggerInteraction.Ignore);

            if (hits?.Count > 0)
            {
                RaycastHit? closestHit = null;
                float closestDistance = float.MaxValue;

                foreach (var hit in hits)
                {
                    if (hit.ColliderComponent?.Entity == Entity)
                    {
                        continue;
                    }

                    // Only consider walkable surfaces (normal pointing upward)
                    float upDot = Vector3.Dot(hit.Normal, Vector3.UnitY);
                    if (upDot < 0.1f) continue;

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

        // Sample ground at multiple nearby points and return averaged groundY and normal.
        private bool SampleGroundAverage(Vector3 position, out float avgGroundY, out Vector3 avgNormal)
        {
            avgGroundY = 0f;
            avgNormal = Vector3.UnitY;

            var hits = 0;
            var accumY = 0f;
            var accumNormal = Vector3.Zero;

            // PERFORMANCE: Reduced to 1 sample for meshes with many triangles (300k+)
            // Multi-point sampling causes severe FPS drops with complex MeshColliders
            // TODO: Re-enable multi-sampling once BVH/Octree is implemented for MeshCollider
            float sampleRadius = Radius * 0.9f;
            var offsets = new Vector3[] {
                new Vector3(0,0,0)  // Center only for now
            };

            foreach (var off in offsets)
            {
                if (SampleGround(position + off, out float gY, out Vector3 gNormal))
                {
                    accumY += gY;
                    accumNormal += gNormal;
                    hits++;
                }
            }

            if (hits == 0) return false;

            avgGroundY = accumY / hits;
            avgNormal = (accumNormal / hits).Normalized();
            return true;
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

                    // IMPORTANT: Only consider surfaces with normals pointing upward (walkable)
                    // Reject surfaces with downward-pointing normals (underside of spheres, overhangs, ceilings)
                    float upDot = Vector3.Dot(hit.Normal, Vector3.UnitY);
                    if (upDot < 0.1f) continue; // Normal must point at least somewhat upward

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
        /// Compute safe movement with collision detection and sliding.
        /// Uses capsule overlap tests to detect obstacles and slides along them.
        /// Handles penetration resolution robustly.
        /// </summary>
        private Vector3 ComputeSafeMovement(Vector3 startPos, Vector3 desiredMotion, float dt)
        {
            if (desiredMotion.LengthSquared < 0.0001f)
                return Vector3.Zero;

            float halfHeight = Height * 0.5f;
            Vector3 capsuleBottom = startPos + Vector3.UnitY * (-halfHeight + Radius);
            Vector3 capsuleTop = startPos + Vector3.UnitY * (halfHeight - Radius);

            Vector3 direction = desiredMotion.Normalized();
            float distance = desiredMotion.Length;

            // Depenetrate ONCE at the beginning
            Vector3 depenetrationOffset = DepenetrateFromOverlaps(startPos);
            if (depenetrationOffset.LengthSquared > 0.0001f)
            {
                startPos += depenetrationOffset;
                capsuleBottom = startPos + Vector3.UnitY * (-halfHeight + Radius);
                capsuleTop = startPos + Vector3.UnitY * (halfHeight - Radius);
            }

            const int maxIterations = 4;
            Vector3 totalMotion = Vector3.Zero;
            float remainingDistance = distance;
            Vector3 currentDirection = direction;

            for (int iteration = 0; iteration < maxIterations && remainingDistance > 0.001f; iteration++)
            {
                // Check for collisions along the movement path using capsule cast
                bool hitSomething = Physics.CollisionSystem.CapsuleCast(
                    capsuleBottom + totalMotion,
                    capsuleTop + totalMotion,
                    Radius,
                    currentDirection,
                    remainingDistance + SkinWidth,
                    out RaycastHit hit,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );

                if (hitSomething && hit.Entity != Entity)
                {
                    // Calculate how much of the hit normal is pointing upward
                    float upDot = Vector3.Dot(hit.Normal, Vector3.UnitY);

                    Console.WriteLine($"[CC] Iter {iteration} Hit {hit.ColliderComponent?.GetType().Name} Dist={hit.Distance:F4} Normal={hit.Normal:F3} UpDot={upDot:F2} RemDist={remainingDistance:F4}");

                    // STEP-UP: if the collider is close and the contact has an upward component,
                    // try to step up by `StepOffset` and continue movement if the path is clear.
                    if (hit.Distance <= StepOffset + SkinWidth && upDot > 0.3f && StepOffset > 0f)
                    {
                        // Test if there is clearance by moving the capsule up by StepOffset and
                        // re-checking the path forward. If no hit, apply the step and continue.
                        var cb = capsuleBottom + totalMotion + Vector3.UnitY * (StepOffset + 0.01f);
                        var ct = capsuleTop + totalMotion + Vector3.UnitY * (StepOffset + 0.01f);
                        if (!Physics.CollisionSystem.CapsuleCast(cb, ct, Radius, currentDirection, remainingDistance + SkinWidth, out var stepHit, ~0, QueryTriggerInteraction.Ignore))
                        {
                            // Apply step-up vertical offset and continue trying to move
                            totalMotion += Vector3.UnitY * StepOffset;
                            // recompute capsule positions and continue loop
                            capsuleBottom += Vector3.UnitY * StepOffset;
                            capsuleTop += Vector3.UnitY * StepOffset;
                            if (DebugPhysics) Console.WriteLine($"[CC][DBG] Stepped up {StepOffset:F3} to bypass small obstacle");
                            continue;
                        }
                    }

                    // Move as far as we can before hitting (leave skin width gap)
                    float safeDistance = MathF.Max(0, hit.Distance - SkinWidth);
                    Vector3 moveToContact = currentDirection * safeDistance;
                    totalMotion += moveToContact;
                    remainingDistance -= safeDistance;

                    if (remainingDistance <= 0.001f)
                        break;

                    // Calculate slide direction along the surface
                    Vector3 remainingMotion = currentDirection * remainingDistance;
                    Vector3 slideDirection = ProjectMotionOnPlane(remainingMotion, hit.Normal);

                    // Prevent upward climbing due to curved normals: clamp vertical component
                    // Allow small climbs per frame (MaxClimbPerFrame), but avoid large upward motion.
                    if (slideDirection.Y > MaxClimbPerFrame)
                    {
                        slideDirection.Y = MaxClimbPerFrame;
                    }
                    else if (slideDirection.Y < -MaxClimbPerFrame)
                    {
                        slideDirection.Y = -MaxClimbPerFrame;
                    }

                    // Continue sliding
                    float slideLength = slideDirection.Length;

                    // Special handling for curved surfaces (spheres/capsules)
                    bool isCurvedCollider = hit.ColliderComponent is SphereCollider || hit.ColliderComponent is CapsuleCollider;
                    bool isStuckInContact = hit.Distance <= 0.001f; // Essentially touching

                    Console.WriteLine($"[CC] SlideLen={slideLength:F4} IsCurved={isCurvedCollider} Stuck={isStuckInContact} HitDist={hit.Distance:F4}");

                    // For curved colliders where we're stuck in contact, use nudge instead of slide
                    if (isCurvedCollider && isStuckInContact)
                    {
                        // Use a small nudge in the desired direction to push past the curved surface
                        float nudgeDistance = MathF.Min(0.05f, remainingDistance);
                        totalMotion += currentDirection * nudgeDistance;
                        remainingDistance -= nudgeDistance;
                        Console.WriteLine($"[CC] >>> CURVED NUDGE: {nudgeDistance:F4}, remaining: {remainingDistance:F4}");

                        if (remainingDistance > 0.001f)
                            continue; // Try next iteration
                        break;
                    }

                    // If slide direction is too small, we're blocked by a flat surface
                    if (slideLength < 0.001f)
                    {
                        Console.WriteLine($"[CC] >>> BLOCKED: Slide vector too small ({slideLength:F6})");
                        break;
                    }

                    currentDirection = slideDirection.Normalized();
                    remainingDistance = slideLength;

                    if (DebugPhysics)
                    {
                        Console.WriteLine($"[CC] Iteration {iteration}: Hit {hit.ColliderComponent?.GetType().Name}, sliding along normal {hit.Normal:F2}, remaining: {remainingDistance:F3}");
                    }
                }
                else
                {
                    // Free movement - no obstacles
                    totalMotion += currentDirection * remainingDistance;
                    break;
                }
            }

            return totalMotion;
        }

        /// <summary>
        /// Depenetrate from any overlapping colliders.
        /// Returns the offset needed to push the character out of overlaps.
        /// </summary>
        private Vector3 DepenetrateFromOverlaps(Vector3 position)
        {
            float halfHeight = Height * 0.5f;
            Vector3 capsuleBottom = position + Vector3.UnitY * (-halfHeight + Radius);
            Vector3 capsuleTop = position + Vector3.UnitY * (halfHeight - Radius);
            
            // Check for overlaps using overlap capsule
            var overlaps = new List<Engine.Components.Collider>();
            if (Physics.CollisionSystem.OverlapCapsule(capsuleBottom, capsuleTop, Radius - 0.01f, out overlaps, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 totalOffset = Vector3.Zero;
                const int maxDepenetrations = 2;
                int depenetrationCount = 0;

                foreach (var overlap in overlaps)
                {
                    if (overlap.Entity == Entity) continue;
                    if (DebugPhysics && (overlap is SphereCollider || overlap is CapsuleCollider))
                    {
                        Console.WriteLine($"[CC][DBG] Overlap with {overlap.GetType().Name} at AABB {overlap.WorldAABB.Center} extents {overlap.WorldAABB.Extents}");
                    }
                    if (depenetrationCount >= maxDepenetrations) break;

                    bool hasContact = false;
                    Vector3 contactPoint = Vector3.Zero;
                    Vector3 normal = Vector3.UnitY;
                    float penetration = 0f;

                    // Use appropriate collision test based on collider type
                    if (overlap is SphereCollider sphereCol)
                    {
                        // Capsule vs Sphere
                        if (sphereCol.Entity != null)
                        {
                            sphereCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                            var sphereCenter = wpos + Vector3.Transform(sphereCol.Center * wscl, wrot);
                            float sphereRadius = sphereCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                            
                            hasContact = Physics.CollisionDetection.TestCapsuleSphere(
                                capsuleBottom + totalOffset,
                                capsuleTop + totalOffset,
                                Radius,
                                sphereCenter,
                                sphereRadius,
                                out contactPoint,
                                out normal,
                                out penetration
                            );
                        }
                    }
                    else if (overlap is CapsuleCollider capsuleCol)
                    {
                        // Capsule vs Capsule
                        if (capsuleCol.Entity != null)
                        {
                            capsuleCol.Entity.GetWorldTRS(out var wpos, out var wrot, out var wscl);
                            var center = wpos + Vector3.Transform(capsuleCol.Center * wscl, wrot);
                            float radius = capsuleCol.Radius * MathF.Max(MathF.Max(MathF.Abs(wscl.X), MathF.Abs(wscl.Y)), MathF.Abs(wscl.Z));
                            
                            float axisScale = capsuleCol.Direction switch { 0 => MathF.Abs(wscl.X), 1 => MathF.Abs(wscl.Y), 2 => MathF.Abs(wscl.Z), _ => MathF.Abs(wscl.Y) };
                            float height = MathF.Max(capsuleCol.Height * axisScale, 2f * radius);
                            float halfH = (height * 0.5f) - radius;
                            
                            Vector3 axis = capsuleCol.Direction switch
                            {
                                0 => Vector3.Transform(Vector3.UnitX, wrot),
                                1 => Vector3.Transform(Vector3.UnitY, wrot),
                                2 => Vector3.Transform(Vector3.UnitZ, wrot),
                                _ => Vector3.Transform(Vector3.UnitY, wrot)
                            };
                            
                            var p1 = center - axis * halfH;
                            var p2 = center + axis * halfH;
                            
                            hasContact = Physics.CollisionDetection.TestCapsuleCapsule(
                                capsuleBottom + totalOffset,
                                capsuleTop + totalOffset,
                                Radius,
                                p1, p2, radius,
                                out contactPoint,
                                out normal,
                                out penetration
                            );
                        }
                    }
                    else
                    {
                        // Fallback to AABB for other collider types (Box, Mesh, Heightfield, etc)
                        hasContact = Physics.CollisionDetection.TestCapsuleAABB(
                            capsuleBottom + totalOffset, 
                            capsuleTop + totalOffset, 
                            Radius, 
                            overlap.WorldAABB, 
                            out contactPoint, 
                            out normal, 
                            out penetration
                        );
                    }

                    if (hasContact && penetration > 0.001f)
                    {
                        if (DebugPhysics && (overlap is SphereCollider || overlap is CapsuleCollider))
                        {
                            Console.WriteLine($"[CC][DBG] Depenetrate Contact: {overlap.GetType().Name} pen={penetration:F4} normal={normal:F3} contactPt={contactPoint:F3}");
                        }

                        // Check if this is a ground-like contact (normal pointing mostly upward)
                        float normalUpDot = Vector3.Dot(normal, Vector3.UnitY);
                        bool isGroundLike = normalUpDot > 0.5f; // More than 60° from horizontal

                        Vector3 offset;

                        if (isGroundLike)
                        {
                            // For ground-like contacts (walking on top or near the side of a rounded surface),
                            // apply a small blended horizontal push away from the contact when the normal
                            // has a noticeable horizontal component. This helps the controller to "roll"
                            // off local bulges instead of getting stuck when only a vertical correction
                            // was applied.
                            float verticalPush = MathF.Min(penetration * normalUpDot + 0.005f, 0.03f);

                            var horiz = new Vector3(normal.X, 0f, normal.Z);
                            if (horiz.LengthSquared > 0.0001f && normalUpDot < 0.95f)
                            {
                                var hdir = horiz.Normalized();
                                // horizontal strength scales with how non-vertical the normal is
                                float horizStrength = MathF.Min(penetration * (1f - normalUpDot) * 0.6f, penetration * 0.5f);
                                offset = hdir * horizStrength + Vector3.UnitY * verticalPush;

                                if (DebugPhysics)
                                {
                                    Console.WriteLine($"[CC][DBG] Ground-like blended push (upDot={normalUpDot:F2}) horiz={horizStrength:F4} vert={verticalPush:F4}");
                                }
                            }
                            else
                            {
                                offset = Vector3.UnitY * verticalPush;
                                if (DebugPhysics)
                                {
                                    Console.WriteLine($"[CC][DBG] Ground-like contact (upDot={normalUpDot:F2}), vertical push only: {verticalPush:F4}");
                                }
                            }
                        }
                        else
                        {
                            // For wall-like contacts, prefer horizontal depenetration
                            var horiz = new Vector3(normal.X, 0f, normal.Z);

                            if (horiz.LengthSquared > 0.0001f)
                            {
                                // Use primarily horizontal push when possible
                                var dir = horiz.Normalized();
                                offset = dir * (penetration + 0.01f);
                            }
                            else
                            {
                                // Mostly vertical penetration - apply only a small vertical push
                                float maxVertical = 0.02f; // limit upward pop
                                float vertical = MathF.Min(penetration + 0.01f, maxVertical);
                                offset = new Vector3(0f, normal.Y > 0 ? vertical : -vertical, 0f);
                            }
                        }

                        // Clamp accumulated vertical offset so we don't launch the player skyward
                        var prospective = totalOffset + offset;
                        if (prospective.Y > 0.05f) offset.Y = MathF.Max(0f, 0.05f - totalOffset.Y);

                        totalOffset += offset;
                        depenetrationCount++;

                        if (DebugPhysics)
                        {
                            Console.WriteLine($"[CC][DBG] Depenetration offset applied: {offset:F3}");
                        }
                    }
                }

                return totalOffset;
            }

            return Vector3.Zero;
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