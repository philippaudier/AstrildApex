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
        public float AirControl { get; set; } = 0.3f;

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

        // ===== SLOPE SLIDING SETTINGS =====

        /// <summary>Enable realistic sliding on steep slopes (slopes steeper than SlopeLimit)</summary>
        [Engine.Serialization.SerializableAttribute("enableSliding")]
        public bool EnableSliding { get; set; } = true;

        /// <summary>Sliding friction coefficient - how much the slope resists sliding (0-1, lower = more slippery)</summary>
        [Engine.Serialization.SerializableAttribute("slideFriction")]
        public float SlideFriction { get; set; } = 0.3f;

        /// <summary>Gravity multiplier when sliding (higher = slides faster down slopes)</summary>
        [Engine.Serialization.SerializableAttribute("slideGravityMultiplier")]
        public float SlideGravityMultiplier { get; set; } = 1.5f;

        /// <summary>How much player input can affect slide direction (0-1, 0 = no control, 1 = full control)</summary>
        [Engine.Serialization.SerializableAttribute("slideControl")]
        public float SlideControl { get; set; } = 0.4f;

        /// <summary>Maximum slide speed (prevents infinite acceleration)</summary>
        [Engine.Serialization.SerializableAttribute("maxSlideSpeed")]
        public float MaxSlideSpeed { get; set; } = 20f;

        /// <summary>Minimum slope angle (degrees) to start sliding. Slopes between SlopeLimit and this are walkable but slow.</summary>
        [Engine.Serialization.SerializableAttribute("minSlideAngle")]
        public float MinSlideAngle { get; set; } = 50f;

        // ===== SLOPE MOMENTUM SETTINGS =====

        /// <summary>Enable momentum-based slope climbing (like Mario 64)</summary>
        [Engine.Serialization.SerializableAttribute("enableSlopeMomentum")]
        public bool EnableSlopeMomentum { get; set; } = true;

        /// <summary>How much momentum is retained when going uphill (0-1, 1 = full momentum retained)</summary>
        [Engine.Serialization.SerializableAttribute("slopeMomentumRetention")]
        public float SlopeMomentumRetention { get; set; } = 0.7f;

        /// <summary>Deceleration rate when climbing slopes (higher = slower climb)</summary>
        [Engine.Serialization.SerializableAttribute("slopeClimbDeceleration")]
        public float SlopeClimbDeceleration { get; set; } = 8f;

        /// <summary>Minimum speed to maintain before starting to slide backwards on steep slopes</summary>
        [Engine.Serialization.SerializableAttribute("minSpeedBeforeBackslide")]
        public float MinSpeedBeforeBackslide { get; set; } = 1f;

        /// <summary>Start sliding backwards if stopped on slope steeper than this angle</summary>
        [Engine.Serialization.SerializableAttribute("backslideAngle")]
        public float BackslideAngle { get; set; } = 35f;

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

        // ===== INTERNAL CONSTANTS =====

        private const int MaxBounces = 4; // Max collision resolution iterations
        private const float MinMoveDistance = 0.001f; // Ignore tiny movements
        private const float GroundCheckDistance = 0.1f; // How far to check for ground

        // ===== TERRAIN COLLISION =====

        private Terrain? _cachedTerrain = null;
        private int _terrainCacheFrame = -1;
        private bool _isGroundedOnTerrain = false; // Track if grounded on terrain vs physics collider

        // ===== MOVEMENT FEEL INTERNAL STATE =====

        private float _timeLeftGround = 0f; // Time since we left the ground (for coyote time)
        private float _jumpBufferCounter = 0f; // Countdown for jump buffer
        private Vector3 _desiredVelocity = Vector3.Zero; // Target velocity for smooth acceleration
        private bool _wasGroundedLastFrame = false; // Track grounding state changes
        private Vector3 _slideVelocity = Vector3.Zero; // Accumulated slide velocity (separate from normal velocity)
        private float _postSlideInertiaTimer = 0f; // Timer to reduce deceleration after exiting slide

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

            // Reset slide velocity when we stop sliding or leave ground
            if (!IsSliding && _slideVelocity.LengthSquared > 0.01f)
            {
                // Transfer slide momentum to regular velocity when exiting slide
                if (IsGrounded)
                {
                    Velocity = new Vector3(_slideVelocity.X, Velocity.Y, _slideVelocity.Z);
                    _postSlideInertiaTimer = 0.5f; // Reduce deceleration for 0.5 seconds after slide
                }
                _slideVelocity = Vector3.Zero;
            }

            // Decay post-slide inertia timer
            if (_postSlideInertiaTimer > 0f)
                _postSlideInertiaTimer -= deltaTime;

            _wasGroundedLastFrame = IsGrounded;

            // Apply gravity with scale and terminal velocity
            if (EnableGravity && !IsGrounded)
            {
                Velocity += Gravity * GravityScale * deltaTime;

                // Clamp to terminal velocity
                if (Velocity.Y < -TerminalVelocity)
                    Velocity = new Vector3(Velocity.X, -TerminalVelocity, Velocity.Z);
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

                // Check if we're on a slope that affects movement
                bool isOnSlope = CurrentSlopeAngle > 5f; // More than 5 degrees = consider it a slope

                if (isOnSlope && EnableSlopeMomentum)
                {
                    // Apply slope momentum physics
                    ApplySlopeMomentum(deltaTime);
                }
                else
                {
                    // Normal flat ground movement
                    ApplyFlatGroundMovement(deltaTime);
                }
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

            float currentSpeed = horizontalVelocity.Length;
            float desiredSpeed = horizontalDesired.Length;

            Vector3 targetVelocity;
            if (desiredSpeed > 0.01f)
            {
                // Player is inputting movement - accelerate towards desired velocity
                float acceleration = GroundAcceleration * deltaTime;
                targetVelocity = Vector3.Lerp(horizontalVelocity, horizontalDesired, acceleration);
            }
            else
            {
                // No input - decelerate to stop
                float deceleration = GroundDeceleration * deltaTime;

                // Reduce deceleration after exiting a slide (preserve momentum)
                if (_postSlideInertiaTimer > 0f)
                {
                    float inertiaFactor = _postSlideInertiaTimer / 0.5f; // 0-1 based on remaining time
                    deceleration *= (1f - inertiaFactor * 0.7f); // Reduce decel by up to 70%
                }

                targetVelocity = Vector3.Lerp(horizontalVelocity, Vector3.Zero, deceleration);

                // Stop completely if very slow (prevents sliding)
                if (targetVelocity.LengthSquared < 0.01f)
                    targetVelocity = Vector3.Zero;
            }

            // Apply ground friction (independent of input)
            // Also reduced by post-slide inertia
            float friction = GroundFriction;
            if (_postSlideInertiaTimer > 0f)
            {
                float inertiaFactor = _postSlideInertiaTimer / 0.5f;
                friction = MathHelper.Lerp(friction, 1.0f, inertiaFactor * 0.5f); // Less friction after slide
            }
            targetVelocity *= friction;

            // Update velocity (preserve vertical component)
            Velocity = new Vector3(targetVelocity.X, Velocity.Y, targetVelocity.Z);
        }

        /// <summary>
        /// Apply slope momentum physics (like Mario 64).
        /// Allows running up slopes with momentum, deceleration, and backsliding.
        /// </summary>
        private void ApplySlopeMomentum(float deltaTime)
        {
            Vector3 horizontalVelocity = new Vector3(Velocity.X, 0, Velocity.Z);
            Vector3 horizontalDesired = new Vector3(_desiredVelocity.X, 0, _desiredVelocity.Z);

            // Calculate slope direction
            Vector3 slopeDirection = CalculateSlopeDirection(GroundNormal);

            // Determine if we're going uphill or downhill
            // Dot product between velocity and slope down direction
            // > 0 = going downhill, < 0 = going uphill
            float slopeDot = Vector3.Dot(horizontalVelocity, slopeDirection);
            bool isGoingUphill = slopeDot < -0.1f;

            if (isGoingUphill)
            {
                // === CLIMBING SLOPE ===

                // Apply deceleration when climbing (fighting gravity)
                float climbDecel = SlopeClimbDeceleration * deltaTime;

                // Apply player input with reduced effectiveness on slopes
                if (horizontalDesired.LengthSquared > 0.01f)
                {
                    // Blend between current velocity and desired, with slope retention
                    float acceleration = GroundAcceleration * SlopeMomentumRetention * deltaTime;
                    horizontalVelocity = Vector3.Lerp(horizontalVelocity, horizontalDesired, acceleration);
                }

                // Apply climb deceleration (simulates gravity pulling back)
                float currentSpeed = horizontalVelocity.Length;
                currentSpeed = MathF.Max(0f, currentSpeed - climbDecel);

                if (currentSpeed > 0.01f)
                {
                    horizontalVelocity = Vector3.Normalize(horizontalVelocity) * currentSpeed;
                }
                else
                {
                    horizontalVelocity = Vector3.Zero;
                }

                // Check if we should start backsliding
                if (currentSpeed < MinSpeedBeforeBackslide && CurrentSlopeAngle > BackslideAngle)
                {
                    // Start sliding backwards down the slope
                    // This will be picked up by the slide detection on next frame
                    _slideVelocity = slopeDirection * 0.5f; // Small initial backslide velocity
                }
            }
            else
            {
                // === GOING DOWNHILL OR FLAT ===

                // Normal movement with slight speed boost from gravity
                float currentSpeed = horizontalVelocity.Length;
                float desiredSpeed = horizontalDesired.Length;

                Vector3 targetVelocity;
                if (desiredSpeed > 0.01f)
                {
                    // Player input - normal acceleration
                    float acceleration = GroundAcceleration * deltaTime;
                    targetVelocity = Vector3.Lerp(horizontalVelocity, horizontalDesired, acceleration);
                }
                else
                {
                    // No input - decelerate (but slower on downhill)
                    float deceleration = GroundDeceleration * 0.7f * deltaTime; // Less decel downhill
                    targetVelocity = Vector3.Lerp(horizontalVelocity, Vector3.Zero, deceleration);

                    if (targetVelocity.LengthSquared < 0.01f)
                        targetVelocity = Vector3.Zero;
                }

                // Apply friction
                targetVelocity *= GroundFriction;

                horizontalVelocity = targetVelocity;
            }

            // Update velocity (preserve vertical component)
            Velocity = new Vector3(horizontalVelocity.X, Velocity.Y, horizontalVelocity.Z);
        }

        /// <summary>
        /// Apply realistic sliding physics on steep slopes.
        /// Simulates gravity along the slope with friction and player control.
        /// </summary>
        private void ApplySlidePhysics(float deltaTime)
        {
            // Calculate the slope direction (direction of steepest descent)
            // Project gravity onto the slope plane
            Vector3 slopeDirection = CalculateSlopeDirection(GroundNormal);

            // Apply gravity along the slope (accelerate down the slope)
            float gravityMagnitude = Gravity.Length * SlideGravityMultiplier;
            Vector3 slopeGravity = slopeDirection * gravityMagnitude * deltaTime;

            // Accumulate slide velocity
            _slideVelocity += slopeGravity;

            // Apply player input to influence slide direction (limited control)
            Vector3 horizontalDesired = new Vector3(_desiredVelocity.X, 0, _desiredVelocity.Z);
            if (horizontalDesired.LengthSquared > 0.01f && SlideControl > 0f)
            {
                // Project input onto slope plane (can't move perpendicular to slope)
                Vector3 inputOnSlope = ProjectVectorOntoPlane(horizontalDesired, GroundNormal);

                // Apply limited control
                float controlInfluence = SlideControl * deltaTime * 10f; // Scale for responsiveness
                _slideVelocity += inputOnSlope * controlInfluence;
            }

            // Apply slide friction (resists movement in all directions on slope)
            _slideVelocity *= (1f - SlideFriction * deltaTime * 2f);

            // Clamp to max slide speed
            float slideSpeed = _slideVelocity.Length;
            if (slideSpeed > MaxSlideSpeed)
            {
                _slideVelocity = Vector3.Normalize(_slideVelocity) * MaxSlideSpeed;
            }

            // Project slide velocity onto slope plane (ensure we stay on surface)
            _slideVelocity = ProjectVectorOntoPlane(_slideVelocity, GroundNormal);

            // Set final velocity (slide velocity replaces normal velocity while sliding)
            Velocity = _slideVelocity;
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

            // Check physics colliders
            if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, -Vector3.UnitY, out RaycastHit hit, checkDistance, CollisionMask))
            {
                physicsSlopeAngle = Vector3.CalculateAngle(Vector3.UnitY, hit.Normal) * (180f / MathF.PI);

                // Accept ground even if slope is too steep (we'll slide on it instead of falling through)
                hasPhysicsGround = true;
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
            }
        }

        /// <summary>
        /// Determine if the character should be sliding based on slope angle and settings.
        /// </summary>
        private void DetermineSlideState(float slopeAngle)
        {
            if (!EnableSliding)
            {
                // Sliding disabled - use old behavior (can't walk on slopes > SlopeLimit)
                IsSliding = false;
                if (slopeAngle > SlopeLimit)
                {
                    IsGrounded = false; // Too steep, not grounded
                }
                return;
            }

            // Sliding enabled - determine state based on slope angle
            if (slopeAngle >= MinSlideAngle)
            {
                // Very steep slope - always slide
                IsSliding = true;
            }
            else if (slopeAngle > SlopeLimit)
            {
                // Moderately steep - walkable but slower, might slide if moving fast
                IsSliding = false; // For now, don't slide on moderate slopes
            }
            else
            {
                // Normal walkable slope
                IsSliding = false;
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
            if (terrain == null) return;

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
            float smoothFactor = 1f - MathF.Exp(-verticalSmoothSpeed * Engine.Core.Time.FixedDeltaTime);
            float verticalMotion = heightDiff * smoothFactor;

            // Update velocity for interpolation (but don't accumulate, use calculated motion)
            Velocity = new Vector3(
                Velocity.X,
                verticalMotion / Engine.Core.Time.FixedDeltaTime,
                Velocity.Z
            );

            // STEP 4: Apply full motion (horizontal + smooth vertical)
            Vector3 fullMotion = new Vector3(
                targetXZ.X - currentPos.X,
                verticalMotion,
                targetXZ.Z - currentPos.Z
            );

            Entity.Transform.Position = currentPos + fullMotion;
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
            if (foundPhysicsGround && closestSnapDistance < float.MaxValue && closestSnapDistance > 0.001f)
            {
                // Re-run spherecast to obtain the actual hit point so we can compute the exact desired Entity.Y
                if (PhysicsManager.Instance.SphereCast(origin, Radius * 0.9f, -Vector3.UnitY, out RaycastHit physicsHit, snapDistance, CollisionMask))
                {
                    if (physicsHit.Distance > SkinWidth)
                    {
                        // Compute desired Entity.Y such that the absolute bottom of the capsule sits on the hit surface
                        float halfCylinderHeight = (Height - 2f * Radius) * 0.5f;

                        // Special-case handling for primitive colliders: use their top Y instead of the sampled hit point.
                        // This avoids offsets when colliders are centered primitives (box, sphere).
                        float topY;
                        var hitCollider = physicsHit.Collider;
                        if (hitCollider is BoxCollider box)
                        {
                            topY = box.WorldCenter.Y + box.WorldSize.Y * 0.5f;
                        }
                        else if (hitCollider is SphereCollider sphere)
                        {
                            topY = sphere.WorldCenter.Y + sphere.WorldRadius;
                        }
                        else
                        {
                            topY = physicsHit.Point.Y;
                        }

                        float desiredEntityY = topY - Center.Y + halfCylinderHeight + Radius;

                        // Smooth snap like terrain branch
                        float snapSpeed = 20.0f;
                        float maxDelta = snapSpeed * Engine.Core.Time.FixedDeltaTime;
                        float delta = desiredEntityY - currentPos.Y;
                        delta = MathF.Max(-maxDelta, MathF.Min(maxDelta, delta));
                        float newY = currentPos.Y + delta;

                        Entity.Transform.Position = new Vector3(currentPos.X, newY, currentPos.Z);
                    }
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
