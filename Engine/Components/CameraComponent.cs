using System;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Engine.Serialization;
using Engine.Input;
using Engine.Physics;

namespace Engine.Components
{
    /// <summary>
    /// Complete camera component with projection and control logic for various game types
    /// Supports: FPS, Third Person, Top Down, Isometric, 2D Side-Scroller, and Manual modes
    /// </summary>
    public sealed class CameraComponent : Component
    {
        // ========== PROJECTION SETTINGS ==========
        public enum ProjectionMode { Perspective, Orthographic }

        [Serialization.Serializable("projectionMode")]
        public ProjectionMode Projection = ProjectionMode.Perspective;

        [Serialization.Serializable("fieldOfView")]
        public float FieldOfView = MathHelper.DegreesToRadians(60f);

        [Serialization.Serializable("orthoSize")]
        public float OrthoSize = 10f;

        [Serialization.Serializable("near")]
        public float Near = 0.05f;

        [Serialization.Serializable("far")]
        public float Far = 2000f;

        [Serialization.Serializable("isMain")]
        public bool IsMain = false;

        // ========== CAMERA CONTROL MODE ==========
        public enum ControlMode
        {
            Manual,           // No automatic control - user scripts control transform
            FirstPerson,      // FPS camera attached to target (Doom, Quake, etc.)
            ThirdPerson,      // Over-the-shoulder camera (Tomb Raider, Dark Souls, etc.)
            TopDown,          // Top-down view with optional tilt (Diablo, Stardew Valley)
            Isometric,        // Fixed isometric angle (classic RTS, Tactics games)
            SideScroller2D,   // 2D side-view following (Mario, Celeste)
            Orbit             // Free orbit around target (Unity Scene view style)
        }

        [Serialization.Serializable("controlMode")]
        public ControlMode Mode = ControlMode.Manual;

        [Serialization.Serializable("updateStage")]
        public UpdateStage Stage = UpdateStage.LateUpdate;

        public enum UpdateStage { Update, LateUpdate, FixedUpdate }

        // ========== TARGET & FOLLOW SETTINGS ==========
        [Serialization.Serializable("followTarget")]
        public TransformComponent? FollowTarget;

        [Serialization.Serializable("targetOffset")]
        public Vector3 TargetOffset = new Vector3(0f, 1.7f, 0f);

        [Serialization.Serializable("smoothPosition")]
        public float SmoothPosition = 10f;

        [Serialization.Serializable("smoothRotation")]
        public float SmoothRotation = 10f;

        // ========== MOUSE/LOOK CONTROLS ==========
        [Serialization.Serializable("sensitivity")]
        public float Sensitivity = 0.002f;

        [Serialization.Serializable("invertY")]
        public bool InvertY = true;

        [Serialization.Serializable("invertX")]
        public bool InvertX = false;

        [Serialization.Serializable("minPitch")]
        public float MinPitch = -80f;

        [Serialization.Serializable("maxPitch")]
        public float MaxPitch = 85f;

        // ========== DISTANCE/ZOOM CONTROLS ==========
        [Serialization.Serializable("distance")]
        public float Distance = 5f;

        [Serialization.Serializable("minDistance")]
        public float MinDistance = 1f;

        [Serialization.Serializable("maxDistance")]
        public float MaxDistance = 12f;

        [Serialization.Serializable("enableZoom")]
        public bool EnableZoom = true;

        [Serialization.Serializable("zoomSpeed")]
        public float ZoomSpeed = 1.5f;

        [Serialization.Serializable("invertZoomScroll")]
        public bool InvertZoomScroll = false;

        // ========== COLLISION AVOIDANCE ==========
        [Serialization.Serializable("enableCollision")]
        public bool EnableCollision = true;

        [Serialization.Serializable("collisionMargin")]
        public float CollisionMargin = 0.2f;

        [Serialization.Serializable("collisionLayerMask")]
        public int CollisionLayerMask = ~0;

        // ========== MODE-SPECIFIC SETTINGS ==========

        // First Person
        [Serialization.Serializable("fpsEyeOffset")]
        public Vector3 FPSEyeOffset = new Vector3(0f, 1.6f, 0f);

        [Serialization.Serializable("fpsEnableMove")]
        public bool FPSEnableMove = false;

        [Serialization.Serializable("fpsMoveSpeed")]
        public float FPSMoveSpeed = 6f;

        [Serialization.Serializable("fpsSprintMultiplier")]
        public float FPSSprintMultiplier = 1.75f;

        // Top Down
        [Serialization.Serializable("topDownAngle")]
        public float TopDownAngle = 45f; // 0=straight down, 45=typical top-down, 90=side view

        [Serialization.Serializable("topDownRotationSpeed")]
        public float TopDownRotationSpeed = 2f;

        [Serialization.Serializable("topDownAllowRotation")]
        public bool TopDownAllowRotation = true;

        // Isometric
        [Serialization.Serializable("isometricAngle")]
        public float IsometricAngle = 30f; // Standard isometric is ~30 degrees

        [Serialization.Serializable("isometricYaw")]
        public float IsometricYaw = 45f; // 45 degrees for classic isometric

        // 2D Side Scroller
        [Serialization.Serializable("sideScrollerAxis")]
        public Vector3 SideScrollerAxis = Vector3.UnitX; // Which axis to follow (X for side, Y for vertical)

        [Serialization.Serializable("sideScrollerLookAhead")]
        public float SideScrollerLookAhead = 2f; // How far ahead to look based on velocity

        [Serialization.Serializable("sideScrollerDeadZone")]
        public float SideScrollerDeadZone = 1f; // Dead zone before camera starts moving

        // ========== INTERNAL STATE (Not Serialized) ==========
        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _currentDistance = 5f;
        private Vector3 _smoothPosition = Vector3.Zero;
        private Quaternion _smoothRotation = Quaternion.Identity;
        private bool _cursorWasLocked = false;
        private bool _initialized = false;

        // ========== COMPONENT LIFECYCLE ==========

        public override void OnEnable()
        {
            base.OnEnable();
            if (!_initialized)
            {
                InitializeCamera();
                _initialized = true;
            }
        }

        public override void Start()
        {
            base.Start();
            InitializeCamera();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            CleanupCursorState();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            if (Stage == UpdateStage.Update && Mode != ControlMode.Manual)
                UpdateCamera(deltaTime);
        }

        public override void LateUpdate(float deltaTime)
        {
            base.LateUpdate(deltaTime);
            if (Stage == UpdateStage.LateUpdate && Mode != ControlMode.Manual)
                UpdateCamera(deltaTime);
        }

        public override void FixedUpdate(float fixedDeltaTime)
        {
            base.FixedUpdate(fixedDeltaTime);
            if (Stage == UpdateStage.FixedUpdate && Mode != ControlMode.Manual)
                UpdateCamera(fixedDeltaTime);
        }

        // ========== INITIALIZATION & CLEANUP ==========

        private void InitializeCamera()
        {
            if (Entity == null) return;

            // Initialize rotation from current transform
            Entity.GetWorldTRS(out var pos, out var rot, out _);
            var forward = Vector3.Transform(Vector3.UnitZ, rot);
            if (forward.LengthSquared > 1e-6f) forward.Normalize();

            _yaw = MathF.Atan2(forward.X, forward.Z);
            _pitch = MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f));

            _currentDistance = Distance;
            _smoothPosition = pos;
            _smoothRotation = rot;

            // Don't auto-lock cursor - wait for GamePanel focus
            InputManager.Instance?.UnlockCursor();
            _cursorWasLocked = false;
        }

        private void CleanupCursorState()
        {
            Engine.Input.Cursor.lockState = CursorLockMode.None;
            Engine.Input.Cursor.visible = true;
            InputManager.Instance?.UnlockCursor();
            _cursorWasLocked = false;
        }

        // ========== MAIN UPDATE LOOP ==========

        private void UpdateCamera(float deltaTime)
        {
            if (Entity == null) return;

            // Handle cursor locking based on GamePanel focus
            if (!HandleCursorState()) return;

            // Auto-adjust projection mode based on camera control mode
            AutoAdjustProjection();

            switch (Mode)
            {
                case ControlMode.FirstPerson:
                    UpdateFirstPerson(deltaTime);
                    break;
                case ControlMode.ThirdPerson:
                    UpdateThirdPerson(deltaTime);
                    break;
                case ControlMode.TopDown:
                    UpdateTopDown(deltaTime);
                    break;
                case ControlMode.Isometric:
                    UpdateIsometric(deltaTime);
                    break;
                case ControlMode.SideScroller2D:
                    UpdateSideScroller2D(deltaTime);
                    break;
                case ControlMode.Orbit:
                    UpdateOrbit(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Auto-adjust projection mode based on camera control mode for optimal visuals
        /// </summary>
        private void AutoAdjustProjection()
        {
            switch (Mode)
            {
                case ControlMode.TopDown:
                case ControlMode.Isometric:
                case ControlMode.SideScroller2D:
                    // These modes work best with orthographic projection
                    if (Projection != ProjectionMode.Orthographic)
                        Projection = ProjectionMode.Orthographic;
                    break;

                case ControlMode.FirstPerson:
                case ControlMode.ThirdPerson:
                case ControlMode.Orbit:
                    // These modes work best with perspective projection
                    if (Projection != ProjectionMode.Perspective)
                        Projection = ProjectionMode.Perspective;
                    break;
            }
        }

        // ========== CURSOR MANAGEMENT ==========

        private bool HandleCursorState()
        {
            // Check if GamePanel is focused (runtime only) via reflection to avoid circular dependency
            bool isGamePanelFocused = true;
            try
            {
                var editorAssembly = System.Reflection.Assembly.Load("Editor");
                var gamePanelType = editorAssembly?.GetType("Editor.Panels.GamePanel");
                var isWindowFocusedProperty = gamePanelType?.GetProperty("IsWindowFocused", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (isWindowFocusedProperty != null)
                {
                    var value = isWindowFocusedProperty.GetValue(null);
                    if (value is bool focused)
                        isGamePanelFocused = focused;
                }
            }
            catch
            {
                // Not in editor context or reflection failed, proceed normally
            }

            var im = InputManager.Instance;
            bool isMenuOpen = im?.IsMenuVisible ?? false;

            // GamePanel not focused - unlock and skip camera updates
            if (!isGamePanelFocused)
            {
                if (_cursorWasLocked)
                {
                    Engine.Input.Cursor.lockState = CursorLockMode.None;
                    Engine.Input.Cursor.visible = true;
                    InputManager.Instance?.UnlockCursor();
                    _cursorWasLocked = false;
                }
                return false;
            }

            // Menu open - unlock but continue updates
            if (isMenuOpen)
            {
                if (_cursorWasLocked)
                {
                    Engine.Input.Cursor.lockState = CursorLockMode.None;
                    Engine.Input.Cursor.visible = true;
                    InputManager.Instance?.UnlockCursor();
                    _cursorWasLocked = false;
                }
                return false;
            }

            // Lock cursor for gameplay (FPS, ThirdPerson, Orbit modes)
            if (Mode == ControlMode.FirstPerson || Mode == ControlMode.ThirdPerson || Mode == ControlMode.Orbit)
            {
                if (!_cursorWasLocked)
                {
                    Engine.Input.Cursor.lockState = CursorLockMode.Locked;
                    Engine.Input.Cursor.visible = false;
                    _cursorWasLocked = true;
                }
            }

            return true;
        }

        // ========== MODE IMPLEMENTATIONS ==========

        private void UpdateFirstPerson(float deltaTime)
        {
            var follow = FollowTarget;
            if (follow?.Entity == null) return;

            var im = InputManager.Instance;

            // Mouse look
            float dx = im?.MouseDelta.X ?? 0f;
            float dy = im?.MouseDelta.Y ?? 0f;

            _yaw += dx * Sensitivity * (InvertX ? -1f : 1f);
            _pitch += dy * Sensitivity * (InvertY ? 1f : -1f);
            _pitch = MathHelper.Clamp(_pitch,
                MathHelper.DegreesToRadians(MinPitch),
                MathHelper.DegreesToRadians(MaxPitch));

            // Calculate rotation
            var rotation = Quaternion.FromAxisAngle(Vector3.UnitY, _yaw) *
                          Quaternion.FromAxisAngle(Vector3.UnitX, _pitch);

            // Position at eye height
            follow.Entity.GetWorldTRS(out var targetPos, out _, out _);
            var eyePosition = targetPos + FPSEyeOffset;

            // Optional WASD movement
            if (FPSEnableMove && im != null)
            {
                float speed = FPSMoveSpeed;
                if (im!.IsKeyDown(Keys.LeftShift)) speed *= FPSSprintMultiplier;

                var forward = Vector3.Transform(Vector3.UnitZ, rotation);
                var right = Vector3.Transform(Vector3.UnitX, rotation);

                if (im!.IsKeyDown(Keys.W)) eyePosition += forward * speed * deltaTime;
                if (im!.IsKeyDown(Keys.S)) eyePosition -= forward * speed * deltaTime;
                if (im!.IsKeyDown(Keys.D)) eyePosition += right * speed * deltaTime;
                if (im!.IsKeyDown(Keys.A)) eyePosition -= right * speed * deltaTime;
            }

            // Smooth movement
            float t = 1f - MathF.Exp(-SmoothPosition * deltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, eyePosition, t);

            Entity?.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }

        private void UpdateThirdPerson(float deltaTime)
        {
            var follow = FollowTarget;
            if (follow?.Entity == null) return;

            var im = InputManager.Instance;

            // Mouse look
            float dx = im?.MouseDelta.X ?? 0f;
            float dy = im?.MouseDelta.Y ?? 0f;

            _yaw += dx * Sensitivity * (InvertX ? -1f : 1f);
            _pitch += dy * Sensitivity * (InvertY ? 1f : -1f);
            _pitch = MathHelper.Clamp(_pitch,
                MathHelper.DegreesToRadians(MinPitch),
                MathHelper.DegreesToRadians(MaxPitch));

            // Zoom
            if (EnableZoom)
            {
                float scroll = im?.ScrollDelta.Y ?? 0f;
                float zoomDelta = scroll * ZoomSpeed * (InvertZoomScroll ? -1f : 1f);
                _currentDistance = MathHelper.Clamp(_currentDistance - zoomDelta, MinDistance, MaxDistance);
            }

            // Calculate desired position
            follow.Entity.GetWorldTRS(out var targetPos, out _, out _);
            var pivot = targetPos + TargetOffset;

            var rotation = Quaternion.FromAxisAngle(Vector3.UnitY, _yaw) *
                          Quaternion.FromAxisAngle(Vector3.UnitX, _pitch);
            var forward = Vector3.Transform(Vector3.UnitZ, rotation);

            var desiredPosition = pivot - forward * _currentDistance;

            // Collision detection
            float finalDistance = _currentDistance;
            if (EnableCollision)
            {
                finalDistance = PerformCollisionCheck(pivot, desiredPosition, forward, _currentDistance);
            }

            var finalPosition = pivot - forward * finalDistance;

            // Smooth movement
            float t = 1f - MathF.Exp(-SmoothPosition * deltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, finalPosition, t);

            Entity?.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }

        private void UpdateTopDown(float deltaTime)
        {
            var follow = FollowTarget;
            if (follow?.Entity == null) return;

            var im = InputManager.Instance;
            follow.Entity.GetWorldTRS(out var targetPos, out _, out _);

            // Optional rotation with mouse or keys
            if (TopDownAllowRotation)
            {
                float rotationInput = 0f;
                if (im != null)
                {
                    if (im!.IsKeyDown(Keys.Q)) rotationInput = -1f;
                    if (im!.IsKeyDown(Keys.E)) rotationInput = 1f;
                }
                _yaw += rotationInput * TopDownRotationSpeed * deltaTime;
            }

            // TopDownAngle: 0° = straight down (looking at XZ plane), 45° = diagonal, 90° = horizontal
            // We need to look DOWN at the target, so pitch should be negative
            float pitchRad = MathHelper.DegreesToRadians(TopDownAngle - 90f);
            float yawRad = _yaw;

            // Build rotation: yaw first (horizontal rotation), then pitch (look down)
            var rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yawRad) *
                          Quaternion.FromAxisAngle(Vector3.UnitX, pitchRad);

            // Calculate forward vector and position camera ABOVE target looking DOWN
            var forward = Vector3.Transform(Vector3.UnitZ, rotation);
            var desiredPosition = targetPos + TargetOffset + forward * _currentDistance;

            // Smooth movement
            float t = 1f - MathF.Exp(-SmoothPosition * deltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, t);

            Entity?.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }

        private void UpdateIsometric(float deltaTime)
        {
            var follow = FollowTarget;
            if (follow?.Entity == null) return;

            follow.Entity.GetWorldTRS(out var targetPos, out _, out _);

            // Isometric: typically 30° pitch looking down from above, 45° yaw for diagonal view
            // IsometricAngle: angle DOWN from horizontal (30° is standard isometric)
            float pitchRad = MathHelper.DegreesToRadians(IsometricAngle - 90f);
            float yawRad = MathHelper.DegreesToRadians(IsometricYaw);

            // Build rotation: yaw first (diagonal view), then pitch (look down)
            var rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yawRad) *
                          Quaternion.FromAxisAngle(Vector3.UnitX, pitchRad);

            // Calculate forward vector and position camera ABOVE target looking DOWN
            var forward = Vector3.Transform(Vector3.UnitZ, rotation);
            var desiredPosition = targetPos + TargetOffset + forward * _currentDistance;

            // Smooth movement
            float t = 1f - MathF.Exp(-SmoothPosition * deltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, t);

            Entity?.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }

        private void UpdateSideScroller2D(float deltaTime)
        {
            var follow = FollowTarget;
            if (follow?.Entity == null) return;

            follow.Entity.GetWorldTRS(out var targetPos, out _, out _);

            // Calculate look-ahead based on target velocity (if available)
            // TODO: Add Rigidbody component support when physics is implemented
            Vector3 lookAhead = Vector3.Zero;

            // Project position onto follow axis
            var followPos = targetPos + lookAhead;
            var currentProjected = Vector3.Dot(_smoothPosition - TargetOffset, SideScrollerAxis) * SideScrollerAxis;
            var targetProjected = Vector3.Dot(followPos, SideScrollerAxis) * SideScrollerAxis;

            // Apply dead zone
            float delta = (targetProjected - currentProjected).Length;
            if (delta > SideScrollerDeadZone)
            {
                var desiredPosition = TargetOffset + targetProjected;
                float t = 1f - MathF.Exp(-SmoothPosition * deltaTime);
                _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, t);
            }

            // Fixed rotation (looking along the Z axis for side-scroller)
            var rotation = Quaternion.Identity;

            Entity?.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }

        private void UpdateOrbit(float deltaTime)
        {
            var follow = FollowTarget;
            if (follow?.Entity == null) return;

            var im = InputManager.Instance;

            // Free orbit with mouse
            float dx = im?.MouseDelta.X ?? 0f;
            float dy = im?.MouseDelta.Y ?? 0f;

            _yaw += dx * Sensitivity * (InvertX ? -1f : 1f);
            _pitch += dy * Sensitivity * (InvertY ? 1f : -1f);
            _pitch = MathHelper.Clamp(_pitch,
                MathHelper.DegreesToRadians(MinPitch),
                MathHelper.DegreesToRadians(MaxPitch));

            // Zoom
            if (EnableZoom)
            {
                float scroll = im?.ScrollDelta.Y ?? 0f;
                float zoomDelta = scroll * ZoomSpeed * (InvertZoomScroll ? -1f : 1f);
                _currentDistance = MathHelper.Clamp(_currentDistance - zoomDelta, MinDistance, MaxDistance);
            }

            follow.Entity.GetWorldTRS(out var targetPos, out _, out _);
            var pivot = targetPos + TargetOffset;

            var rotation = Quaternion.FromAxisAngle(Vector3.UnitY, _yaw) *
                          Quaternion.FromAxisAngle(Vector3.UnitX, _pitch);
            var forward = Vector3.Transform(Vector3.UnitZ, rotation);

            var desiredPosition = pivot - forward * _currentDistance;

            // Smooth movement
            float t = 1f - MathF.Exp(-SmoothPosition * deltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, desiredPosition, t);

            Entity?.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }

        // ========== COLLISION HELPER ==========

        private float PerformCollisionCheck(Vector3 pivot, Vector3 desiredPosition, Vector3 forward, float distance)
        {
            var direction = (desiredPosition - pivot).Normalized();

            // Get player's collider to ignore it
            // OBSOLETE: Old collision system removed - camera occlusion disabled
            // TODO: Re-implement using new KinematicCharacterController or Physics.Colliders system
            /*
            Collider? playerCollider = null;
            float playerRadius = 0.5f;

            var follow = FollowTarget;
            if (follow?.Entity != null)
            {
                playerCollider = follow.Entity.GetComponent<Collider>();
                var charController = follow.Entity.GetComponent<CharacterController>();
                if (charController != null)
                    playerRadius = charController.Radius;
            }

            var rayOrigin = pivot + direction * (playerRadius + 0.1f);
            var adjustedDistance = distance - (playerRadius + 0.1f);

            var ray = new Ray
            {
                Origin = rayOrigin,
                Direction = direction
            };

            var hits = CollisionSystem.RaycastAll(ray, adjustedDistance, CollisionLayerMask, QueryTriggerInteraction.Ignore);
            */

            // Camera occlusion disabled - old collision system removed - no collision detection
            return distance;
        }

        // ========== PROJECTION MATRICES ==========

        public Matrix4 ViewMatrix
        {
            get
            {
                if (Entity == null) return Matrix4.Identity;
                Entity.GetWorldTRS(out var worldPos, out var worldRot, out _);
                var forward = Vector3.Transform(Vector3.UnitZ, worldRot);
                var up = Vector3.Transform(Vector3.UnitY, worldRot);
                var viewLH = Engine.Mathx.LH.LookAtLH(worldPos, worldPos + forward, up);
                var zflip = Matrix4.CreateScale(1f, 1f, -1f);
                return viewLH * zflip;
            }
        }

        public Matrix4 ProjectionMatrix(float aspect)
        {
            aspect = MathF.Max(0.01f, aspect);
            float near = MathF.Max(0.001f, Near);
            float far = MathF.Max(near + 0.001f, Far);

            return Projection switch
            {
                ProjectionMode.Perspective => Matrix4.CreatePerspectiveFieldOfView(FieldOfView, aspect, near, far),
                ProjectionMode.Orthographic => CreateOrthographic(aspect, near, far),
                _ => Matrix4.CreatePerspectiveFieldOfView(FieldOfView, aspect, near, far)
            };
        }

        private Matrix4 CreateOrthographic(float aspect, float near, float far)
        {
            float height = OrthoSize;
            float width = height * aspect;
            return Matrix4.CreateOrthographic(width, height, near, far);
        }
    }
}
