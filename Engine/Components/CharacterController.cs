using System;
using System.Linq;
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

        /// <summary>Interpolation mode for smooth rendering</summary>
        [Engine.Serialization.SerializableAttribute("interpolation")]
        public InterpolationMode Interpolation { get; set; } = InterpolationMode.Interpolate;

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

        // ===== TERRAIN COLLISION =====

        private Terrain? _cachedTerrain = null;
        private int _terrainCacheFrame = -1;
        private bool _isGroundedOnTerrain = false; // Track if grounded on terrain vs physics collider

        // ===== INTERPOLATION =====
        // Transform.Position = Physics position (ground truth, never modified by interpolation)
        // RenderPosition = Interpolated position for camera/rendering

        private Vector3 _previousPhysicsPosition = Vector3.Zero;  // Position at previous FixedUpdate
        private Vector3 _currentPhysicsPosition = Vector3.Zero;   // Position at current FixedUpdate
        private float _timeSinceLastFixedUpdate = 0f;

        /// <summary>
        /// Position to use for rendering (camera should read this instead of Transform.Position).
        /// This is the interpolated position between physics frames.
        /// </summary>
        public Vector3 RenderPosition
        {
            get
            {
                if (Entity == null || Interpolation == InterpolationMode.None)
                    return Entity?.Transform.Position ?? Vector3.Zero;

                if (Interpolation == InterpolationMode.Interpolate)
                {
                    // Interpolate between previous and current physics positions
                    float alpha = _timeSinceLastFixedUpdate / Engine.Core.Time.FixedDeltaTime;
                    alpha = MathF.Max(0f, MathF.Min(1f, alpha));
                    return Vector3.Lerp(_previousPhysicsPosition, _currentPhysicsPosition, alpha);
                }
                else if (Interpolation == InterpolationMode.Extrapolate)
                {
                    // Extrapolate based on velocity
                    return _currentPhysicsPosition + Velocity * _timeSinceLastFixedUpdate;
                }

                return _currentPhysicsPosition;
            }
        }

        // ===== COMPONENT LIFECYCLE =====

        public override void OnEnable()
        {
            base.OnEnable();

            // Initialize interpolation positions to avoid jump on first frame
            if (Entity != null)
            {
                _previousPhysicsPosition = Entity.Transform.Position;
                _currentPhysicsPosition = Entity.Transform.Position;
            }
        }

        public override void FixedUpdate(float deltaTime)
        {
            base.FixedUpdate(deltaTime);

            if (Entity == null || Mode != CharacterControllerMode.Kinematic)
                return;

            // Store previous physics position for interpolation
            _previousPhysicsPosition = _currentPhysicsPosition;

            // Apply gravity only when NOT grounded
            if (EnableGravity && !IsGrounded)
            {
                Velocity += Gravity * deltaTime;
            }

            // TERRAIN-SPECIFIC MOVEMENT: Separate horizontal and vertical
            if (IsGrounded && _isGroundedOnTerrain)
            {
                // Grounded on terrain - move horizontally, follow terrain height vertically
                MoveOnTerrain(Velocity * deltaTime);
            }
            else
            {
                // In air or on physics collider - normal 3D movement
                if (Velocity.LengthSquared > MinMoveDistance * MinMoveDistance)
                {
                    Move(Velocity * deltaTime);
                }
            }

            // Check ground state AFTER applying movement (critical for terrain following)
            CheckGround();

            // Decay velocity when grounded (friction)
            if (IsGrounded)
            {
                Velocity = new Vector3(Velocity.X * 0.9f, 0f, Velocity.Z * 0.9f);
            }

            // Store current physics position (Transform.Position is the ground truth)
            _currentPhysicsPosition = Entity.Transform.Position;

            // Reset interpolation timer
            _timeSinceLastFixedUpdate = 0f;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (Entity == null || Mode != CharacterControllerMode.Kinematic)
                return;

            // Accumulate time since last FixedUpdate
            _timeSinceLastFixedUpdate += deltaTime;
        }

        public override void LateUpdate(float deltaTime)
        {
            base.LateUpdate(deltaTime);

            // LateUpdate does nothing now - interpolation happens in RenderPosition property
            // Camera should read RenderPosition instead of Transform.Position
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

            // Standard 3D movement with collision resolution
            // Used when: in air, on physics colliders, or jumping
            // NOT used when grounded on terrain (uses MoveOnTerrain instead)

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

            // Apply final position
            Entity.Transform.Position = finalPosition;
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
                // IMPORTANT: Ignore ONLY the ground we're standing on when jumping
                // But still collide with terrain in front of us (like slopes)
                bool isGroundHit = hit.Normal.Y > 0.7f; // Hit normal is mostly upward (ground)
                bool isMovingUpward = motion.Y > 0.01f; // Moving upward (jumping)

                // Only ignore if:
                // 1. It's a ground-like surface (normal pointing up)
                // 2. We're jumping upward
                // 3. The hit is very close (we're standing on it)
                // 4. The hit is BELOW us (not in front)
                if (isGroundHit && isMovingUpward && hit.Distance < SkinWidth * 2f)
                {
                    // Check if hit is below us (standing on it) vs in front (hitting a slope)
                    Vector3 bottomPos = currentPos + Center - Vector3.UnitY * ((Height - 2f * Radius) * 0.5f);
                    if (hit.Point.Y < bottomPos.Y + Radius * 0.5f) // Hit is below our feet
                    {
                        // Ignore this ground collision - we're jumping away from the ground under us
                        return false; // No collision - proceed with full movement
                    }
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
        /// Checks both physics colliders AND terrain heightmap.
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

            // Check bottom sphere against physics colliders
            if (PhysicsManager.Instance.SphereCast(bottom, Radius, direction, out RaycastHit bottomHit, distance, CollisionMask))
            {
                if (bottomHit.Distance < closestDistance)
                {
                    closestDistance = bottomHit.Distance;
                    closestHit = bottomHit;
                }
            }

            // Check middle sphere against physics colliders
            if (PhysicsManager.Instance.SphereCast(middle, Radius, direction, out RaycastHit middleHit, distance, CollisionMask))
            {
                if (middleHit.Distance < closestDistance)
                {
                    closestDistance = middleHit.Distance;
                    closestHit = middleHit;
                }
            }

            // Check top sphere against physics colliders
            if (PhysicsManager.Instance.SphereCast(top, Radius, direction, out RaycastHit topHit, distance, CollisionMask))
            {
                if (topHit.Distance < closestDistance)
                {
                    closestDistance = topHit.Distance;
                    closestHit = topHit;
                }
            }

            // Check terrain collision ONLY at bottom (ground level)
            // Don't check middle/top as it prevents climbing slopes
            var terrain = FindTerrain();
            if (terrain != null)
            {
                // Check bottom sphere only for terrain collision
                if (terrain.RaycastTerrain(bottom, direction, distance, out Engine.Physics.RaycastHit terrainHit))
                {
                    if (terrainHit.Distance < closestDistance)
                    {
                        closestDistance = terrainHit.Distance;
                        closestHit = terrainHit;
                    }
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
        /// Check if character is grounded (checks both physics colliders and terrain)
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
            // CRITICAL: checkDistance must be >= Radius to detect terrain when perfectly aligned
            // Because origin (bottom sphere center) is Radius above the terrain when grounded
            float checkDistance = Radius + GroundCheckDistance + SkinWidth;

            bool hasPhysicsGround = false;
            float physicsGroundDistance = float.MaxValue;
            Vector3 physicsGroundNormal = Vector3.UnitY;

            // Check physics colliders
            if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, -Vector3.UnitY, out RaycastHit hit, checkDistance, CollisionMask))
            {
                float slopeAngle = Vector3.CalculateAngle(Vector3.UnitY, hit.Normal) * (180f / MathF.PI);

                if (slopeAngle <= SlopeLimit)
                {
                    hasPhysicsGround = true;
                    physicsGroundDistance = hit.Distance;
                    physicsGroundNormal = hit.Normal;
                }
            }

            // Check terrain collision
            bool hasTerrainGround = CheckTerrainCollision(origin, out float terrainHeight, out Vector3 terrainNormal, out float terrainSlopeAngle);
            float terrainGroundDistance = hasTerrainGround ? MathF.Max(0, origin.Y - terrainHeight) : float.MaxValue;

            // Validate terrain slope AND distance
            if (hasTerrainGround)
            {
                if (terrainSlopeAngle > SlopeLimit || terrainGroundDistance > checkDistance)
                {
                    hasTerrainGround = false;
                    terrainGroundDistance = float.MaxValue;
                }
            }

            // Choose closest ground (physics or terrain)
            if (hasPhysicsGround || hasTerrainGround)
            {
                if (terrainGroundDistance < physicsGroundDistance && hasTerrainGround)
                {
                    // Terrain is closer
                    IsGrounded = true;
                    GroundNormal = terrainNormal;
                    GroundDistance = terrainGroundDistance;
                    _isGroundedOnTerrain = true; // On terrain
                }
                else if (hasPhysicsGround)
                {
                    // Physics collider is closer
                    IsGrounded = true;
                    GroundNormal = physicsGroundNormal;
                    GroundDistance = physicsGroundDistance;
                    _isGroundedOnTerrain = false; // On physics object (platform, etc.)
                }
                else
                {
                    IsGrounded = false;
                    GroundNormal = Vector3.UnitY;
                    GroundDistance = float.MaxValue;
                    _isGroundedOnTerrain = false;
                }
            }
            else
            {
                IsGrounded = false;
                GroundNormal = Vector3.UnitY;
                GroundDistance = float.MaxValue;
                _isGroundedOnTerrain = false;
            }
        }

        /// <summary>
        /// Move on terrain with horizontal collision detection and vertical terrain following.
        /// This is called when grounded on terrain - completely bypasses vertical physics.
        /// </summary>
        private void MoveOnTerrain(Vector3 motion)
        {
            if (Entity == null) return;

            var terrain = FindTerrain();
            if (terrain == null) return;

            Vector3 currentPos = Entity.Transform.Position;

            // STEP 1: Move horizontally only (ignore motion.Y, we'll calculate Y from terrain)
            Vector3 horizontalMotion = new Vector3(motion.X, 0f, motion.Z);

            if (horizontalMotion.LengthSquared < MinMoveDistance * MinMoveDistance)
            {
                // No horizontal movement - just update Y to terrain height
                UpdateHeightToTerrain(currentPos.X, currentPos.Z);
                return;
            }

            // STEP 2: Check horizontal collisions (walls, obstacles)
            Vector3 desiredPos = currentPos + horizontalMotion;
            Vector3 finalPos = currentPos;

            // Use capsule cast for horizontal collision (only check middle sphere to allow climbing)
            Vector3 origin = currentPos + Center;
            Vector3 direction = Vector3.Normalize(horizontalMotion);
            float distance = horizontalMotion.Length;

            if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, direction, out RaycastHit hit, distance, CollisionMask))
            {
                // Hit something - slide along surface
                float safeDistance = MathF.Max(0, hit.Distance - SkinWidth);
                Vector3 slideMotion = horizontalMotion - Vector3.Dot(horizontalMotion, hit.Normal) * hit.Normal;
                finalPos = currentPos + Vector3.Normalize(horizontalMotion) * safeDistance + slideMotion * 0.5f;
            }
            else
            {
                // No collision - move full distance
                finalPos = desiredPos;
            }

            // STEP 3: Update Y position to match terrain height at new XZ position
            UpdateHeightToTerrain(finalPos.X, finalPos.Z);
        }

        /// <summary>
        /// Set entity Y position to match terrain height (perfect capsule alignment)
        /// </summary>
        private void UpdateHeightToTerrain(float worldX, float worldZ)
        {
            if (Entity == null) return;

            var terrain = FindTerrain();
            if (terrain == null || !terrain.IsPositionOnTerrain(worldX, worldZ))
                return;

            // Query terrain height
            float terrainHeight = terrain.GetHeightAtPosition(worldX, worldZ);

            // Calculate correct entity Y for capsule alignment
            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
            float targetY = terrainHeight - Center.Y + halfCylinderHeight + Radius;

            // Set position directly (no snapping, no corrections)
            Entity.Transform.Position = new Vector3(worldX, targetY, worldZ);
        }

        /// <summary>
        /// Snap character to ground when on slopes (prevents bouncing on both terrain and physics colliders)
        /// </summary>
        private void SnapToGround()
        {
            if (Entity == null || !IsGrounded)
                return;

            // Start from the bottom sphere center of the capsule
            Vector3 origin = GetBottomPosition();
            float snapDistance = StepHeight;

            float closestSnapDistance = float.MaxValue;
            bool foundPhysicsGround = false;

            // Check physics colliders
            if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, -Vector3.UnitY, out RaycastHit hit, snapDistance, CollisionMask))
            {
                if (hit.Distance > SkinWidth)
                {
                    closestSnapDistance = hit.Distance;
                    foundPhysicsGround = true;
                }
            }

            // Check terrain - for terrain, we want to place the absolute bottom of the capsule at terrain height
            Vector3 currentPos = Entity.Transform.Position;
            if (CheckTerrainCollision(new Vector3(currentPos.X, currentPos.Y, currentPos.Z), out float terrainHeight, out _, out _))
            {
                // CRITICAL FIX: Calculate correct Entity.Y for absolute bottom to touch terrain
                // Absolute bottom = Entity.Y + Center.Y - halfCylinderHeight - Radius
                // If we want: terrainHeight = Entity.Y + Center.Y - halfCylinderHeight - Radius
                // Then: Entity.Y = terrainHeight - Center.Y + halfCylinderHeight + Radius

                float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
                float desiredEntityY = terrainHeight - Center.Y + halfCylinderHeight + Radius;

                // Snap both UP (if sunk into terrain) and DOWN (if floating above)
                float heightDifference = currentPos.Y - desiredEntityY;
                float absHeightDiff = MathF.Abs(heightDifference);

                // ALWAYS snap when grounded on terrain (remove distance check)
                // This ensures perfect alignment even with interpolation
                if (absHeightDiff > 0.001f)
                {
                    if (!foundPhysicsGround || absHeightDiff < closestSnapDistance)
                    {
                        closestSnapDistance = absHeightDiff;

                        // SMOOTH snap instead of instant teleport to avoid stutter
                        // Use time-based lerp for frame-rate independent smoothing
                        float snapSpeed = 20.0f; // Units per second
                        float maxDelta = snapSpeed * Engine.Core.Time.FixedDeltaTime;
                        float delta = desiredEntityY - currentPos.Y;
                        delta = MathF.Max(-maxDelta, MathF.Min(maxDelta, delta)); // Clamp
                        float newY = currentPos.Y + delta;

                        Entity.Transform.Position = new Vector3(
                            currentPos.X,
                            newY,
                            currentPos.Z
                        );
                        return; // Early exit, already snapped
                    }
                }
            }

            // Apply snap for physics ground only if we didn't snap to terrain
            if (closestSnapDistance < float.MaxValue && closestSnapDistance > 0.001f && foundPhysicsGround)
            {
                float snapAmount = closestSnapDistance - SkinWidth;
                if (snapAmount > 0)
                {
                    Entity.Transform.Position -= Vector3.UnitY * snapAmount;
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

        // ===== TERRAIN COLLISION INTEGRATION =====

        /// <summary>
        /// Adjust horizontal motion to follow terrain slopes (both uphill and downhill).
        /// Samples terrain height ahead and adjusts Y component of motion.
        /// </summary>
        private Vector3 AdjustMotionForTerrain(Vector3 currentPosition, Vector3 motion)
        {
            var terrain = FindTerrain();
            if (terrain == null)
                return motion; // No terrain, keep original motion

            // Only adjust horizontal motion (ignore if jumping/falling)
            if (MathF.Abs(motion.Y) > 0.01f)
                return motion;

            // Get horizontal movement direction and distance
            Vector3 horizontalMotion = new Vector3(motion.X, 0, motion.Z);
            float horizontalDistance = horizontalMotion.Length;

            if (horizontalDistance < MinMoveDistance)
                return motion; // Too small to matter

            // IMPROVED: Use actual entity position, not bottom position
            // This gives us the correct height query for the terrain
            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;

            // Get current terrain height at current XZ position
            if (!CheckTerrainCollision(new Vector3(currentPosition.X, currentPosition.Y, currentPosition.Z),
                out float currentTerrainHeight, out _, out _))
                return motion; // Not on terrain

            // Calculate where we'll be after horizontal movement
            Vector3 targetPosition = currentPosition + horizontalMotion;

            // Get terrain height at target XZ position
            if (!CheckTerrainCollision(new Vector3(targetPosition.X, targetPosition.Y, targetPosition.Z),
                out float targetTerrainHeight, out _, out float targetSlope))
                return motion; // Target position not on terrain

            // If slope is too steep, don't adjust (let collision system handle it)
            if (targetSlope > SlopeLimit)
                return motion;

            // Calculate the correct entity Y positions for both current and target
            float currentDesiredY = currentTerrainHeight - Center.Y + halfCylinderHeight + Radius;
            float targetDesiredY = targetTerrainHeight - Center.Y + halfCylinderHeight + Radius;

            // The vertical motion should be the difference in desired Y positions
            float verticalMotion = targetDesiredY - currentDesiredY;

            // Return motion with corrected Y component to follow terrain exactly
            return new Vector3(
                motion.X,
                verticalMotion,
                motion.Z
            );
        }

        /// <summary>
        /// Find the terrain in the scene (cached per frame for performance).
        /// Returns null if no terrain is found.
        /// </summary>
        private Terrain? FindTerrain()
        {
            if (Entity?.Scene == null)
                return null;

            // Use frame-based caching to avoid repeated scene searches
            int currentFrame = Engine.Core.Time.FrameCount;
            if (_terrainCacheFrame == currentFrame && _cachedTerrain != null)
                return _cachedTerrain;

            // Search for terrain in the scene
            foreach (var entity in Entity.Scene.Entities)
            {
                var terrain = entity.GetComponent<Terrain>();
                if (terrain != null)
                {
                    _cachedTerrain = terrain;
                    _terrainCacheFrame = currentFrame;
                    return terrain;
                }
            }

            _terrainCacheFrame = currentFrame;
            return null;
        }

        /// <summary>
        /// Check terrain collision at the character's position.
        /// Returns true if standing on terrain, with height and normal data.
        /// </summary>
        private bool CheckTerrainCollision(Vector3 position, out float terrainHeight, out Vector3 terrainNormal, out float slopeAngle)
        {
            terrainHeight = 0f;
            terrainNormal = Vector3.UnitY;
            slopeAngle = 0f;

            var terrain = FindTerrain();
            if (terrain == null)
                return false;

            // Check if position is on terrain bounds
            if (!terrain.IsPositionOnTerrain(position.X, position.Z))
                return false;

            // Get terrain data at position
            terrainHeight = terrain.GetHeightAtPosition(position.X, position.Z);
            terrainNormal = terrain.GetNormalAtPosition(position.X, position.Z);
            slopeAngle = terrain.GetSlopeAngleAtPosition(position.X, position.Z);

            return true;
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

    /// <summary>
    /// Interpolation mode for smooth rendering between FixedUpdate calls
    /// </summary>
    public enum InterpolationMode
    {
        /// <summary>No interpolation - position updated only in FixedUpdate (can appear stuttery)</summary>
        None = 0,

        /// <summary>Interpolate between previous and current physics positions (smooth but slight delay)</summary>
        Interpolate = 1,

        /// <summary>Extrapolate future position based on velocity (responsive but can overshoot)</summary>
        Extrapolate = 2
    }
}
