using System;
using System.Text.Json.Serialization;
using OpenTK.Mathematics;
using Engine.Serialization;
using Engine.Input;
using Engine.Physics;

namespace Engine.Components
{
    public sealed class CameraComponent : Component
    {
        // Type de projection
        public enum ProjectionMode { Perspective, Orthographic, TwoD }

        [Serialization.Serializable("projectionMode")]
        public ProjectionMode Projection = ProjectionMode.Perspective;

        // Params de projection perspective
        [Serialization.Serializable("fieldOfView")]
        public float FieldOfView = MathHelper.DegreesToRadians(60f);

        // Params de projection orthographique
        [Serialization.Serializable("orthoSize")]
        public float OrthoSize = 10f;  // Hauteur de la vue orthographique

        [Serialization.Serializable("near")]
        public float Near = 0.05f;

        [Serialization.Serializable("far")]
        public float Far = 2000f;

        [Serialization.Serializable("isMain")]
        public bool IsMain = false;     // coche une caméra principale (Editor peut en déduire la vue runtime)

    // Keep behaviour metadata for editor convenience, but the component itself
    // no longer performs input or update logic. CameraController MonoBehaviour
    // will handle FPS/Orbit behaviors at runtime.
    public enum UpdateStage { Update, LateUpdate, FixedUpdate }
    public enum Behavior { Manual, FPS, OrbitFollow }
    [Serialization.Serializable("updateStage")] public UpdateStage Stage = UpdateStage.LateUpdate;
    [Serialization.Serializable("behavior")]    public Behavior Mode = Behavior.Manual;

    // Common options (editor-exposed only)
    [Serialization.Serializable("smoothPosition")] public float SmoothPosition = 12f;
    [Serialization.Serializable("smoothRotation")] public float SmoothRotation = 12f;

    // FPS options (editor-exposed, no behavior here)
    [Serialization.Serializable("fpsEnableMove")] public bool FpsEnableMove = false;
    [Serialization.Serializable("fpsMoveSpeed")] public float FpsMoveSpeed = 6f;
    [Serialization.Serializable("fpsSprintMul")] public float FpsSprintMultiplier = 1.75f;
    [Serialization.Serializable("fpsSensitivity")] public float FpsSensitivity = 0.002f;
    [Serialization.Serializable("fpsInvertY")] public bool FpsInvertY = true;

    // Orbit/Follow options (editor-exposed)
    [Serialization.Serializable("followTarget")] public TransformComponent? FollowTarget;
    [Serialization.Serializable("targetOffset")] public Vector3 TargetOffset = new Vector3(0f, 1.7f, 0f);
    [Serialization.Serializable("orbitSensitivity")] public float OrbitSensitivity = 0.002f;
    [Serialization.Serializable("invertLookY")] public bool InvertLookY = true;
    [Serialization.Serializable("invertLookX")] public bool InvertLookX = false;
    [Serialization.Serializable("orbitBehindTarget")] public bool OrbitBehindTarget = true;
    [Serialization.Serializable("minPitchDeg")] public float MinPitchDeg = -80f;
    [Serialization.Serializable("maxPitchDeg")] public float MaxPitchDeg = 85f;
    [Serialization.Serializable("enableZoom")] public bool EnableZoom = true;
    [Serialization.Serializable("invertZoomScroll")] public bool InvertZoomScroll = false;
    [Serialization.Serializable("minDistance")] public float MinDistance = 0f;
    [Serialization.Serializable("maxDistance")] public float MaxDistance = 12f;
    [Serialization.Serializable("zoomSpeed")] public float ZoomSpeed = 1.5f;
    [Serialization.Serializable("zoomSmooth")] public float ZoomSmooth = 12f;

    [Serialization.Serializable("enableCollision")] public bool EnableCollision = false;
    [Serialization.Serializable("collisionRadius")] public float CollisionRadius = 0.2f;
    [Serialization.Serializable("collisionLayerMask")] public int CollisionLayerMask = ~0;

        public Matrix4 ViewMatrix
        {
            get
            {
                if (Entity == null) return Matrix4.Identity;
                // Use world TRS to compute view matrix. The CameraComponent itself
                // is data-only: it derives view from the Entity transform so
                // external scripts (CameraController) should modify the transform.
                Entity.GetWorldTRS(out var worldPos, out var worldRot, out _);
                var forward = Vector3.Transform(Vector3.UnitZ, worldRot);
                var up      = Vector3.Transform(Vector3.UnitY, worldRot);
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
                ProjectionMode.TwoD => CreateOrthographic2D(aspect, near, far),
                _ => Matrix4.CreatePerspectiveFieldOfView(FieldOfView, aspect, near, far)
            };
        }

        private Matrix4 CreateOrthographic(float aspect, float near, float far)
        {
            float height = OrthoSize;
            float width = height * aspect;
            return Matrix4.CreateOrthographic(width, height, near, far);
        }

        private Matrix4 CreateOrthographic2D(float aspect, float near, float far)
        {
            // Mode 2D : projection orthographique centrée
            float height = OrthoSize;
            float width = height * aspect;
            return Matrix4.CreateOrthographic(width, height, near, far);
        }
    }
}
