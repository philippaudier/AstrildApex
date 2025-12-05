using System;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using Engine.Components;
using Engine.Rendering;

namespace Editor.Gizmos
{
    public static class ParticleSystemGizmo
    {
        /// <summary>
        /// Draw gizmos for ParticleSystem visualization in editor
        /// Draws the emission shape and active particles
        /// </summary>
        public static void DrawGizmo(ParticleSystem ps, Matrix4 viewMatrix, Matrix4 projMatrix)
        {
            if (ps?.Entity?.Transform == null) return;

            // Get world transform
            ps.Entity.GetWorldTRS(out var worldPos, out var worldRot, out var worldScale);
            
            // Get gizmo color based on state
            var color = GetGizmoColor(ps);
            var colorVec = new Vector4(color.R, color.G, color.B, color.A);

            // Draw emission shape
            DrawEmissionShape(ps, worldPos, worldRot, worldScale, colorVec);

            // Draw active particles as small points
            if (ps.IsPlaying)
            {
                DrawActiveParticles(ps, colorVec);
            }
        }

        private static void DrawEmissionShape(ParticleSystem ps, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Vector4 color)
        {
            // Note: This is a simplified gizmo that shows basic shapes
            // The actual shapes are implemented in ViewportRenderer with DrawCircle/DrawSphereWire methods
            // For now we just draw a simple visualization hint
            
            float radius = ps.ShapeRadius * Math.Max(worldScale.X, Math.Max(worldScale.Y, worldScale.Z));
            
            switch (ps.Shape)
            {
                case ShapeType.Sphere:
                    // Would draw sphere wireframe - requires access to ViewportRenderer methods
                    break;
                    
                case ShapeType.Cone:
                    // Would draw cone wireframe
                    break;
                    
                case ShapeType.Box:
                    // Would draw box wireframe
                    break;
                    
                case ShapeType.Circle:
                    // Would draw circle wireframe
                    break;
            }
        }

        private static void DrawActiveParticles(ParticleSystem ps, Vector4 color)
        {
            // Draw live particles as small crosses or points
            // This gives visual feedback that particles are being emitted
        }

        /// <summary>
        /// Get a color for particle system gizmo based on state
        /// </summary>
        public static Color4 GetGizmoColor(ParticleSystem ps)
        {
            if (ps == null) return new Color4(0.8f, 0.8f, 0.8f, 0.6f);
            
            if (ps.IsPlaying && !ps.IsPaused)
            {
                return new Color4(0.3f, 1.0f, 0.5f, 0.8f); // Green when playing
            }
            else if (ps.IsPaused)
            {
                return new Color4(1.0f, 0.7f, 0.2f, 0.8f); // Orange when paused
            }
            else
            {
                return new Color4(0.5f, 0.5f, 1.0f, 0.6f); // Blue when stopped
            }
        }
    }
}
