using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Engine.Inspector;

namespace Engine.Components
{
    /// <summary>
    /// High-performance particle system for visual effects
    /// Inspired by Unity, Unreal Niagara, and Godot's best practices
    /// </summary>
    public sealed class ParticleSystem : Component
    {
        // ==================== CORE SETTINGS ====================

        [Engine.Serialization.Serializable("maxParticles")] [Editable]
        public int MaxParticles = 1000;

        [Engine.Serialization.Serializable("duration")] [Editable]
        public float Duration = 5.0f;

        [Engine.Serialization.Serializable("looping")] [Editable]
        public bool Looping = true;

        [Engine.Serialization.Serializable("playOnAwake")] [Editable]
        public bool PlayOnAwake = true;

        [Engine.Serialization.Serializable("simulationSpace")] [Editable]
        public SimulationSpace Space = SimulationSpace.World;

        // ==================== EMISSION MODULE ====================

        [Engine.Serialization.Serializable("emissionEnabled")] [Editable]
        public bool EmissionEnabled = true;

        [Engine.Serialization.Serializable("emissionRate")] [Editable]
        public float EmissionRate = 100.0f; // particles per second (increased from 10)

        [Engine.Serialization.Serializable("emissionBursts")]
        public List<EmissionBurst> Bursts = new();

        // ==================== SHAPE MODULE ====================

        [Engine.Serialization.Serializable("shapeType")] [Editable]
        public ShapeType Shape = ShapeType.Cone;

        [Engine.Serialization.Serializable("shapeRadius")] [Editable]
        public float ShapeRadius = 1.0f;

        [Engine.Serialization.Serializable("shapeAngle")] [Editable]
        public float ShapeAngle = 25.0f; // For cone

        [Engine.Serialization.Serializable("shapeBox")] [Editable]
        public Vector3 ShapeBox = Vector3.One; // For box

        // ==================== PARTICLE PROPERTIES ====================

        [Engine.Serialization.Serializable("startLifetime")] [Editable]
        public MinMaxCurve StartLifetime = new(5.0f);

        [Engine.Serialization.Serializable("startSpeed")] [Editable]
        public MinMaxCurve StartSpeed = new(5.0f);

        [Engine.Serialization.Serializable("startSize")] [Editable]
        public MinMaxCurve StartSize = new(1.0f);

        [Engine.Serialization.Serializable("startRotation")] [Editable]
        public MinMaxCurve StartRotation = new(0.0f);

        [Engine.Serialization.Serializable("startColor")] [Editable]
        public Color4 StartColor = Color4.White;

        [Engine.Serialization.Serializable("gravityMultiplier")] [Editable]
        public float GravityMultiplier = 0.0f;

        // ==================== VELOCITY OVER LIFETIME ====================

        [Engine.Serialization.Serializable("velocityOverLifetimeEnabled")] [Editable]
        public bool VelocityOverLifetimeEnabled = false;

        [Engine.Serialization.Serializable("velocityOverLifetime")] [Editable]
        public Vector3 VelocityOverLifetime = Vector3.Zero;

        // ==================== COLOR OVER LIFETIME ====================

        [Engine.Serialization.Serializable("colorOverLifetimeEnabled")] [Editable]
        public bool ColorOverLifetimeEnabled = false;

        [Engine.Serialization.Serializable("colorGradient")]
        public ColorGradient ColorOverLifetime = new();

        // ==================== SIZE OVER LIFETIME ====================

        [Engine.Serialization.Serializable("sizeOverLifetimeEnabled")] [Editable]
        public bool SizeOverLifetimeEnabled = false;

        [Engine.Serialization.Serializable("sizeOverLifetimeCurve")] [Editable]
        public AnimationCurve SizeOverLifetime = AnimationCurve.Linear(0, 1, 1, 0);

        // ==================== ROTATION OVER LIFETIME ====================

        [Engine.Serialization.Serializable("rotationOverLifetimeEnabled")] [Editable]
        public bool RotationOverLifetimeEnabled = false;

        [Engine.Serialization.Serializable("rotationOverLifetimeSpeed")] [Editable]
        public float RotationOverLifetimeSpeed = 45.0f; // degrees per second

        // ==================== RENDERER SETTINGS ====================

        [Engine.Serialization.Serializable("renderMode")] [Editable]
        public RenderMode RenderMode = RenderMode.Billboard;

        [Engine.Serialization.Serializable("materialGuid")]
        public Guid MaterialGuid = Guid.Empty;

        [Engine.Serialization.Serializable("sortingMode")] [Editable]
        public SortingMode SortingMode = SortingMode.None;

        // ==================== RUNTIME STATE ====================

        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public int ParticleCount => _aliveCount;

        private Particle[] _particles = Array.Empty<Particle>();
        private int _aliveCount = 0;
        private float _time = 0.0f;
        private float _emissionAccumulator = 0.0f;
        private Random _random = new();

        // ==================== PARTICLE STRUCT ====================

        public struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Lifetime;
            public float Age;
            public float Size;
            public float Rotation;
            public Color4 Color;
            public bool IsAlive;
        }

        // ==================== LIFECYCLE ====================

        public override void Start()
        {
            _particles = new Particle[MaxParticles];

            if (PlayOnAwake)
            {
                Play();
            }
        }

        public override void Update(float dt)
        {
            UpdateInternal(dt);
        }

        // Allow manual update from editor for preview
        public void UpdateEditor(float dt)
        {
            UpdateInternal(dt);
        }

        private void UpdateInternal(float dt)
        {
            if (!IsPlaying || IsPaused)
            {
                return;
            }

            _time += dt;

            // Stop if duration reached and not looping
            if (!Looping && _time >= Duration)
            {
                Stop();
                return;
            }

            // Loop duration
            if (Looping && _time >= Duration)
            {
                _time = 0.0f;
            }

            // Update existing particles
            UpdateParticles(dt);

            // Emit new particles
            if (EmissionEnabled)
            {
                EmitParticles(dt);
            }
        }

        // ==================== PLAYBACK CONTROL ====================

        public void Play()
        {
            // Initialize particle array if needed
            if (_particles == null || _particles.Length != MaxParticles)
            {
                _particles = new Particle[MaxParticles];
            }

            IsPlaying = true;
            IsPaused = false;
            _time = 0.0f;
            _aliveCount = 0;
            _emissionAccumulator = 0.0f;
        }

        public void Stop()
        {
            IsPlaying = false;
            IsPaused = false;
            _time = 0.0f;
            Clear();
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        public void Clear()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].IsAlive = false;
            }
            _aliveCount = 0;
        }

        // ==================== EMISSION ====================

        private void EmitParticles(float dt)
        {
            // Calculate how many particles to emit this frame
            _emissionAccumulator += EmissionRate * dt;

            int particlesToEmit = (int)_emissionAccumulator;
            _emissionAccumulator -= particlesToEmit;

            // Emit particles
            for (int i = 0; i < particlesToEmit; i++)
            {
                EmitParticle();
            }
        }

        private void EmitParticle()
        {
            if (_aliveCount >= MaxParticles) return;

            // Find free slot
            int index = FindFreeParticleSlot();
            if (index == -1) return;

            ref var p = ref _particles[index];

            // Initialize particle
            p.IsAlive = true;
            p.Age = 0.0f;
            p.Lifetime = StartLifetime.Evaluate(_random);
            p.Size = StartSize.Evaluate(_random);
            p.Rotation = StartRotation.Evaluate(_random);
            p.Color = StartColor;

            // Position based on shape
            Vector3 localPos = GetEmissionPosition();
            
            // Store in world space if SimulationSpace is World
            if (Space == SimulationSpace.World)
            {
                Vector3 worldPos = Entity?.Transform?.GetWorldPosition() ?? Vector3.Zero;
                p.Position = localPos + worldPos;
            }
            else
            {
                p.Position = localPos;
            }

            // Velocity based on shape and speed
            Vector3 direction = GetEmissionDirection(p.Position);
            float speed = StartSpeed.Evaluate(_random);
            p.Velocity = direction * speed;

            _aliveCount++;
        }

        private int FindFreeParticleSlot()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].IsAlive)
                    return i;
            }
            return -1;
        }

        // ==================== PARTICLE UPDATE ====================

        private void UpdateParticles(float dt)
        {
            Vector3 worldGravity = new Vector3(0, -9.81f, 0) * GravityMultiplier;

            for (int i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.IsAlive) continue;

                // Age particle
                p.Age += dt;

                // Kill if lifetime exceeded
                if (p.Age >= p.Lifetime)
                {
                    p.IsAlive = false;
                    _aliveCount--;
                    continue;
                }

                // Calculate lifetime ratio (0 to 1)
                float t = p.Age / p.Lifetime;

                // Apply gravity
                p.Velocity += worldGravity * dt;

                // Apply velocity over lifetime
                if (VelocityOverLifetimeEnabled)
                {
                    p.Velocity += VelocityOverLifetime * dt;
                }

                // Update position
                Vector3 transformPos = Entity?.Transform?.Position ?? Vector3.Zero;

                if (Space == SimulationSpace.Local)
                {
                    p.Position += p.Velocity * dt;
                }
                else // World space
                {
                    p.Position += p.Velocity * dt;
                }

                // Update size over lifetime
                if (SizeOverLifetimeEnabled)
                {
                    float sizeMultiplier = SizeOverLifetime.Evaluate(t);
                    p.Size = StartSize.Evaluate(_random) * sizeMultiplier;
                }

                // Update color over lifetime
                if (ColorOverLifetimeEnabled)
                {
                    p.Color = ColorOverLifetime.Evaluate(t);
                }

                // Update rotation over lifetime
                if (RotationOverLifetimeEnabled)
                {
                    p.Rotation += RotationOverLifetimeSpeed * dt;
                }
            }
        }

        // ==================== SHAPE EMISSION ====================

        private Vector3 GetEmissionPosition()
        {
            // Emit particles at local space (relative to ParticleSystem position)
            // World space will be added during rendering
            switch (Shape)
            {
                case ShapeType.Sphere:
                    return RandomInSphere() * ShapeRadius;

                case ShapeType.Cone:
                    return Vector3.Zero;

                case ShapeType.Box:
                    return RandomInBox(ShapeBox);

                case ShapeType.Circle:
                    return RandomOnCircle() * ShapeRadius;

                default:
                    return Vector3.Zero;
            }
        }

        private Vector3 GetEmissionDirection(Vector3 particlePos)
        {
            Quaternion rotation = Entity?.Transform?.Rotation ?? Quaternion.Identity;

            switch (Shape)
            {
                case ShapeType.Sphere:
                    // For sphere, emit radially outward from origin
                    return particlePos.LengthSquared > 0 ? particlePos.Normalized() : Vector3.UnitZ;

                case ShapeType.Cone:
                    // Cone emission along forward (Z) axis
                    Vector3 forward = rotation * Vector3.UnitZ;
                    float angle = ShapeAngle * MathF.PI / 180.0f;
                    return RandomInCone(forward, angle);

                case ShapeType.Box:
                    return rotation * Vector3.UnitZ;

                case ShapeType.Circle:
                    return rotation * Vector3.UnitZ;

                default:
                    return Vector3.UnitY;
            }
        }

        // ==================== RANDOM HELPERS ====================

        private Vector3 RandomInSphere()
        {
            float u = (float)_random.NextDouble();
            float v = (float)_random.NextDouble();
            float theta = u * 2.0f * MathF.PI;
            float phi = MathF.Acos(2.0f * v - 1.0f);
            float r = MathF.Pow((float)_random.NextDouble(), 1.0f / 3.0f);

            float sinTheta = MathF.Sin(theta);
            float cosTheta = MathF.Cos(theta);
            float sinPhi = MathF.Sin(phi);
            float cosPhi = MathF.Cos(phi);

            return new Vector3(
                r * sinPhi * cosTheta,
                r * sinPhi * sinTheta,
                r * cosPhi
            );
        }

        private Vector3 RandomOnCircle()
        {
            float angle = (float)_random.NextDouble() * 2.0f * MathF.PI;
            return new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        }

        private Vector3 RandomInBox(Vector3 size)
        {
            return new Vector3(
                ((float)_random.NextDouble() - 0.5f) * size.X,
                ((float)_random.NextDouble() - 0.5f) * size.Y,
                ((float)_random.NextDouble() - 0.5f) * size.Z
            );
        }

        private Vector3 RandomInCone(Vector3 direction, float angle)
        {
            float u = (float)_random.NextDouble();
            float v = (float)_random.NextDouble();

            float theta = u * 2.0f * MathF.PI;
            float phi = v * angle;

            // Create perpendicular vectors
            Vector3 up = Math.Abs(direction.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitX;
            Vector3 right = Vector3.Cross(direction, up).Normalized();
            up = Vector3.Cross(right, direction).Normalized();

            // Random direction within cone
            float x = MathF.Sin(phi) * MathF.Cos(theta);
            float y = MathF.Sin(phi) * MathF.Sin(theta);
            float z = MathF.Cos(phi);

            return (direction * z + right * x + up * y).Normalized();
        }

        // ==================== PUBLIC ACCESS ====================

        /// <summary>Get read-only access to particles for rendering</summary>
        public ReadOnlySpan<Particle> GetParticles()
        {
            return new ReadOnlySpan<Particle>(_particles);
        }

        /// <summary>Get particle data for rendering (position, size, color, rotation)</summary>
        public void GetRenderData(out Vector3[] positions, out float[] sizes, out Color4[] colors, out float[] rotations)
        {
            int count = _aliveCount;
            positions = new Vector3[count];
            sizes = new float[count];
            colors = new Color4[count];
            rotations = new float[count];

            // For Local space, we need to add the world position offset
            // For World space, particles are already in world coordinates
            Vector3 worldOffset = (Space == SimulationSpace.Local) 
                ? (Entity?.Transform?.GetWorldPosition() ?? Vector3.Zero) 
                : Vector3.Zero;

            int index = 0;
            for (int i = 0; i < _particles.Length && index < count; i++)
            {
                if (_particles[i].IsAlive)
                {
                    positions[index] = _particles[i].Position + worldOffset;
                    sizes[index] = _particles[i].Size;
                    colors[index] = _particles[i].Color;
                    rotations[index] = _particles[i].Rotation;
                    index++;
                }
            }
        }
    }

    // ==================== ENUMS ====================

    public enum SimulationSpace
    {
        World,
        Local
    }

    public enum ShapeType
    {
        Sphere,
        Cone,
        Box,
        Circle
    }

    public enum RenderMode
    {
        Billboard,
        Mesh,
        StretchedBillboard
    }

    public enum SortingMode
    {
        None,
        OldestInFront,
        YoungestInFront,
        ByDistance
    }

    // ==================== HELPER CLASSES ====================

    [System.Serializable]
    public class MinMaxCurve
    {
        [Engine.Serialization.Serializable("constant")]
        public float Constant;
        
        [Engine.Serialization.Serializable("min")]
        public float Min;
        
        [Engine.Serialization.Serializable("max")]
        public float Max;
        
        [Engine.Serialization.Serializable("mode")]
        public CurveMode Mode;

        public MinMaxCurve(float constant)
        {
            Mode = CurveMode.Constant;
            Constant = constant;
            Min = constant;
            Max = constant;
        }

        public float Evaluate(Random random)
        {
            return Mode switch
            {
                CurveMode.Constant => Constant,
                CurveMode.Random => Min + (float)random.NextDouble() * (Max - Min),
                _ => Constant
            };
        }
    }

    public enum CurveMode
    {
        Constant,
        Random,
        Curve
    }

    [System.Serializable]
    public class EmissionBurst
    {
        public float Time;
        public int Count;
        public int Cycles = 1;
    }

    [System.Serializable]
    public class ColorGradient
    {
        public List<ColorKey> Colors = new()
        {
            new ColorKey { Time = 0, Color = Color4.White },
            new ColorKey { Time = 1, Color = Color4.White }
        };

        public Color4 Evaluate(float t)
        {
            if (Colors.Count == 0) return Color4.White;
            if (Colors.Count == 1) return Colors[0].Color;

            // Find the two keys to interpolate between
            for (int i = 0; i < Colors.Count - 1; i++)
            {
                if (t >= Colors[i].Time && t <= Colors[i + 1].Time)
                {
                    float segmentT = (t - Colors[i].Time) / (Colors[i + 1].Time - Colors[i].Time);
                    return LerpColor(Colors[i].Color, Colors[i + 1].Color, segmentT);
                }
            }

            return Colors[^1].Color;
        }

        private Color4 LerpColor(Color4 a, Color4 b, float t)
        {
            return new Color4(
                a.R + (b.R - a.R) * t,
                a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t,
                a.A + (b.A - a.A) * t
            );
        }
    }

    [System.Serializable]
    public class ColorKey
    {
        public float Time;
        public Color4 Color;
    }

    [System.Serializable]
    public class AnimationCurve
    {
        public List<Keyframe> Keys = new();

        public static AnimationCurve Linear(float startTime, float startValue, float endTime, float endValue)
        {
            return new AnimationCurve
            {
                Keys = new List<Keyframe>
                {
                    new Keyframe { Time = startTime, Value = startValue },
                    new Keyframe { Time = endTime, Value = endValue }
                }
            };
        }

        public float Evaluate(float t)
        {
            if (Keys.Count == 0) return 0;
            if (Keys.Count == 1) return Keys[0].Value;

            // Clamp t
            if (t <= Keys[0].Time) return Keys[0].Value;
            if (t >= Keys[^1].Time) return Keys[^1].Value;

            // Find segment
            for (int i = 0; i < Keys.Count - 1; i++)
            {
                if (t >= Keys[i].Time && t <= Keys[i + 1].Time)
                {
                    float segmentT = (t - Keys[i].Time) / (Keys[i + 1].Time - Keys[i].Time);
                    return Keys[i].Value + (Keys[i + 1].Value - Keys[i].Value) * segmentT;
                }
            }

            return Keys[^1].Value;
        }
    }

    [System.Serializable]
    public class Keyframe
    {
        public float Time;
        public float Value;
    }
}
