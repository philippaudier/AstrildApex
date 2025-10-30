using System;
using Engine.Scripting;
using Engine.Components;
using Engine.Inspector;
using OpenTK.Mathematics;

namespace Engine.Scripting
{
    /// <summary>
    /// FloatingSphere behaviour: simple sinusoidal vertical motion and per-frame rotation.
    /// Attach to sphere entities. Matches project MonoBehaviour lifecycle (Start / Update).
    /// </summary>
    public class FloatingSphere : MonoBehaviour
    {
        [Editable] public float Amplitude = 0.25f;      // vertical travel in world units
        [Editable] public float Frequency = 1.0f;       // cycles per second
        [Editable] public Vector3 RotationSpeed = new Vector3(0f, 45f, 0f); // degrees per second
        [Editable] public bool RandomizePhase = true;   // stagger multiple instances

        private Vector3 _startPosition;
        private float _phaseOffset = 0f;
        private float _time = 0f;

        public override void Start()
        {
            base.Start();
            if (Entity == null) return;
            Entity.GetWorldTRS(out var pos, out _, out _);
            _startPosition = pos;

            if (RandomizePhase)
            {
                // Derive a deterministic per-entity phase using the entity id
                _phaseOffset = (Entity.Id % 1000u) / 1000f * MathF.Tau;
            }
            else
            {
                _phaseOffset = 0f;
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
            if (Entity == null) return;

            _time += deltaTime;

            // Vertical offset using sinusoid (uses local start position captured at Start)
            float offsetY = Amplitude * MathF.Sin(2f * MathF.PI * Frequency * _time + _phaseOffset);

            // Rotation delta from degrees/sec -> radians for this frame
            var deltaDeg = RotationSpeed * deltaTime;
            var deltaQ = Quaternion.FromEulerAngles(
                MathHelper.DegreesToRadians(deltaDeg.X),
                MathHelper.DegreesToRadians(deltaDeg.Y),
                MathHelper.DegreesToRadians(deltaDeg.Z)
            );

            // Read current world transform so we only modify position+rotation and preserve scale
            Entity.GetWorldTRS(out var pos, out var rot, out var scl);

            var newPos = new Vector3(_startPosition.X, _startPosition.Y + offsetY, _startPosition.Z);
            var newRot = deltaQ * rot;

            Entity.SetWorldTRS(newPos, newRot, scl);
        }
    }
}
