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
    /// - Manual movement via Move() method or SetDesiredVelocity() for smooth inertia
    /// - Capsule-based collision detection
    /// - Ground detection, slope handling, step-up
    /// - Gravity applied manually with customizable scale
    /// - Advanced movement feel: inertia, air control, coyote time, jump buffer
    /// - Perfect for player controllers
    ///
    /// PHYSICS MODE (Future - requires BulletSharp):
    /// - Full rigidbody simulation
    /// - Forces, impulses, mass, friction
    /// - Physics constraints
    /// - Perfect for NPCs and physics-driven characters
    ///
    /// USAGE (Legacy - Direct Movement):
    /// var cc = entity.AddComponent&lt;CharacterController&gt;();
    /// cc.Mode = CharacterControllerMode.Kinematic;
    /// cc.EnableMovementFeel = false; // Use legacy direct movement
    /// cc.Move(inputVector * speed * Time.DeltaTime);
    /// if (cc.IsGrounded && Input.Jump) cc.Jump(jumpForce);
    ///
    /// USAGE (Recommended - Inertia System):
    /// var cc = entity.AddComponent&lt;CharacterController&gt;();
    /// cc.Mode = CharacterControllerMode.Kinematic;
    /// cc.EnableMovementFeel = true; // Enable smooth acceleration/deceleration
    /// // In Update():
    /// cc.SetDesiredVelocity(inputVector * maxSpeed); // Set target velocity
    /// if (Input.JumpPressed) cc.RequestJump(jumpForce); // Supports buffering and coyote time
    ///
    /// MOVEMENT FEEL PARAMETERS:
    /// - GravityScale: 1.0 = normal, >1 = heavy, <1 = floaty
    /// - GroundAcceleration/Deceleration: Higher = snappier movement
    /// - AirControl: 0-1, how much control you have while airborne
    /// - CoyoteTime: Grace period after leaving ground where you can still jump
    /// - JumpBufferTime: Grace period before landing where jump input is remembered
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

        // ===== MOVEMENT FEEL SETTINGS =====

        /// <summary>Gravity multiplier - affects fall speed and feeling of weight (1.0 = normal, >1 = heavy, <1 = floaty)</summary>
        [Engine.Serialization.SerializableAttribute("gravityScale")]
        public float GravityScale { get; set; } = 1.0f;

        /// <summary>Maximum falling speed (prevents infinite acceleration)</summary>
        [Engine.Serialization.SerializableAttribute("terminalVelocity")]
        public float TerminalVelocity { get; set; } = 50f;

        /// <summary>Ground acceleration - how fast you reach max speed when grounded (higher = snappier)</summary>
        [Engine.Serialization.SerializableAttribute("groundAcceleration")]
        public float GroundAcceleration { get; set; } = 30f;

        /// <summary>Ground deceleration - how fast you stop when no input (higher = snappier stop)</summary>
        [Engine.Serialization.SerializableAttribute("groundDeceleration")]
        public float GroundDeceleration { get; set; } = 25f;

        /// <summary>Ground friction coefficient applied each frame (0-1, lower = more slippery)</summary>
        [Engine.Serialization.SerializableAttribute("groundFriction")]
        public float GroundFriction { get; set; } = 0.92f;

        /// <summary>Air control - how much you can influence movement while airborne (0-1, 1 = full control)</summary>
        [Engine.Serialization.SerializableAttribute("airControl")]
        public float AirControl { get; set; } = 0.8f;

        /// <summary>Air acceleration - how fast you can change direction in air</summary>
        [Engine.Serialization.SerializableAttribute("airAcceleration")]
        public float AirAcceleration { get; set; } = 15f;

        /// <summary>Air drag coefficient - resistance when moving through air (0-1)</summary>
        [Engine.Serialization.SerializableAttribute("airDrag")]
        public float AirDrag { get; set; } = 0.98f;

        /// <summary>Coyote time - grace period after leaving ground where you can still jump (seconds)</summary>
        [Engine.Serialization.SerializableAttribute("coyoteTime")]
        public float CoyoteTime { get; set; } = 0.15f;

        /// <summary>Jump buffer time - grace period before landing where jump input is remembered (seconds)</summary>
        [Engine.Serialization.SerializableAttribute("jumpBufferTime")]
        public float JumpBufferTime { get; set; } = 0.1f;

        /// <summary>Enable advanced movement feel (inertia, air control, etc.). If false, uses legacy direct movement.</summary>
        [Engine.Serialization.SerializableAttribute("enableMovementFeel")]
        public bool EnableMovementFeel { get; set; } = true;

        // ===== SLOPE SETTINGS =====
        // NO SLIDING - Slopes > SlopeLimit act like walls (can't climb)

        // ===== STATE (READ-ONLY) =====

        /// <summary>Is the controller currently grounded?</summary>
        public bool IsGrounded { get; private set; } = false;

        /// <summary>Is the controller currently sliding on a steep slope?</summary>
        public bool IsSliding { get; private set; } = false;

        /// <summary>Current slope angle in degrees (0 = flat, 90 = vertical)</summary>
        public float CurrentSlopeAngle { get; private set; } = 0f;

        /// <summary>Current velocity (affected by gravity, jumps, external forces)</summary>
        public Vector3 Velocity { get; private set; } = Vector3.Zero;

        /// <summary>Normal of the ground surface (if grounded)</summary>
        public Vector3 GroundNormal { get; private set; } = Vector3.UnitY;

        /// <summary>Distance to ground (0 if grounded)</summary>
        public float GroundDistance { get; private set; } = 0f;

        /// <summary>Is there a ceiling directly above the controller?</summary>
        public bool IsCeilingAbove { get; private set; } = false;

        /// <summary>Distance to ceiling (float.MaxValue if no ceiling)</summary>
        public float CeilingDistance { get; private set; } = float.MaxValue;

        // ===== INTERNAL CONSTANTS =====

        private const int MaxBounces = 4; // Max collision resolution iterations
        private const float MinMoveDistance = 0.001f; // Ignore tiny movements
        private const float GroundCheckDistance = 0.1f; // How far to check for ground

        // ===== TERRAIN COLLISION =====

        private Terrain? _cachedTerrain = null;
        private int _terrainCacheFrame = -1;
        private bool _isGroundedOnTerrain = false; // Track if grounded on terrain vs physics collider
        private Collider? _groundCollider = null; // Track the collider we're standing on

        // ===== MOVEMENT FEEL INTERNAL STATE =====

        private float _timeLeftGround = 0f; // Time since we left the ground (for coyote time)
        private float _jumpBufferCounter = 0f; // Countdown for jump buffer
        private Vector3 _desiredVelocity = Vector3.Zero; // Target velocity for smooth acceleration
        private bool _wasGroundedLastFrame = false; // Track grounding state changes
        private bool _wasSlidingLastFrame = false; // Track sliding state changes

        // ===== INTERPOLATION =====
        // Transform.Position = Physics position (ground truth, never modified by interpolation)
        // RenderPosition = Interpolated position for camera/rendering

        private Vector3 _previousPhysicsPosition = Vector3.Zero;  // Position at previous FixedUpdate
        private float _lastFixedUpdateTime = 0f;  // Absolute time of last FixedUpdate

        /// <summary>
        /// Position to use for rendering (camera should read this instead of Transform.Position).
        /// This is the interpolated position between physics frames.
        /// Uses absolute time to avoid stuttering from frame timing issues.
        /// </summary>
        public Vector3 RenderPosition
        {
            get
            {
                if (Entity == null || Interpolation == InterpolationMode.None)
                    return Entity?.Transform.Position ?? Vector3.Zero;

                if (Interpolation == InterpolationMode.Interpolate)
                {
                    // Calculate time since last FixedUpdate using absolute time
                    // This avoids stuttering issues from delta time accumulation
                    float timeSinceFixed = Engine.Core.Time.TimeValue - _lastFixedUpdateTime;
                    float alpha = timeSinceFixed / Engine.Core.Time.FixedDeltaTime;
                    alpha = MathF.Max(0f, MathF.Min(1f, alpha));

                    // CRITICAL FIX: When FixedUpdate just executed (alpha ~= 0), show current position
                    // Otherwise interpolation shows the OLD position causing stuttering!
                    if (alpha < 0.1f)
                    {
                        return Entity.Transform.Position;
                    }

                    // Interpolate between previous and current physics state
                    return Vector3.Lerp(_previousPhysicsPosition, Entity.Transform.Position, alpha);
                }
                else if (Interpolation == InterpolationMode.Extrapolate)
                {
                    // Extrapolate based on velocity
                    float timeSinceFixed = Engine.Core.Time.TimeValue - _lastFixedUpdateTime;
                    return Entity.Transform.Position + Velocity * timeSinceFixed;
                }

                return Entity.Transform.Position;
            }
        }

        // ===== COMPONENT LIFECYCLE =====

        public override void OnEnable()
        {
            base.OnEnable();

            // Initialize interpolation to avoid jump on first frame
            if (Entity != null)
            {
                _previousPhysicsPosition = Entity.Transform.Position;
                _lastFixedUpdateTime = Engine.Core.Time.TimeValue;
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (Entity == null || Mode != CharacterControllerMode.Kinematic)
                return;

            // KINEMATIC MODE: Movement in Update (synchronized with camera LateUpdate)

            // Update coyote time and jump buffer counters
            if (!IsGrounded)
                _timeLeftGround += deltaTime;
            else
                _timeLeftGround = 0f;

            if (_jumpBufferCounter > 0f)
                _jumpBufferCounter -= deltaTime;

            // Track grounding state changes
            if (IsGrounded && !_wasGroundedLastFrame)
            {
                // Just landed - check if there's a buffered jump
                if (_jumpBufferCounter > 0f)
                {
                    // Execute buffered jump
                    Jump(10f); // Default jump force - you can expose this later
                    _jumpBufferCounter = 0f;
                }
            }

            // Track state changes
            _wasGroundedLastFrame = IsGrounded;
            _wasSlidingLastFrame = IsSliding;

            // Apply gravity with scale and terminal velocity
            if (EnableGravity && !IsGrounded)
            {
                Velocity += Gravity * GravityScale * deltaTime;

                // Clamp to terminal velocity
                if (Velocity.Y < -TerminalVelocity)
                    Velocity = new Vector3(Velocity.X, -TerminalVelocity, Velocity.Z);
            }

            // Zero out downward velocity when grounded to prevent sinking
            if (IsGrounded && Velocity.Y < 0)
            {
                Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
            }

            // ADVANCED MOVEMENT FEEL: Apply acceleration/deceleration with inertia
            if (EnableMovementFeel)
            {
                ApplyMovementFeel(deltaTime);
            }
            else
            {
                // LEGACY MODE: Direct velocity application (old behavior)
                // Decay velocity when grounded (friction)
                if (IsGrounded)
                {
                    Velocity = new Vector3(Velocity.X * 0.9f, 0f, Velocity.Z * 0.9f);
                }
            }

            // MOVEMENT: Use surface-following movement when grounded, standard Move() when in air
            if (Velocity.LengthSquared > MinMoveDistance * MinMoveDistance)
            {
                if (IsGrounded && _isGroundedOnTerrain && FindTerrain() != null)
                {
                    // On terrain - use terrain-specific movement for smooth slope following
                    MoveOnTerrain(Velocity * deltaTime);
                }
                else if (IsGrounded && !_isGroundedOnTerrain)
                {
                    // On physics collider - use collider surface following for smooth slope climbing
                    MoveOnCollider(Velocity * deltaTime);
                }
                else
                {
                    // In air - use standard movement
                    Move(Velocity * deltaTime);
                }
            }

            // Check ground state AFTER applying movement (critical for accurate grounding)
            CheckGround();

            // Check ceiling state
            CheckCeiling();
        }

        public override void FixedUpdate(float deltaTime)
        {
            base.FixedUpdate(deltaTime);

            // FixedUpdate is for Physics mode (future BulletSharp integration)
            // Kinematic mode uses Update() to stay synchronized with camera
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
        /// Enhanced with contact classification for better wall/ceiling/ground handling.
        /// </summary>
        /// <param name="motion">Desired movement in world space</param>
        public void Move(Vector3 motion)
        {
            if (Entity == null) return;
            if (Mode != CharacterControllerMode.Kinematic) return;
            if (motion.LengthSquared < MinMoveDistance * MinMoveDistance) return;

            // Standard 3D movement with collision resolution
            // Used when: in air or jumping
            // NOT used when grounded (uses MoveOnTerrain or MoveOnCollider instead)

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

                // Collision occurred - classify contact type
                ContactType contactType = ClassifyContact(hitNormal);
                finalPosition = newPosition;

                // Calculate remaining motion based on contact type
                float remainingDistance = remainingMotion.Length * (1.0f - travelDistance);

                if (contactType == ContactType.Ceiling)
                {
                    // Hit ceiling - slide along it but don't try to move up
                    Vector3 slideDirection = ProjectVectorOntoPlane(remainingMotion, hitNormal);

                    // Remove any upward component
                    if (slideDirection.Y > 0)
                        slideDirection.Y = 0;

                    float slideLength = slideDirection.Length;
                    if (slideLength > 0.001f)
                        remainingMotion = Vector3.Normalize(slideDirection) * remainingDistance;
                    else
                        break; // Can't slide, stop here
                }
                else if (contactType == ContactType.Wall)
                {
                    // Hit wall - slide along it
                    Vector3 slideDirection = ProjectVectorOntoPlane(remainingMotion, hitNormal);
                    float slideLength = slideDirection.Length;

                    if (slideLength > 0.001f)
                    {
                        // Slide along wall
                        remainingMotion = Vector3.Normalize(slideDirection) * remainingDistance;
                    }
                    else
                    {
                        // Moving perpendicular to wall - stop
                        break;
                    }
                }
                else // ContactType.Ground
                {
                    // Hit ground - check slope
                    float slopeAngle = Vector3.CalculateAngle(Vector3.UnitY, hitNormal) * (180f / MathF.PI);

                    if (slopeAngle > SlopeLimit)
                    {
                        // Slope too steep - treat as wall
                        Vector3 slideDirection = ProjectVectorOntoPlane(remainingMotion, hitNormal);
                        float slideLength = slideDirection.Length;

                        if (slideLength > 0.001f)
                            remainingMotion = Vector3.Normalize(slideDirection) * remainingDistance;
                        else
                            break;
                    }
                    else
                    {
                        // Walkable slope - slide along it
                        Vector3 slideDirection = ProjectVectorOntoPlane(remainingMotion, hitNormal);
                        float slideLength = slideDirection.Length;

                        if (slideLength > 0.001f)
                            remainingMotion = Vector3.Normalize(slideDirection) * remainingDistance;
                        else
                            break;
                    }
                }
            }

            // Apply final position
            Entity.Transform.Position = finalPosition;

            // CRITICAL: Depenetrate after movement to prevent getting stuck in walls
            Depenetrate();
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
        /// Set desired movement velocity (for smooth acceleration with inertia).
        /// Use this instead of directly setting Velocity when EnableMovementFeel is true.
        /// </summary>
        public void SetDesiredVelocity(Vector3 desiredVelocity)
        {
            _desiredVelocity = desiredVelocity;
        }

        /// <summary>
        /// Request a jump (supports jump buffering).
        /// Call this when the player presses jump - it will be buffered if in air.
        /// </summary>
        public void RequestJump(float jumpForce)
        {
            if (Mode != CharacterControllerMode.Kinematic)
                return;

            // If grounded or within coyote time, jump immediately
            if (IsGrounded || _timeLeftGround <= CoyoteTime)
            {
                Jump(jumpForce);
            }
            else
            {
                // Buffer the jump for when we land
                _jumpBufferCounter = JumpBufferTime;
            }
        }

        /// <summary>
        /// Perform a simple jump with the given force.
        /// For player input, use RequestJump() instead (supports buffering).
        /// </summary>
        public void Jump(float jumpForce)
        {
            if (Mode != CharacterControllerMode.Kinematic)
                return;

            // Allow jump if grounded OR within coyote time
            if (!IsGrounded && _timeLeftGround > CoyoteTime)
                return;

            Velocity = new Vector3(Velocity.X, jumpForce, Velocity.Z);
            IsGrounded = false;
            _timeLeftGround = CoyoteTime + 0.01f; // Prevent double-jump via coyote time
        }

        // ===== INTERNAL METHODS - KINEMATIC MODE =====

        /// <summary>
        /// Apply movement feel with inertia, acceleration, and drag.
        /// This creates smooth, responsive character movement with weight.
        /// </summary>
        private void ApplyMovementFeel(float deltaTime)
        {
            // Extract horizontal velocity (we don't apply friction to vertical velocity)
            Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);
            Vector3 horizontalDesired = new Vector3(_desiredVelocity.X, 0, _desiredVelocity.Z);

            if (IsSliding)
            {
                // === SLIDING ON STEEP SLOPE ===
                ApplySlidePhysics(deltaTime);
            }
            else if (IsGrounded)
            {
                // === GROUNDED MOVEMENT ===

                // Normal flat ground movement (slopes are now handled purely by slide mechanics)
                ApplyFlatGroundMovement(deltaTime);
            }
            else
            {
                // === AIR MOVEMENT ===

                // Apply air control - player has limited influence over movement in air
                if (horizontalDesired.LengthSquared > 0.01f)
                {
                    // Accelerate in air (slower than on ground)
                    float airAccel = AirAcceleration * AirControl * deltaTime;
                    Vector3 targetVelocity = Vector3.Lerp(horizontalVelocity, horizontalDesired, airAccel);

                    // Update horizontal velocity
                    horizontalVelocity = targetVelocity;
                }

                // Apply air drag (slight deceleration when moving through air)
                horizontalVelocity *= AirDrag;

                // Update velocity (preserve vertical component)
                Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Z);
            }

            // Reset desired velocity after applying (scripts must set it every frame)
            _desiredVelocity = Vector3.Zero;
        }

        /// <summary>
        /// Apply normal flat ground movement with acceleration and friction.
        /// </summary>
        private void ApplyFlatGroundMovement(float deltaTime)
        {
            Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);
            Vector3 horizontalDesired = new Vector3(_desiredVelocity.X, 0, _desiredVelocity.Z);

            Vector3 targetVelocity;
            if (horizontalDesired.LengthSquared > 0.01f)
            {
                // Player is inputting movement - accelerate towards desired velocity
                float acceleration = GroundAcceleration * deltaTime;
                targetVelocity = Vector3.Lerp(horizontalVelocity, horizontalDesired, acceleration);
            }
            else
            {
                // No input - decelerate to stop
                float deceleration = GroundDeceleration * deltaTime;
                targetVelocity = Vector3.Lerp(horizontalVelocity, Vector3.Zero, deceleration);

                // Stop completely if very slow
                if (targetVelocity.LengthSquared < 0.01f)
                    targetVelocity = Vector3.Zero;
            }

            // Apply ground friction
            targetVelocity *= GroundFriction;

            // Update velocity (preserve vertical component)
            Velocity = new Vector3(targetVelocity.X, Velocity.Y, targetVelocity.Z);
        }

        /// <summary>
        /// Apply slope sliding - simple gravity-based acceleration down the slope.
        /// </summary>
        private void ApplySlidePhysics(float deltaTime)
        {
            // Calculate slope direction (downward along the slope)
            Vector3 slopeDirection = CalculateSlopeDirection(GroundNormal);

            // Apply gravity along the slope
            float gravityMagnitude = Gravity.Length * GravityScale;
            Vector3 slopeGravity = slopeDirection * gravityMagnitude * deltaTime;
            Velocity += slopeGravity;

            // Apply basic friction to prevent infinite acceleration
            Velocity *= GroundFriction;
        }

        /// <summary>
        /// Calculate the direction of steepest descent on a slope.
        /// </summary>
        private Vector3 CalculateSlopeDirection(Vector3 normal)
        {
            // The slope direction is perpendicular to both the normal and the horizontal plane
            // This gives us the direction of steepest descent
            Vector3 right = Vector3.Cross(normal, Vector3.UnitY);

            // Handle case where normal is vertical (avoid zero vector)
            if (right.LengthSquared < 0.0001f)
            {
                right = Vector3.UnitX;
            }
            else
            {
                right = Vector3.Normalize(right);
            }

            // Cross product in reverse order to get downward direction
            Vector3 slopeDown = Vector3.Cross(normal, right);
            slopeDown = Vector3.Normalize(slopeDown);

            return slopeDown;
        }

        /// <summary>
        /// Project a vector onto a plane defined by its normal.
        /// </summary>
        private Vector3 ProjectVectorOntoPlane(Vector3 vector, Vector3 planeNormal)
        {
            // Remove the component of vector that's perpendicular to the plane
            float distance = Vector3.Dot(vector, planeNormal);
            return vector - planeNormal * distance;
        }

        /// <summary>
        /// Contact type classification based on surface normal
        /// </summary>
        private enum ContactType
        {
            Ground,   // Walkable surface (normal pointing mostly up)
            Wall,     // Vertical or steep surface (normal pointing sideways)
            Ceiling   // Overhead surface (normal pointing mostly down)
        }

        /// <summary>
        /// Classify a contact based on its surface normal.
        /// This helps differentiate between ground, walls, and ceilings for better collision response.
        /// </summary>
        private ContactType ClassifyContact(Vector3 normal)
        {
            // Use dot product with up vector to classify
            float upDot = Vector3.Dot(normal, Vector3.UnitY);

            if (upDot > 0.7f)  // cos(45°) ≈ 0.7
            {
                return ContactType.Ground;
            }
            else if (upDot < -0.7f)
            {
                return ContactType.Ceiling;
            }
            else
            {
                return ContactType.Wall;
            }
        }

        /// <summary>
        /// Try to move from current position by motion vector.
        /// Returns true if collision occurred.
        /// Enhanced with better surface normal handling for smooth sliding on rounded surfaces.
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
                bool isMovingUpward = motion.Y > 0.05f; // Moving upward (jumping) - require a bit more upward motion to qualify

                // Only ignore if:
                // 1. It's a ground-like surface (normal pointing up)
                // 2. We're jumping upward
                // 3. The hit is very close (we're standing on it)
                // 4. The hit is BELOW us (not in front)
                if (isGroundHit && isMovingUpward && hit.Distance < SkinWidth * 2f)
                {
                    // Check if hit is below us (standing on it) vs in front (hitting a slope)
                    Vector3 bottomPos = currentPos + Center - Vector3.UnitY * ((Height - 2f * Radius) * 0.5f);
                    const float groundIgnoreEpsilon = 0.02f;

                    // Check both vertical AND horizontal position
                    bool isBelow = hit.Point.Y <= bottomPos.Y + groundIgnoreEpsilon;

                    // Check if hit is in front of us horizontally
                    Vector3 toHit = new Vector3(hit.Point.X - bottomPos.X, 0, hit.Point.Z - bottomPos.Z);
                    Vector3 horizontalMotion = new Vector3(motion.X, 0, motion.Z);
                    bool isInFront = false;

                    if (toHit.LengthSquared > 0.0001f && horizontalMotion.LengthSquared > 0.0001f)
                    {
                        float dotProduct = Vector3.Dot(Vector3.Normalize(toHit), Vector3.Normalize(horizontalMotion));
                        isInFront = dotProduct > 0.5f; // Hit is in front if > 60 degrees alignment
                    }

                    // Only ignore when the contact point is at or below us AND not in front
                    if (isBelow && !isInFront)
                    {
                        // Ignore this ground collision - we're jumping away from the ground under us
                        return false; // No collision - proceed with full movement
                    }
                }

                // Hit something - move to hit point with depenetration bias
                // Add extra margin to prevent getting stuck against walls
                const float DepenetrationBias = 0.02f; // Increased significantly to prevent sticking
                float safeDistance = MathF.Max(0, hit.Distance - SkinWidth - DepenetrationBias);
                newPosition = currentPos + direction * safeDistance;

                // IMPROVED: Better normal handling for curved surfaces
                // For spheres and rounded surfaces, use the actual contact normal for smoother sliding
                if (hit.Collider is SphereCollider)
                {
                    // For spheres, the normal at the contact point is perfect for sliding
                    hitNormal = hit.Normal;
                }
                else if (hit.Collider is CapsuleCollider)
                {
                    // For capsules, the normal is also accurate for smooth sliding
                    hitNormal = hit.Normal;
                }
                else
                {
                    // For boxes and other primitives, use the hit normal
                    hitNormal = hit.Normal;
                }

                travelDistance = safeDistance / distance;
                return true;
            }

            // No hit - safe to move
            return false;
        }

        /// <summary>
        /// Cast the character's capsule shape along a direction.
        /// Uses swept sphere casts at 3 points (bottom, middle, top) to approximate capsule.
        /// Now uses FULL radius for accurate collision detection.
        /// </summary>
        private bool CapsuleCast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            // Capsule is approximated by 3 sphere sweeps at different heights
            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;

            // Sample points along the capsule
            Vector3 capsuleCenter = origin + Center;
            Vector3 bottom = capsuleCenter - Vector3.UnitY * halfCylinderHeight;
            Vector3 middle = capsuleCenter;
            Vector3 top = capsuleCenter + Vector3.UnitY * halfCylinderHeight;

            // Use FULL radius for accurate detection
            float sweepRadius = Radius;

            RaycastHit? closestHit = null;
            float closestDistance = float.MaxValue;

            // BOTTOM sphere
            if (PhysicsManager.Instance.SphereCast(bottom, sweepRadius, direction, out RaycastHit bottomHit, distance, CollisionMask))
            {
                // Filter out ground under feet when moving horizontally (to allow jumping)
                bool shouldIgnore = false;
                bool isHorizontalMove = MathF.Abs(direction.Y) < 0.3f;
                bool isGroundHit = bottomHit.Normal.Y > 0.7f;

                if (isHorizontalMove && isGroundHit && IsGrounded && bottomHit.Distance < SkinWidth * 2f)
                {
                    // Only ignore if hit is UNDER us, not IN FRONT
                    Vector3 toHit = bottomHit.Point - bottom;
                    Vector3 horizontalToHit = new Vector3(toHit.X, 0, toHit.Z);
                    Vector3 horizontalDirection = new Vector3(direction.X, 0, direction.Z);

                    if (horizontalToHit.LengthSquared > 0.0001f && horizontalDirection.LengthSquared > 0.0001f)
                    {
                        float dotProduct = Vector3.Dot(Vector3.Normalize(horizontalToHit), Vector3.Normalize(horizontalDirection));
                        shouldIgnore = dotProduct < 0.5f; // Ignore if not in front (< 60 degrees)
                    }
                    else
                    {
                        shouldIgnore = true; // Hit is directly below
                    }
                }

                if (!shouldIgnore && bottomHit.Distance < closestDistance)
                {
                    closestDistance = bottomHit.Distance;
                    closestHit = bottomHit;
                }
            }

            // MIDDLE sphere
            if (PhysicsManager.Instance.SphereCast(middle, sweepRadius, direction, out RaycastHit middleHit, distance, CollisionMask))
            {
                if (middleHit.Distance < closestDistance)
                {
                    closestDistance = middleHit.Distance;
                    closestHit = middleHit;
                }
            }

            // TOP sphere
            if (PhysicsManager.Instance.SphereCast(top, sweepRadius, direction, out RaycastHit topHit, distance, CollisionMask))
            {
                if (topHit.Distance < closestDistance)
                {
                    closestDistance = topHit.Distance;
                    closestHit = topHit;
                }
            }

            // Check terrain collision ONLY at bottom (ground level)
            var terrain = FindTerrain();
            if (terrain != null)
            {
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
                IsSliding = false;
                CurrentSlopeAngle = 0f;
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
            float physicsSlopeAngle = 0f;

            // Check physics colliders - use FULL radius for accurate detection
            if (PhysicsManager.Instance.SphereCast(origin, Radius, -Vector3.UnitY, out RaycastHit hit, checkDistance, CollisionMask))
            {
                physicsSlopeAngle = Vector3.CalculateAngle(Vector3.UnitY, hit.Normal) * (180f / MathF.PI);

                // Accept ground even if slope is too steep (we'll slide on it instead of falling through)
                hasPhysicsGround = true;
                // The hit.Distance is from origin to contact point
                // Since origin is the bottom sphere center (already at Radius above ground when grounded),
                // the actual distance from the bottom of the capsule is hit.Distance
                physicsGroundDistance = hit.Distance;
                physicsGroundNormal = hit.Normal;
            }

            // Check terrain collision
            bool hasTerrainGround = CheckTerrainCollision(origin, out float terrainHeight, out Vector3 terrainNormal, out float terrainSlopeAngle);
            float terrainGroundDistance = hasTerrainGround ? MathF.Max(0, origin.Y - terrainHeight) : float.MaxValue;

            // Validate terrain distance (but NOT slope - we want to detect steep slopes for sliding)
            if (hasTerrainGround)
            {
                if (terrainGroundDistance > checkDistance)
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
                    CurrentSlopeAngle = terrainSlopeAngle;
                    _isGroundedOnTerrain = true;
                    _groundCollider = null; // Not on a physics collider

                    // Determine if we're sliding on this slope
                    DetermineSlideState(terrainSlopeAngle);
                }
                else if (hasPhysicsGround)
                {
                    // Physics collider is closer
                    IsGrounded = true;
                    GroundNormal = physicsGroundNormal;
                    GroundDistance = physicsGroundDistance;
                    CurrentSlopeAngle = physicsSlopeAngle;
                    _isGroundedOnTerrain = false;
                    _groundCollider = hit.Collider; // Store the collider we're standing on

                    // Determine if we're sliding on this slope
                    DetermineSlideState(physicsSlopeAngle);
                }
                else
                {
                    IsGrounded = false;
                    IsSliding = false;
                    GroundNormal = Vector3.UnitY;
                    GroundDistance = float.MaxValue;
                    CurrentSlopeAngle = 0f;
                    _isGroundedOnTerrain = false;
                    _groundCollider = null;
                }
            }
            else
            {
                IsGrounded = false;
                IsSliding = false;
                GroundNormal = Vector3.UnitY;
                GroundDistance = float.MaxValue;
                CurrentSlopeAngle = 0f;
                _isGroundedOnTerrain = false;
                _groundCollider = null;
            }
        }

        /// <summary>
        /// Resolve any overlaps with colliders by pushing the character out.
        /// This prevents the character from getting stuck inside walls or other colliders.
        /// Uses multiple sphere checks along the capsule height to detect and resolve overlaps.
        /// </summary>
        private void ResolveOverlaps()
        {
            if (Entity == null) return;

            const int MaxIterations = 8; // Max attempts to push out
            const float PushDistance = 0.03f; // Distance to push per iteration

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                bool hadOverlap = false;
                Vector3 totalPushVector = Vector3.Zero;

                // Check overlaps at multiple points along the capsule
                float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
                Vector3 capsuleCenter = Entity.Transform.Position + Center;
                Vector3 bottom = capsuleCenter - Vector3.UnitY * halfCylinderHeight;
                Vector3 middle = capsuleCenter;
                Vector3 top = capsuleCenter + Vector3.UnitY * halfCylinderHeight;

                // Check bottom sphere (smaller radius to avoid ground)
                var bottomOverlaps = PhysicsManager.Instance.OverlapSphere(bottom, Radius * 0.8f, CollisionMask);
                foreach (var collider in bottomOverlaps)
                {
                    // Skip the ground we're standing on
                    if (collider == _groundCollider && IsGrounded) continue;

                    // Calculate push direction (away from collider center)
                    Vector3 colliderCenter = collider.WorldCenter;
                    Vector3 directionAway = bottom - colliderCenter;
                    
                    if (directionAway.LengthSquared > 0.0001f)
                    {
                        directionAway = Vector3.Normalize(directionAway);
                        totalPushVector += directionAway;
                        hadOverlap = true;
                    }
                }

                // Check middle sphere
                var middleOverlaps = PhysicsManager.Instance.OverlapSphere(middle, Radius * 0.95f, CollisionMask);
                foreach (var collider in middleOverlaps)
                {
                    if (collider == _groundCollider && IsGrounded) continue;

                    Vector3 colliderCenter = collider.WorldCenter;
                    Vector3 directionAway = middle - colliderCenter;
                    
                    if (directionAway.LengthSquared > 0.0001f)
                    {
                        directionAway = Vector3.Normalize(directionAway);
                        totalPushVector += directionAway;
                        hadOverlap = true;
                    }
                }

                // Check top sphere
                var topOverlaps = PhysicsManager.Instance.OverlapSphere(top, Radius * 0.95f, CollisionMask);
                foreach (var collider in topOverlaps)
                {
                    if (collider == _groundCollider && IsGrounded) continue;

                    Vector3 colliderCenter = collider.WorldCenter;
                    Vector3 directionAway = top - colliderCenter;
                    
                    if (directionAway.LengthSquared > 0.0001f)
                    {
                        directionAway = Vector3.Normalize(directionAway);
                        totalPushVector += directionAway;
                        hadOverlap = true;
                    }
                }

                // If no overlaps, we're done
                if (!hadOverlap) break;

                // Push character out
                if (totalPushVector.LengthSquared > 0.0001f)
                {
                    Vector3 pushDirection = Vector3.Normalize(totalPushVector);
                    Vector3 pushAmount = pushDirection * PushDistance;

                    // Don't push down through ground
                    if (IsGrounded && pushAmount.Y < 0)
                    {
                        pushAmount.Y = 0;
                    }

                    Entity.Transform.Position += pushAmount;
                }
            }
        }

        /// <summary>
        /// Determine if the character should be sliding based on slope angle.
        /// Simple: Slope > SlopeLimit = slide down
        /// </summary>
        private void DetermineSlideState(float slopeAngle)
        {
            // Hysteresis to prevent jittering
            const float HYSTERESIS = 2f;

            if (slopeAngle > SlopeLimit + HYSTERESIS)
            {
                IsSliding = true;
            }
            else if (slopeAngle < SlopeLimit - HYSTERESIS)
            {
                IsSliding = false;
            }
        }

        /// <summary>
        /// Check for ceiling collisions above the character.
        /// This prevents the character from clipping through ceilings and stops upward velocity.
        /// </summary>
        private void CheckCeiling()
        {
            if (Entity == null)
            {
                IsCeilingAbove = false;
                CeilingDistance = float.MaxValue;
                return;
            }

            // Start from the top sphere center of the capsule
            Vector3 origin = GetTopPosition();
            float checkDistance = Radius + SkinWidth * 2f;

            // Check for colliders above using sphere cast with FULL radius for accurate detection
            if (PhysicsManager.Instance.SphereCast(origin, Radius, Vector3.UnitY, out RaycastHit hit, checkDistance, CollisionMask))
            {
                // Check if the hit normal is pointing downward (ceiling surface)
                if (hit.Normal.Y < -0.1f)
                {
                    IsCeilingAbove = true;
                    CeilingDistance = hit.Distance;

                    // Stop upward velocity if hitting ceiling
                    if (Velocity.Y > 0f)
                    {
                        Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
                    }
                    return;
                }
            }

            IsCeilingAbove = false;
            CeilingDistance = float.MaxValue;
        }

        /// <summary>
        /// Depenetrate from overlapping colliders.
        /// This prevents the character from getting stuck inside colliders.
        /// Uses 3 sample points (bottom, middle, top) for efficiency.
        /// Improved: Prevents pushing downward when grounded.
        /// </summary>
        private void Depenetrate()
        {
            if (Entity == null) return;

            const int maxIterations = 3;
            const float maxDepenetrationPerFrame = 0.3f;

            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                // Recalculate positions each iteration
                Vector3 capsuleCenter = Entity.Transform.Position + Center;
                Vector3 bottom = capsuleCenter - Vector3.UnitY * halfCylinderHeight;
                Vector3 middle = capsuleCenter;
                Vector3 top = capsuleCenter + Vector3.UnitY * halfCylinderHeight;

                Vector3[] samplePoints = { bottom, middle, top };
                Vector3 totalDepenetration = Vector3.Zero;
                int overlapCount = 0;

                foreach (var samplePoint in samplePoints)
                {
                    // Check for overlapping colliders
                    var overlapping = PhysicsManager.Instance.OverlapSphere(samplePoint, Radius, CollisionMask);

                    foreach (var collider in overlapping)
                    {
                        // CRITICAL: Skip the ground collider we're standing on
                        if (IsGrounded && collider == _groundCollider)
                            continue;

                        // Get closest point on collider
                        Vector3 closestPoint = collider.ClosestPoint(samplePoint);
                        Vector3 penetrationVector = samplePoint - closestPoint;
                        float penetrationDepth = penetrationVector.Length;

                        // Check if we're penetrating (inside collider)
                        if (penetrationDepth < Radius && penetrationDepth > 0.001f)
                        {
                            // Calculate push-out direction
                            Vector3 pushDirection = Vector3.Normalize(penetrationVector);
                            float pushAmount = Radius - penetrationDepth + SkinWidth;

                            totalDepenetration += pushDirection * pushAmount;
                            overlapCount++;
                        }
                    }
                }

                // Apply depenetration if overlaps found
                if (overlapCount > 0)
                {
                    Vector3 avgDepenetration = totalDepenetration / overlapCount;

                    // IMPROVED: When grounded, prevent pushing downward (keep CC on ground)
                    if (IsGrounded && avgDepenetration.Y < 0)
                    {
                        avgDepenetration.Y = 0;
                    }

                    // Clamp to max per frame to prevent teleporting
                    if (avgDepenetration.Length > maxDepenetrationPerFrame)
                    {
                        avgDepenetration = Vector3.Normalize(avgDepenetration) * maxDepenetrationPerFrame;
                    }

                    // Only apply if significant
                    if (avgDepenetration.LengthSquared > 0.00001f)
                    {
                        Entity.Transform.Position += avgDepenetration;
                    }
                }
                else
                {
                    // No overlaps - we're done
                    break;
                }
            }
        }

        /// <summary>
        /// Move on physics collider with surface following (like MoveOnTerrain but for colliders).
        /// Follows the surface of box/sphere/capsule colliders smoothly.
        /// SIMPLIFIED: Uses SphereCast for consistent detection and minimal smoothing.
        /// </summary>
        private void MoveOnCollider(Vector3 motion)
        {
            if (Entity == null) return;

            Vector3 currentPos = Entity.Transform.Position;

            // SAFETY: Prevent micro-movements
            const float minSignificantMotion = 0.001f;
            if (motion.LengthSquared < minSignificantMotion * minSignificantMotion)
                return;

            // STEP 1: Calculate target XZ position (horizontal movement with collisions)
            Vector3 horizontalMotion = new Vector3(motion.X, 0f, motion.Z);
            Vector3 targetXZ = currentPos;

            if (horizontalMotion.LengthSquared >= MinMoveDistance * MinMoveDistance)
            {
                // Check horizontal collisions
                Vector3 origin = currentPos + Center;
                Vector3 direction = Vector3.Normalize(horizontalMotion);
                float distance = horizontalMotion.Length;

                if (PhysicsManager.Instance.SphereCast(origin, Radius, direction, out RaycastHit hit, distance, CollisionMask))
                {
                    // Hit something - move safely and slide
                    float safeDistance = MathF.Max(0, hit.Distance - SkinWidth);
                    Vector3 slideMotion = ProjectVectorOntoPlane(horizontalMotion, hit.Normal);
                    targetXZ = currentPos + Vector3.Normalize(horizontalMotion) * safeDistance + slideMotion * 0.5f;
                }
                else
                {
                    // No collision - move full distance
                    targetXZ = currentPos + horizontalMotion;
                }
            }

            // STEP 2: Find ground height at new XZ position
            // CRITICAL: Cast from FRONT EDGE of bottom hemisphere to detect ground naturally at edges
            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
            Vector3 bottomCenter = new Vector3(targetXZ.X, currentPos.Y, targetXZ.Z) + Center - Vector3.UnitY * halfCylinderHeight;

            // Determine cast origin based on movement direction
            Vector3 castOrigin = bottomCenter;
            if (horizontalMotion.LengthSquared > 0.0001f)
            {
                // Moving - cast from front edge of hemisphere
                Vector3 moveDir = Vector3.Normalize(horizontalMotion);
                castOrigin = bottomCenter + new Vector3(moveDir.X * Radius * 0.7f, 0, moveDir.Z * Radius * 0.7f);
            }

            // Cast downward from above to detect ground
            Vector3 rayStart = castOrigin + Vector3.UnitY * (Height + 1f);
            float rayDistance = Height + GroundCheckDistance + 10f;
            bool foundGround = false;
            float targetY = currentPos.Y;

            if (PhysicsManager.Instance.Raycast(rayStart, -Vector3.UnitY, out RaycastHit groundHit, rayDistance, CollisionMask))
            {
                // Validate upward-facing normal (ground, not wall)
                if (groundHit.Normal.Y > 0.7f)
                {
                    float groundY = groundHit.Point.Y;
                    
                    // Validate reasonable distance to prevent detecting adjacent colliders
                    float verticalDistance = MathF.Abs(groundY - (bottomCenter.Y - Radius));
                    if (verticalDistance < 1.5f) // Only accept nearby ground
                    {
                        // Calculate target entity Y position
                        targetY = groundY + Radius - Center.Y + halfCylinderHeight;
                        foundGround = true;
                    }
                }
            }

            // STEP 3: Calculate vertical movement with adaptive smoothing
            float currentY = currentPos.Y;
            float heightDiff = targetY - currentY;
            float verticalMotion = 0f;

            if (foundGround)
            {
                bool isGoingDown = heightDiff < 0f;
                bool isMovingFast = horizontalMotion.Length > 0.1f;
                
                if (isGoingDown)
                {
                    // DESCENDING: Aggressive smoothing to stick to ground
                    if (isMovingFast && MathF.Abs(heightDiff) > 0.01f)
                    {
                        // Fast descent - very responsive
                        const float descentSmoothSpeed = 100f;
                        float smoothFactor = 1f - MathF.Exp(-descentSmoothSpeed * Engine.Core.Time.DeltaTime);
                        verticalMotion = heightDiff * smoothFactor;
                    }
                    else
                    {
                        // Slow descent - moderate smoothing
                        const float descentSmoothSpeed = 50f;
                        float smoothFactor = 1f - MathF.Exp(-descentSmoothSpeed * Engine.Core.Time.DeltaTime);
                        verticalMotion = heightDiff * smoothFactor;
                    }
                }
                else
                {
                    // ASCENDING: Gentler smoothing for smooth uphill
                    const float ascentSmoothSpeed = 30f;
                    float smoothFactor = 1f - MathF.Exp(-ascentSmoothSpeed * Engine.Core.Time.DeltaTime);
                    verticalMotion = heightDiff * smoothFactor;
                }
                
                // SNAP when very close to eliminate micro-oscillations
                if (MathF.Abs(heightDiff) < 0.002f)
                {
                    verticalMotion = heightDiff;
                }
            }
            else
            {
                // No ground - don't move vertically (gravity handles falling)
                verticalMotion = 0f;
            }

            // STEP 4: Apply motion
            Vector3 fullMotion = new Vector3(
                targetXZ.X - currentPos.X,
                verticalMotion,
                targetXZ.Z - currentPos.Z
            );

            if (fullMotion.LengthSquared > 0.00001f)
            {
                Entity.Transform.Position = currentPos + fullMotion;

                // Depenetrate to prevent getting stuck
                Depenetrate();
            }
        }

        /// <summary>
        /// Move on terrain with horizontal collision detection and vertical terrain following.
        /// Calculates vertical velocity to smoothly follow terrain height.
        /// </summary>
        private void MoveOnTerrain(Vector3 motion)
        {
            if (Entity == null) return;

            var terrain = FindTerrain();
            if (terrain == null)
            {
                // NO TERRAIN FOUND - This should never happen if _isGroundedOnTerrain is true!
                // Safety fallback: use standard movement instead
                _isGroundedOnTerrain = false;
                Move(motion);
                return;
            }

            Vector3 currentPos = Entity.Transform.Position;

            // STEP 1: Calculate target XZ position (horizontal movement with collisions)
            Vector3 horizontalMotion = new Vector3(motion.X, 0f, motion.Z);
            Vector3 targetXZ = currentPos;

            if (horizontalMotion.LengthSquared >= MinMoveDistance * MinMoveDistance)
            {
                // Check horizontal collisions
                Vector3 origin = currentPos + Center;
                Vector3 direction = Vector3.Normalize(horizontalMotion);
                float distance = horizontalMotion.Length;

                if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, direction, out RaycastHit hit, distance, CollisionMask))
                {
                    // Hit something - slide along surface
                    float safeDistance = MathF.Max(0, hit.Distance - SkinWidth);
                    Vector3 slideMotion = horizontalMotion - Vector3.Dot(horizontalMotion, hit.Normal) * hit.Normal;
                    targetXZ = currentPos + Vector3.Normalize(horizontalMotion) * safeDistance + slideMotion * 0.5f;
                }
                else
                {
                    // No collision - move full distance
                    targetXZ = currentPos + horizontalMotion;
                }
            }

            // STEP 2: Calculate target Y from terrain height at new XZ
            float targetY = CalculateTerrainTargetY(targetXZ.X, targetXZ.Z);

            // STEP 3: Calculate vertical movement to reach target height smoothly
            // This creates smooth interpolation instead of direct snapping
            float currentY = currentPos.Y;
            float heightDiff = targetY - currentY;

            // Use exponential smoothing for vertical movement (prevents overshoot)
            // This gives smooth, damped movement that converges to target without oscillation
            const float verticalSmoothSpeed = 20f; // Higher = more responsive
            float smoothFactor = 1f - MathF.Exp(-verticalSmoothSpeed * Engine.Core.Time.DeltaTime);
            float verticalMotion = heightDiff * smoothFactor;

            // Update velocity for interpolation (but don't accumulate, use calculated motion)
            Velocity = new Vector3(
                Velocity.X,
                verticalMotion / Engine.Core.Time.DeltaTime,
                Velocity.Z
            );

            // STEP 4: Apply full motion (horizontal + smooth vertical)
            Vector3 fullMotion = new Vector3(
                targetXZ.X - currentPos.X,
                verticalMotion,
                targetXZ.Z - currentPos.Z
            );

            Entity.Transform.Position = currentPos + fullMotion;

            // Depenetrate after movement to prevent getting stuck
            Depenetrate();
        }

        /// <summary>
        /// Calculate the target Y position for perfect capsule alignment on terrain
        /// </summary>
        private float CalculateTerrainTargetY(float worldX, float worldZ)
        {
            var terrain = FindTerrain();
            if (terrain == null || !terrain.IsPositionOnTerrain(worldX, worldZ))
                return Entity?.Transform.Position.Y ?? 0f;

            // Query terrain height
            float terrainHeight = terrain.GetHeightAtPosition(worldX, worldZ);

            // Calculate correct entity Y for capsule alignment
            float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;
            return terrainHeight - Center.Y + halfCylinderHeight + Radius;
        }

        /// <summary>
        /// Snap character to ground when on slopes (prevents bouncing on both terrain and physics colliders)
        /// </summary>
        private void SnapToGround()
        {
            if (Entity == null || !IsGrounded)
                return;

            // Don't snap when grounded on terrain (MoveOnTerrain handles it)
            if (_isGroundedOnTerrain)
                return;

            // PHYSICS COLLIDER SNAPPING ONLY
            // Don't snap - let gravity handle it naturally
            // Snapping was causing stuttering and stuck issues
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
