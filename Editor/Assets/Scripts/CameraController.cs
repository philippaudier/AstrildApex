using System;
using Engine.Scripting;
using Engine.Components;
using OpenTK.Mathematics;
using Engine.Input;
using Engine.Inspector;

namespace Engine.Scripting
{
    /// <summary>
    /// Clean 3rd person camera controller with smooth collision detection
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Editable] public CameraComponent? Camera;
        [Editable] public TransformComponent? Target; // The player to follow

        // Camera settings
        [Editable] public Vector3 Offset = new Vector3(0f, 1.7f, 0f); // Offset from target position (pivot point)
        [Editable] public float Distance = 5f;
        [Editable] public float MinDistance = 1f;
        [Editable] public float MaxDistance = 12f;

        // Mouse control
        [Editable] public float Sensitivity = 0.002f;
        [Editable] public bool InvertY = true;
        [Editable] public float MinPitch = -80f; // degrees
        [Editable] public float MaxPitch = 85f; // degrees

        // Zoom
        [Editable] public float ZoomSpeed = 1.5f;

        // Smoothing
        [Editable] public float Smoothing = 10f;

        // Collision
        [Editable] public float CollisionMargin = 0.2f;
        [Editable] public int CollisionLayerMask = ~0; // All layers

        // Internal state
        private float _yaw = 0f;
        private float _pitch = 0f;
        private float _currentDistance = 5f;
        private Vector3 _smoothPosition = Vector3.Zero;
        private bool _cursorWasLocked = false; // Track lock state to avoid repeated LockCursor() calls

        public override void Start()
        {
            base.Start();

            if (Camera == null)
                Camera = GetComponent<CameraComponent>();

            if (Entity == null) return;

            // Initialize rotation from current transform
            Entity.GetWorldTRS(out var pos, out var rot, out _);
            var forward = Vector3.Transform(Vector3.UnitZ, rot);
            if (forward.LengthSquared > 1e-6f) forward.Normalize();

            _yaw = MathF.Atan2(forward.X, forward.Z);
            _pitch = MathF.Asin(MathHelper.Clamp(forward.Y, -1f, 1f));

            _currentDistance = Distance;
            _smoothPosition = pos;
            
            // Reset cursor state on Start (important for Play Mode restart)
            // Force unlock first to clear any stale state from previous session
            InputManager.Instance?.UnlockCursor();
            _cursorWasLocked = false;
            
            // Initialize cursor state for gameplay (Locked mode: invisible, infinite rotation like Unity FPS)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false; // Hidden and locked for FPS camera
            _cursorWasLocked = true;
            Console.WriteLine("[CameraController] Start - Cursor LOCKED (invisible, FPS mode)");
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            
            // Always clean up cursor state, regardless of current state
            // This handles cases where Play Mode stops with menu open
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            InputManager.Instance?.UnlockCursor();
            _cursorWasLocked = false;
            Console.WriteLine("[CameraController] OnDestroy - Cursor unlocked and cleaned up");
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (Entity == null || Target?.Entity == null) return;

            var im = InputManager.Instance;
            
            // Check if in-game menu is open
            bool isMenuOpen = im?.IsMenuVisible ?? false;
            
            // Manage cursor state based on menu visibility
            if (isMenuOpen)
            {
                // Menu is open - unlock cursor and make it visible (only once)
                if (_cursorWasLocked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    InputManager.Instance?.UnlockCursor();
                    _cursorWasLocked = false;
                    Console.WriteLine("[CameraController] Menu OPEN - cursor visible and unlocked");
                }
                // Don't process camera rotation when menu is open
                return;
            }
            else
            {
                // Menu is closed - lock cursor for FPS camera (only once)
                if (!_cursorWasLocked)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false; // Hidden and locked for FPS camera
                    _cursorWasLocked = true;
                    Console.WriteLine("[CameraController] Menu CLOSED - cursor locked (invisible, FPS mode)");
                }
            }

            // Mouse look (only when cursor is locked and menu closed)
            float dx = im?.MouseDelta.X ?? 0f;
            float dy = im?.MouseDelta.Y ?? 0f;

            _yaw += dx * Sensitivity;
            _pitch += dy * Sensitivity * (InvertY ? 1f : -1f);
            _pitch = MathHelper.Clamp(_pitch,
                MathHelper.DegreesToRadians(MinPitch),
                MathHelper.DegreesToRadians(MaxPitch));

            // Zoom
            float scroll = im?.ScrollDelta.Y ?? 0f;
            _currentDistance = MathHelper.Clamp(_currentDistance - scroll * ZoomSpeed, MinDistance, MaxDistance);

            // Calculate desired camera position
            Target.Entity.GetWorldTRS(out var targetPos, out _, out _);
            var pivot = targetPos + Offset;

            // Calculate rotation
            var rotation = Quaternion.FromAxisAngle(Vector3.UnitY, _yaw) *
                          Quaternion.FromAxisAngle(Vector3.UnitX, _pitch);
            var forward = Vector3.Transform(Vector3.UnitZ, rotation);

            // Desired position behind target
            var desiredPosition = pivot - forward * _currentDistance;

            // Collision detection - raycast from pivot to camera
            float finalDistance = _currentDistance;
            var direction = (desiredPosition - pivot).Normalized();

            // Get player's collider to explicitly ignore it
            Engine.Components.Collider? playerCollider = null;
            float playerRadius = 0.5f; // Default fallback
            if (Target?.Entity != null)
            {
                playerCollider = Target.Entity.GetComponent<Engine.Components.Collider>();

                // Get player capsule radius to offset raycast origin
                var charController = Target.Entity.GetComponent<Engine.Components.CharacterController>();
                if (charController != null)
                {
                    playerRadius = charController.Radius;
                }
            }

            // Start raycast slightly outside the player's capsule to avoid starting inside it
            var rayOrigin = pivot + direction * (playerRadius + 0.1f);
            var adjustedDistance = _currentDistance - (playerRadius + 0.1f);

            var ray = new Engine.Physics.Ray
            {
                Origin = rayOrigin,
                Direction = direction
            };

            // Raycast to find obstacles between pivot and camera
            var hits = Engine.Physics.CollisionSystem.RaycastAll(
                ray,
                adjustedDistance,
                CollisionLayerMask,
                Engine.Physics.QueryTriggerInteraction.Ignore);

            if (hits != null && hits.Count > 0)
            {
                float closestDistance = adjustedDistance;

                foreach (var hit in hits)
                {
                    // Skip the player's collider explicitly (safety check)
                    if (playerCollider != null && hit.ColliderComponent == playerCollider)
                        continue;

                    // Skip the camera entity itself
                    if (hit.ColliderComponent?.Entity == Entity)
                        continue;

                    // IMPORTANT: Only consider hits that are in front of the ray origin
                    // This prevents detecting terrain under the player's feet
                    if (hit.Distance < 0.01f)
                        continue;

                    // Verify the hit point is actually between rayOrigin and desiredPosition
                    var hitPoint = hit.Point;
                    var toPivot = (pivot - hitPoint).Length;
                    var toDesired = (desiredPosition - hitPoint).Length;

                    // If hit is closer to pivot than rayOrigin, or closer to camera than desired, skip it
                    if (toPivot < playerRadius || toDesired > _currentDistance)
                        continue;

                    // Found an obstacle - use closest hit
                    if (hit.Distance < closestDistance)
                    {
                        closestDistance = hit.Distance;
                    }
                }

                // Pull camera in if we hit something
                // Add back the offset we removed at the start
                if (closestDistance < adjustedDistance)
                {
                    finalDistance = MathF.Max(MinDistance, closestDistance + (playerRadius + 0.1f) - CollisionMargin);
                }
            }

            // Final camera position with collision
            var finalPosition = pivot - forward * finalDistance;

            // Smooth movement (position only, rotation is direct for responsiveness)
            float t = 1f - MathF.Exp(-Smoothing * deltaTime);
            _smoothPosition = Vector3.Lerp(_smoothPosition, finalPosition, t);

            // Apply rotation directly for instant mouse response
            // No smoothing on rotation to avoid input lag when moving mouse fast
            Entity.SetWorldTRS(_smoothPosition, rotation, Vector3.One);
        }
    }
}
