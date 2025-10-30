using System;

namespace Engine.Mathx.Noise
{
    /// <summary>
    /// Interface for CPU noise generators
    /// </summary>
    public interface INoise
    {
        /// <summary>
        /// Sample noise at 2D coordinates (x,z) returning value in [-1,1] (or [0,1] depending on implementation docs)
        /// </summary>
        float Sample(float x, float z);

        /// <summary>
        /// Sample fractal fBm/octaves
        /// </summary>
        float SampleFractal(float x, float z, int octaves, float lacunarity, float gain);
    }
}
