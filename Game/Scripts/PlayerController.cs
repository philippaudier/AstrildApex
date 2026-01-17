using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Engine.Scripting;
using Engine.Components;
using Engine.Input;
using Engine.Inspector;

/// <summary>
/// First-person player controller for exploration game.
/// Works with CameraComponent in FirstPerson mode for mouse look.
/// This controller handles WASD movement and jumping only.
/// </summary>
public class PlayerController : MonoBehaviour
{
    // === REFERENCES ===
    [Editable("Character Controller")]
    public CharacterController? Controller;

    [Editable("Camera")]
    public CameraComponent? Camera;

    // === MOVEMENT SETTINGS ===
    [Editable("Walk Speed")]
    public float WalkSpeed = 5.0f;

    [Editable("Run Speed")]
    public float RunSpeed = 8.0f;

    [Editable("Jump Force")]
    public float JumpForce = 7.0f;

    // === SWIMMING SETTINGS ===
    [Editable("Swim Speed")]
    public float SwimSpeed = 4.0f;

    [Editable("Swim Sprint Speed")]
    public float SwimSprintSpeed = 6.0f;

    [Editable("Dive Speed")]
    [Tooltip("Vertical speed when diving down or swimming up")]
    public float DiveSpeed = 5.0f;

    // === PLAYER ROTATION ===
    [Editable("Rotate With Camera")]
    [Tooltip("If true, player entity rotates to match camera yaw (horizontal look direction)")]
    public bool RotateWithCamera = true;

    public override void Start()
    {
        base.Start();

        if (Entity == null) return;

        // Auto-find components if not assigned
        if (Controller == null)
        {
            Controller = Entity.GetComponent<CharacterController>();
        }

        if (Camera == null)
        {
            // Try to find camera in children
            foreach (var child in Entity.Children)
            {
                var cam = child.GetComponent<CameraComponent>();
                if (cam != null)
                {
                    Camera = cam;
                    break;
                }
            }
        }

        // Set camera to FirstPerson mode if not already configured
        if (Camera != null && Camera.Mode == CameraComponent.ControlMode.Manual)
        {
            Camera.Mode = CameraComponent.ControlMode.FirstPerson;
            var transformComp = Entity.GetComponent<TransformComponent>();
            if (transformComp != null)
            {
                Camera.FollowTarget = transformComp;
            }
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (Controller == null || Entity == null) return;

        // Only rotate player in FirstPerson mode (in ThirdPerson, camera handles rotation)
        if (RotateWithCamera && Camera != null && Camera.Entity != null && Camera.Mode == CameraComponent.ControlMode.FirstPerson)
        {
            // Get camera's world rotation
            Camera.Entity.GetWorldTRS(out _, out var cameraRotation, out _);

            // Extract yaw (Y rotation) from camera quaternion
            var forward = Vector3.Transform(Vector3.UnitZ, cameraRotation);
            float yaw = MathF.Atan2(forward.X, forward.Z);

            // Apply only yaw to player entity (keep standing upright)
            Entity.Transform.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, yaw);
        }

        // KINEMATIC movement is in Update (not FixedUpdate) to stay synchronized with camera
        // No interpolation needed - everything runs in the same Update/LateUpdate loop
        if (Controller.IsSwimming)
        {
            HandleSwimming(deltaTime);
        }
        else
        {
            HandleMovement(deltaTime);
            HandleJump();
        }
    }

    // Debug timer to avoid spamming console
    private float _debugTimer = 0f;

    private void HandleMovement(float deltaTime)
    {
        if (Controller == null || Entity == null) return;

        var im = InputManager.Instance;
        if (im == null) return;

        // Get input
        Vector3 moveInput = Vector3.Zero;

        if (im.GetKey(Keys.W)) moveInput.Z += 1f;
        if (im.GetKey(Keys.S)) moveInput.Z -= 1f;
        if (im.GetKey(Keys.A)) moveInput.X -= 1f;
        if (im.GetKey(Keys.D)) moveInput.X += 1f;

        // Debug output every second
        _debugTimer += deltaTime;
        if (_debugTimer > 1f && moveInput.LengthSquared > 0)
        {
            _debugTimer = 0f;
            Console.WriteLine($"[PlayerController] Input={moveInput}, IsGrounded={Controller.IsGrounded}, EnableMovementFeel={Controller.EnableMovementFeel}, Velocity={Controller.Velocity}");
        }

        // Normalize to prevent faster diagonal movement
        if (moveInput.LengthSquared > 0)
            moveInput = Vector3.Normalize(moveInput);

        // Check if running
        bool isRunning = im.GetKey(Keys.LeftShift);
        float currentSpeed = isRunning ? RunSpeed : WalkSpeed;

        // Convert to world space using CAMERA rotation (not player rotation)
        // This makes WASD movement relative to where you're looking
        Vector3 forward, right;

        if (Camera != null && Camera.Entity != null)
        {
            // Get camera's world rotation
            Camera.Entity.GetWorldTRS(out _, out var cameraRotation, out _);

            // Extract forward and right from camera rotation
            forward = Vector3.Transform(new Vector3(0, 0, 1), cameraRotation);
            right = Vector3.Transform(new Vector3(1, 0, 0), cameraRotation);
        }
        else
        {
            // Fallback to player rotation if no camera
            forward = Vector3.Transform(new Vector3(0, 0, 1), Entity.Transform.Rotation);
            right = Vector3.Transform(new Vector3(1, 0, 0), Entity.Transform.Rotation);
        }

        // Keep movement horizontal (no flying)
        forward.Y = 0;
        right.Y = 0;

        if (forward.LengthSquared > 0) forward = Vector3.Normalize(forward);
        if (right.LengthSquared > 0) right = Vector3.Normalize(right);

        Vector3 moveDirection = (forward * moveInput.Z + right * moveInput.X);

        // Apply movement using the appropriate method
        if (Controller.EnableMovementFeel)
        {
            // NEW SYSTEM: Set desired velocity for smooth acceleration/deceleration
            Vector3 desiredVelocity = moveDirection * currentSpeed;
            Controller.SetDesiredVelocity(desiredVelocity);
        }
        else
        {
            // LEGACY SYSTEM: Direct movement
            if (moveDirection.LengthSquared > 0)
            {
                Vector3 movement = moveDirection * currentSpeed * deltaTime;
                Controller.Move(movement);
            }
        }
    }

    private void HandleJump()
    {
        if (Controller == null) return;

        var im = InputManager.Instance;
        if (im == null) return;

        // Use RequestJump() for smooth jump feel with buffering and coyote time
        // NOTE: Using GetKey instead of GetKeyDown because GetKeyDown doesn't work reliably in Play Mode
        // RequestJump() handles duplicate jump requests internally
        if (im.GetKey(Keys.Space))
        {
            if (Controller.EnableMovementFeel)
            {
                // NEW SYSTEM: Request jump (supports buffering and coyote time)
                Controller.RequestJump(JumpForce);
            }
            else
            {
                // LEGACY SYSTEM: Direct jump
                Controller.Jump(JumpForce);
            }
        }
    }

    private void HandleSwimming(float deltaTime)
    {
        if (Controller == null || Entity == null) return;

        var im = InputManager.Instance;
        if (im == null) return;

        // === HORIZONTAL MOVEMENT (WASD) ===
        Vector3 moveInput = Vector3.Zero;

        if (im.GetKey(Keys.W)) moveInput.Z += 1f;
        if (im.GetKey(Keys.S)) moveInput.Z -= 1f;
        if (im.GetKey(Keys.A)) moveInput.X -= 1f;
        if (im.GetKey(Keys.D)) moveInput.X += 1f;

        // Normalize to prevent faster diagonal movement
        if (moveInput.LengthSquared > 0)
            moveInput = Vector3.Normalize(moveInput);

        // Check if sprinting
        bool isSprinting = im.GetKey(Keys.LeftShift);
        float currentSwimSpeed = isSprinting ? SwimSprintSpeed : SwimSpeed;

        // Convert to world space using CAMERA rotation
        Vector3 forward, right;

        if (Camera != null && Camera.Entity != null)
        {
            Camera.Entity.GetWorldTRS(out _, out var cameraRotation, out _);
            forward = Vector3.Transform(new Vector3(0, 0, 1), cameraRotation);
            right = Vector3.Transform(new Vector3(1, 0, 0), cameraRotation);
        }
        else
        {
            forward = Vector3.Transform(new Vector3(0, 0, 1), Entity.Transform.Rotation);
            right = Vector3.Transform(new Vector3(1, 0, 0), Entity.Transform.Rotation);
        }

        // For swimming, we can move in the direction we're looking (including up/down)
        // But separate horizontal from vertical for better control
        Vector3 horizontalForward = new Vector3(forward.X, 0, forward.Z);
        if (horizontalForward.LengthSquared > 0.001f)
            horizontalForward = Vector3.Normalize(horizontalForward);

        Vector3 horizontalRight = new Vector3(right.X, 0, right.Z);
        if (horizontalRight.LengthSquared > 0.001f)
            horizontalRight = Vector3.Normalize(horizontalRight);

        // Horizontal swim direction
        Vector3 swimDirection = (horizontalForward * moveInput.Z + horizontalRight * moveInput.X);

        // === VERTICAL MOVEMENT (Space/Ctrl/Q/E) ===
        float verticalInput = 0f;

        // Space or E = swim up / surface
        if (im.GetKey(Keys.Space) || im.GetKey(Keys.E))
            verticalInput += 1f;

        // Ctrl, C, or Q = dive down
        if (im.GetKey(Keys.LeftControl) || im.GetKey(Keys.RightControl) || im.GetKey(Keys.C) || im.GetKey(Keys.Q))
            verticalInput -= 1f;

        // Option: W while looking up/down also affects vertical movement
        // This allows swimming in the direction you're looking
        if (moveInput.Z > 0.5f) // Moving forward
        {
            // Add some vertical component based on camera pitch
            verticalInput += forward.Y * 0.5f;
        }

        // === APPLY SWIMMING MOVEMENT ===
        Vector3 desiredHorizontalVelocity = swimDirection * currentSwimSpeed;
        float desiredVerticalVelocity = verticalInput * DiveSpeed;

        // Use the combined swim velocity API
        Controller.SetDesiredVelocity(desiredHorizontalVelocity);
        Controller.SetDesiredSwimVelocity(desiredVerticalVelocity);

        // Debug output - always output when swimming to trace issues
        _debugTimer += deltaTime;
        if (_debugTimer > 0.5f)
        {
            _debugTimer = 0f;
            Console.WriteLine($"[PC] SWIM INPUT: moveInput=({moveInput.X:F1},{moveInput.Z:F1}), vertInput={verticalInput:F1}, desiredH={desiredHorizontalVelocity.Length:F2}, desiredV={desiredVerticalVelocity:F2}");
        }
    }
}
