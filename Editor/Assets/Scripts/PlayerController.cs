using System;
using Engine.Scripting;
using Engine.Components;
using OpenTK.Mathematics;
using Engine.Inspector;
using Engine.Input;
using Engine.Physics;
using PhysicsAPI = Engine.Physics.Physics;
using Engine.Scene;
using Editor.Logging;

namespace Engine.Scripting
{
    /// <summary>
    /// Contrôleur simple du joueur - version épurée
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Editable] public CharacterController? controller;
        [Editable] public CameraComponent? camera;
        [Editable] public float moveSpeed = 6f;
        [Editable] public float jumpHeight = 2f;
        [Editable] public bool alignForwardToCamera = true;
        [Editable] public bool alignOnlyWhenMoving = true;
        [Editable] public bool debugInput = false;
        [Editable] public bool debugPhysics = false;

        private bool _prevSpace = false;

        public override void Start()
        {
            if (controller == null)
                controller = GetComponent<CharacterController>();
        }

        public override void Update(float dt)
        {
            base.Update(dt);

            // TEMPORAIREMENT DÉSACTIVÉ pour debug - décommente pour réactiver
            // if (true) return;

            var ctr = controller;
            if (ctr == null || ctr.Entity == null) return;

            // Sync debug physics flag
            ctr.DebugPhysics = debugPhysics;

            // Lire les inputs
            Vector3 moveInput = GetMoveInput();
            bool jumpPressed = GetJumpInput();

            if (debugInput)
            {
                LogManager.LogVerbose($"[PlayerController] Input - Move: {moveInput:F3}, Jump: {jumpPressed}, IsGrounded: {ctr.IsGrounded}", "PlayerController");
            }


            // Calculer le mouvement final
            Vector3 finalMove = Vector3.Zero;

            // Mouvement horizontal
            if (moveInput.LengthSquared > 0.001f)
            {
                Vector3 calculatedMove = CalculateMovement(moveInput);
                finalMove = calculatedMove * dt;

                if (debugInput)
                {
                    LogManager.LogVerbose($"[PlayerController] Calculated move: {calculatedMove:F3}, final move: {finalMove:F3}, dt: {dt:F3}", "PlayerController");
                }

            }

            // Saut
            // Debug jump action + raw space
            var inputManager = InputManager.Instance;
            var map = inputManager?.FindActionMap("Player");
            var jumpAction = map?.FindAction("Jump");
            bool jumpActionPressed = jumpAction?.WasPressedThisFrame == true;
            bool rawSpace = inputManager?.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space) == true;

            if (debugInput)
            {
                LogManager.LogVerbose($"[PlayerController] Jump state - actionWasPressed={jumpActionPressed}, rawSpace={rawSpace}, prevSpace={_prevSpace}, controller.IsGrounded={(controller?.IsGrounded ?? false)}", "PlayerController");
            }


            // rising-edge raw-space fallback: allow jumping when space was not pressed previous frame but is pressed now
            bool rawSpaceRising = rawSpace && !_prevSpace;

            if ((jumpPressed || jumpActionPressed || rawSpaceRising) && ctr.IsGrounded)
            {
                float jumpVelocity = MathF.Sqrt(2f * ctr.Gravity * jumpHeight);
                // Apply vertical impulse via the controller API so it can set suppression before horizontal move
                ctr.AddVerticalImpulse(jumpVelocity);

                if (debugInput)
                {
                    LogManager.LogVerbose($"[PlayerController] Jump! Velocity: {jumpVelocity:F3} (impulse applied via AddVerticalImpulse)", "PlayerController");
                }

            }

            // store for next frame edge detection
            _prevSpace = rawSpace;

            // Appliquer le mouvement
            // finalMove contains horizontal per-frame translation; controller handles vertical internally
            if (finalMove.LengthSquared > 0.001f)
            {
                if (debugInput)
                {
                    LogManager.LogVerbose($"[PlayerController] Applying move: {finalMove:F3}", "PlayerController");
                }
                ctr.Move(finalMove, dt);
            }

            // Rotation vers la direction de la caméra
            if (alignForwardToCamera && camera?.Entity != null)
            {
                bool isMoving = moveInput.LengthSquared > 0.001f;
                if (!alignOnlyWhenMoving || isMoving)
                {
                    AlignToCamera();
                }
            }
        }

        private Vector3 GetMoveInput()
        {
            float forward = 0f, strafe = 0f;

            // Essayer d'abord les action maps
            var inputManager = InputManager.Instance;
            var map = inputManager?.FindActionMap("Player");

            if (map?.IsEnabled == true)
            {
                if (map.FindAction("MoveForward")?.IsPressed == true) forward += 1f;
                if (map.FindAction("MoveBackward")?.IsPressed == true) forward -= 1f;
                if (map.FindAction("MoveRight")?.IsPressed == true) strafe += 1f;
                if (map.FindAction("MoveLeft")?.IsPressed == true) strafe -= 1f;

                if (debugInput && (forward != 0f || strafe != 0f))
                {
                    LogManager.LogVerbose($"[PlayerController] Action map input - Forward: {forward}, Strafe: {strafe}", "PlayerController");
                }

            }
            else if (inputManager != null)
            {
                // Fallback sur les touches directes
                if (inputManager.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.W)) forward += 1f;
                if (inputManager.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.S)) forward -= 1f;
                if (inputManager.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.D)) strafe += 1f;
                if (inputManager.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.A)) strafe -= 1f;

                if (debugInput && (forward != 0f || strafe != 0f))
                {
                    LogManager.LogVerbose($"[PlayerController] Raw key input - Forward: {forward}, Strafe: {strafe}", "PlayerController");
                }
            }
            else if (debugInput)
            {
                LogManager.LogVerbose($"[PlayerController] No input manager found!", "PlayerController");
            }

            return new Vector3(strafe, 0, forward);
        }

        private bool GetJumpInput()
        {
            var inputManager = InputManager.Instance;
            var map = inputManager?.FindActionMap("Player");

            if (map?.IsEnabled == true)
            {
                return map.FindAction("Jump")?.WasPressedThisFrame == true;
            }
            else if (inputManager != null)
            {
                return inputManager.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space);
            }

            return false;
        }

        private Vector3 CalculateMovement(Vector3 input)
        {
            if (input.LengthSquared < 0.001f) return Vector3.Zero;

            Vector3 move;

            var camEntity = camera?.Entity;
            if (camEntity != null)
            {
                // Mouvement relatif à la caméra
                camEntity.GetWorldTRS(out _, out var camRot, out _);
                var camForward = Vector3.Transform(Vector3.UnitZ, camRot);
                camForward.Y = 0f;
                if (camForward.LengthSquared > 1e-6f)
                    camForward.Normalize();
                else
                    camForward = Vector3.UnitZ;
                var camRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, camForward));
                move = camRight * input.X + camForward * input.Z;
            }
            else
            {
                // Mouvement en coordonnées monde
                move = new Vector3(input.X, 0f, input.Z);
            }

            if (move.LengthSquared > 1e-6f) move.Normalize();
            return move * moveSpeed;
        }

        private void AlignToCamera()
        {
            var camEntity = camera?.Entity;
            var ctrlEntity = controller?.Entity;
            if (camEntity == null || ctrlEntity == null) return;

            camEntity.GetWorldTRS(out _, out var camRot, out _);
            var camForward = Vector3.Transform(Vector3.UnitZ, camRot);
            camForward.Y = 0f;

            if (camForward.LengthSquared > 1e-6f)
            {
                camForward.Normalize();
                var newYaw = MathF.Atan2(camForward.X, camForward.Z);
                var newRotation = Quaternion.FromAxisAngle(Vector3.UnitY, newYaw);
                ctrlEntity.Transform.Rotation = newRotation;
            }
        }
    }
}
