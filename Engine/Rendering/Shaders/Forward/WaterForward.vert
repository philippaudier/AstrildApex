#version 420 core

#include "../Includes/Common.glsl"

layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

// Water animation parameters
uniform float u_Time;              // Game time for animation
uniform float u_WaveSpeed;         // Speed of wave animation
uniform float u_WaveAmplitude;     // Height of waves (vertex displacement)
uniform float u_WaveFrequency;     // Frequency of waves
uniform vec2  u_WaveDirection;     // Direction of wave movement (normalized)

// Two-layer normal animation
uniform float u_NormalMapScale;        // Legacy: global scale for both layers
uniform float u_NormalLayer1Scale;     // Separate tiling scale for layer 1
uniform float u_NormalLayer2Scale;     // Separate tiling scale for layer 2
uniform float u_NormalLayer1Speed;
uniform float u_NormalLayer2Speed;
uniform vec2  u_NormalLayer1Direction;
uniform vec2  u_NormalLayer2Direction;

// Planar reflection matrix
uniform mat4 u_ReflectionViewProj;

out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUV;
out vec2 vUVLayer1;  // Animated UVs for first normal map layer
out vec2 vUVLayer2;  // Animated UVs for second normal map layer
out vec4 vScreenPos; // Screen position for depth buffer reading
out vec4 vReflectionPos; // Reflection position in clip space (for planar reflections)

/// <summary>
/// Simple sine wave function for water surface.
/// </summary>
float waveHeight(vec2 worldPos, float time)
{
    float x = worldPos.x;
    float z = worldPos.y;

    // Multiple wave layers for more natural look
    float wave1 = sin(x * u_WaveFrequency + time * u_WaveSpeed) * 0.5;
    float wave2 = sin(z * u_WaveFrequency * 1.3 + time * u_WaveSpeed * 0.8) * 0.3;
    float wave3 = sin((x + z) * u_WaveFrequency * 0.7 + time * u_WaveSpeed * 1.2) * 0.2;

    return (wave1 + wave2 + wave3) * u_WaveAmplitude;
}

void main()
{
    vec4 worldPos = u_Model * vec4(aPos, 1.0);
    vec3 worldNormal = normalize(u_NormalMat * aNormal);

    // Phase 6: Vertex displacement for waves (optional, controlled by u_WaveAmplitude)
    if (u_WaveAmplitude > 0.0)
    {
        float wave = waveHeight(worldPos.xz, u_Time);
        worldPos.y += wave;

        // Approximate normal calculation for displaced vertex
        // Calculate neighboring heights for normal estimation
        float epsilon = 0.1;
        float hL = waveHeight(worldPos.xz - vec2(epsilon, 0.0), u_Time);
        float hR = waveHeight(worldPos.xz + vec2(epsilon, 0.0), u_Time);
        float hD = waveHeight(worldPos.xz - vec2(0.0, epsilon), u_Time);
        float hU = waveHeight(worldPos.xz + vec2(0.0, epsilon), u_Time);

        // Compute gradient
        vec3 tangentX = normalize(vec3(2.0 * epsilon, hR - hL, 0.0));
        vec3 tangentZ = normalize(vec3(0.0, hU - hD, 2.0 * epsilon));
        worldNormal = normalize(cross(tangentZ, tangentX));
    }

    vWorldPos = worldPos.xyz;
    vNormal = worldNormal;

    // Base UV with tiling/offset
    vUV = aUV * u_TextureTiling + u_TextureOffset;

    // Phase 2: Animated UVs for two-layer normal mapping
    // Layer 1: Main normal map with configurable direction, speed, and scale
    vec2 normalScroll1 = u_NormalLayer1Direction * u_NormalLayer1Speed * u_Time;
    vUVLayer1 = (vUV * u_NormalLayer1Scale) + normalScroll1;

    // Layer 2: Secondary normal map with different direction, speed, and scale for detail
    vec2 normalScroll2 = u_NormalLayer2Direction * u_NormalLayer2Speed * u_Time;
    vUVLayer2 = (vUV * u_NormalLayer2Scale) + normalScroll2;

    // Calculate screen position for depth buffer reading (Phase 2)
    gl_Position = uViewProj * worldPos;
    vScreenPos = gl_Position;

    // Calculate reflection position in clip space (Phase 6: Planar Reflections)
    // This is the standard approach from Rastertek and OpenGL tutorials
    vReflectionPos = u_ReflectionViewProj * worldPos;
}
