#version 420 core

#include "../Includes/Common.glsl"

layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

// Animation time (set by renderer each frame)
uniform float u_Time;

// === GLOBAL WIND/WEATHER PARAMETERS (from WeatherComponent) ===
uniform float u_WindStrength;
uniform vec2 u_WindDirection;
uniform float u_WindSpeed;
uniform float u_WindGustiness;

// === GLOBAL/LOCAL/BLEND SYSTEM ===
// 0 = Use Global (wind from WeatherComponent influences waves)
// 1 = Use Local (material-specific wave parameters)
// 2 = Blend between Local and Global
uniform int u_WaveMode = 0;
uniform float u_WaveBlendFactor = 1.0; // 0 = local, 1 = global

// === LOCAL WATER PARAMETERS (material overrides) ===
uniform float u_WaveSpeed_Local = 1.0;
uniform float u_WaveAmplitude_Local = 0.1;
uniform float u_WaveFrequency_Local = 1.0;
uniform vec2  u_WaveDirection_Local = vec2(1.0, 0.0);

// Two-layer normal animation
uniform float u_NormalMapScale;        // Legacy: global scale for both layers
uniform float u_NormalLayer1Scale;     // Separate tiling scale for layer 1
uniform float u_NormalLayer2Scale;     // Separate tiling scale for layer 2
uniform float u_NormalLayer1Speed;
uniform float u_NormalLayer2Speed;
uniform vec2  u_NormalLayer1Direction_Local;
uniform vec2  u_NormalLayer2Direction_Local;

// Planar reflection matrix
uniform mat4 u_ReflectionViewProj;

// === HELPER FUNCTIONS: Get effective wave parameters ===
float getWaveSpeed() {
    if (u_WaveMode == 0) {
        // Global: Use wind speed from WeatherComponent
        return u_WindSpeed;
    }
    if (u_WaveMode == 1) return u_WaveSpeed_Local;
    return mix(u_WaveSpeed_Local, u_WindSpeed, u_WaveBlendFactor);
}

float getWaveAmplitude() {
    if (u_WaveMode == 0) {
        // Global: Scale amplitude by wind strength
        return u_WaveAmplitude_Local * (0.5 + u_WindStrength * 0.5);
    }
    if (u_WaveMode == 1) return u_WaveAmplitude_Local;
    float globalAmplitude = u_WaveAmplitude_Local * (0.5 + u_WindStrength * 0.5);
    return mix(u_WaveAmplitude_Local, globalAmplitude, u_WaveBlendFactor);
}

float getWaveFrequency() {
    if (u_WaveMode == 0) {
        // Global: Slightly vary frequency with wind
        return u_WaveFrequency_Local * (0.8 + u_WindGustiness * 0.4);
    }
    if (u_WaveMode == 1) return u_WaveFrequency_Local;
    float globalFreq = u_WaveFrequency_Local * (0.8 + u_WindGustiness * 0.4);
    return mix(u_WaveFrequency_Local, globalFreq, u_WaveBlendFactor);
}

vec2 getWaveDirection() {
    if (u_WaveMode == 0) {
        // Global: Use wind direction for wave direction
        return normalize(u_WindDirection);
    }
    if (u_WaveMode == 1) return u_WaveDirection_Local;
    vec2 globalDir = normalize(u_WindDirection);
    return normalize(mix(u_WaveDirection_Local, globalDir, u_WaveBlendFactor));
}

// Get effective normal layer directions (follow wind in Global mode)
vec2 getNormalLayer1Direction() {
    if (u_WaveMode == 0) {
        // Global: Main layer follows wind direction
        return normalize(u_WindDirection);
    }
    if (u_WaveMode == 1) return u_NormalLayer1Direction_Local;
    vec2 globalDir = normalize(u_WindDirection);
    return normalize(mix(u_NormalLayer1Direction_Local, globalDir, u_WaveBlendFactor));
}

vec2 getNormalLayer2Direction() {
    if (u_WaveMode == 0) {
        // Global: Secondary layer at 45-degree offset for detail
        vec2 windDir = normalize(u_WindDirection);
        float angle = 0.785398; // 45 degrees in radians
        return normalize(vec2(
            windDir.x * cos(angle) - windDir.y * sin(angle),
            windDir.x * sin(angle) + windDir.y * cos(angle)
        ));
    }
    if (u_WaveMode == 1) return u_NormalLayer2Direction_Local;
    vec2 windDir = normalize(u_WindDirection);
    float angle = 0.785398;
    vec2 globalDir = normalize(vec2(
        windDir.x * cos(angle) - windDir.y * sin(angle),
        windDir.x * sin(angle) + windDir.y * cos(angle)
    ));
    return normalize(mix(u_NormalLayer2Direction_Local, globalDir, u_WaveBlendFactor));
}

out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUV;
out vec2 vUVLayer1;  // Animated UVs for first normal map layer
out vec2 vUVLayer2;  // Animated UVs for second normal map layer
out vec4 vScreenPos; // Screen position for depth buffer reading
out vec4 vReflectionPos; // Reflection position in clip space (for planar reflections)

/// <summary>
/// Directional sine wave function for water surface.
/// Uses effective parameters from Global/Local/Blend system.
/// Waves now follow waveDirection parameter.
/// </summary>
float waveHeight(vec2 worldPos, float time, float waveSpeed, float waveFreq, float waveAmplitude, vec2 waveDir)
{
    float x = worldPos.x;
    float z = worldPos.y;

    // Normalize wave direction
    vec2 dir = normalize(waveDir);

    // Project world position onto wave direction for directional waves
    float dirProjection = dot(vec2(x, z), dir);

    // Perpendicular direction for cross-waves
    vec2 perpDir = vec2(-dir.y, dir.x);
    float perpProjection = dot(vec2(x, z), perpDir);

    // Multiple wave layers for more natural look, now directional
    float wave1 = sin(dirProjection * waveFreq + time * waveSpeed) * 0.5;
    float wave2 = sin(perpProjection * waveFreq * 1.3 + time * waveSpeed * 0.8) * 0.3;
    float wave3 = sin((dirProjection + perpProjection) * waveFreq * 0.7 + time * waveSpeed * 1.2) * 0.2;

    return (wave1 + wave2 + wave3) * waveAmplitude;
}

void main()
{
    vec4 worldPos = u_Model * vec4(aPos, 1.0);
    vec3 worldNormal = normalize(u_NormalMat * aNormal);

    // Get effective wave parameters (Global/Local/Blend)
    float waveSpeed = getWaveSpeed();
    float waveAmplitude = getWaveAmplitude();
    float waveFrequency = getWaveFrequency();
    vec2 waveDirection = getWaveDirection();

    // Phase 6: Vertex displacement for waves (optional, controlled by waveAmplitude)
    if (waveAmplitude > 0.0)
    {
        float wave = waveHeight(worldPos.xz, u_Time, waveSpeed, waveFrequency, waveAmplitude, waveDirection);
        worldPos.y += wave;

        // Approximate normal calculation for displaced vertex
        // Calculate neighboring heights for normal estimation
        float epsilon = 0.1;
        float hL = waveHeight(worldPos.xz - vec2(epsilon, 0.0), u_Time, waveSpeed, waveFrequency, waveAmplitude, waveDirection);
        float hR = waveHeight(worldPos.xz + vec2(epsilon, 0.0), u_Time, waveSpeed, waveFrequency, waveAmplitude, waveDirection);
        float hD = waveHeight(worldPos.xz - vec2(0.0, epsilon), u_Time, waveSpeed, waveFrequency, waveAmplitude, waveDirection);
        float hU = waveHeight(worldPos.xz + vec2(0.0, epsilon), u_Time, waveSpeed, waveFrequency, waveAmplitude, waveDirection);

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
    // Get effective normal directions (follow wind in Global mode)
    vec2 normalLayer1Dir = getNormalLayer1Direction();
    vec2 normalLayer2Dir = getNormalLayer2Direction();

    // Layer 1: Main normal map with configurable direction, speed, and scale
    vec2 normalScroll1 = normalLayer1Dir * u_NormalLayer1Speed * u_Time;
    vUVLayer1 = (vUV * u_NormalLayer1Scale) + normalScroll1;

    // Layer 2: Secondary normal map with different direction, speed, and scale for detail
    vec2 normalScroll2 = normalLayer2Dir * u_NormalLayer2Speed * u_Time;
    vUVLayer2 = (vUV * u_NormalLayer2Scale) + normalScroll2;

    // Calculate screen position for depth buffer reading (Phase 2)
    gl_Position = uViewProj * worldPos;
    vScreenPos = gl_Position;

    // Calculate reflection position in clip space (Phase 6: Planar Reflections)
    // This is the standard approach from Rastertek and OpenGL tutorials
    vReflectionPos = u_ReflectionViewProj * worldPos;
}
