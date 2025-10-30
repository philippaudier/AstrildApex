using System;

namespace Engine.Mathx
{
    public static class Mathf
    {
        public static float Clamp(float v, float min, float max)
            => MathF.Min(MathF.Max(v, min), max);

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Lerp(float a, float b, float t)
            => a + (b - a) * t;

        /// <summary>Exponential damping toward target. lambda~10..20 feels snappy.</summary>
        public static float Damp(float current, float target, float lambda, float dt)
            => Lerp(current, target, 1f - MathF.Exp(-lambda * dt));
    }
}
