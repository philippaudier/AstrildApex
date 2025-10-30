using System;
using OpenTK.Mathematics;
using Engine.Serialization;

namespace Engine.Components
{
    /// <summary>
    /// Transform component - mandatory for all entities, like Unity
    /// </summary>
    public sealed class TransformComponent : Component
    {
        [Serialization.Serializable("position")]
        private Vector3 _position = Vector3.Zero;
        
        [Serialization.Serializable("rotation")]
        private Quaternion _rotation = Quaternion.Identity;
        
        [Serialization.Serializable("scale")]
        private Vector3 _scale = Vector3.One;
        
        public Vector3 Position 
        { 
            get => _position; 
            set 
            { 
                if (_position != value) 
                { 
                    _position = value; 
                    Entity?.NotifyTransformChanged(); 
                } 
            } 
        }
        
        public Quaternion Rotation 
        { 
            get => _rotation; 
            set 
            { 
                if (_rotation != value) 
                { 
                    _rotation = value; 
                    Entity?.NotifyTransformChanged(); 
                } 
            } 
        }
        
        public Vector3 Scale 
        { 
            get => _scale; 
            set 
            { 
                if (_scale != value) 
                { 
                    _scale = value; 
                    Entity?.NotifyTransformChanged(); 
                } 
            } 
        }

        /// <summary>
        /// Get world transform (considering parent hierarchy)
        /// </summary>
        public void GetWorldTRS(out Vector3 pos, out Quaternion rot, out Vector3 scl)
        {
            if (Entity?.Parent == null)
            {
                pos = Position;
                rot = Rotation;
                scl = Scale;
                return;
            }

            var parentTransform = Entity.Parent.GetComponent<TransformComponent>();
            if (parentTransform == null)
            {
                pos = Position;
                rot = Rotation; 
                scl = Scale;
                return;
            }

            parentTransform.GetWorldTRS(out var pp, out var pr, out var ps);

            scl = new Vector3(ps.X * Scale.X, ps.Y * Scale.Y, ps.Z * Scale.Z);
            rot = Quaternion.Normalize(pr * Rotation);
            
            var lpScaled = new Vector3(Position.X * ps.X, Position.Y * ps.Y, Position.Z * ps.Z);
            var lpRot = Vector3.Transform(lpScaled, pr);
            pos = pp + lpRot;
        }

        /// <summary>
        /// Set world transform
        /// </summary>
        public void SetWorldTRS(Vector3 wpos, Quaternion wrot, Vector3 wscl)
        {
            if (Entity?.Parent == null)
            {
                Position = wpos;
                Rotation = wrot;
                Scale = wscl;
                return;
            }

            var parentTransform = Entity.Parent.GetComponent<TransformComponent>();
            if (parentTransform == null)
            {
                Position = wpos;
                Rotation = wrot;
                Scale = wscl;
                return;
            }

            parentTransform.GetWorldTRS(out var pp, out var pr, out var ps);
            var invPr = Quaternion.Invert(pr);

            Rotation = Quaternion.Normalize(invPr * wrot);
            Scale = new Vector3(
                SafeDiv(wscl.X, ps.X),
                SafeDiv(wscl.Y, ps.Y),
                SafeDiv(wscl.Z, ps.Z));

            var delta = wpos - pp;
            var unrot = Vector3.Transform(delta, invPr);
            Position = new Vector3(
                SafeDiv(unrot.X, ps.X),
                SafeDiv(unrot.Y, ps.Y),
                SafeDiv(unrot.Z, ps.Z));
        }

        private static float SafeDiv(float a, float b) => MathF.Abs(b) < 1e-8f ? 0f : a / b;

        public Matrix4 LocalMatrix => 
            Matrix4.CreateScale(Scale) * 
            Matrix4.CreateFromQuaternion(Rotation) * 
            Matrix4.CreateTranslation(Position);

        public Matrix4 WorldMatrix
        {
            get
            {
                var parentTransform = Entity?.Parent?.GetComponent<TransformComponent>();
                return parentTransform == null ? LocalMatrix : LocalMatrix * parentTransform.WorldMatrix;
            }
        }
    }
}