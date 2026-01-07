#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;    // HDR scene color
uniform sampler2D u_DepthTexture;     // Depth buffer

// Camera parameters for depth reconstruction
uniform mat4 u_InvProjection;
uniform mat4 u_InvView;
uniform vec3 u_CameraPos;

// Fog parameters
uniform vec3 u_FogColor;
uniform float u_Density;
uniform float u_DepthStart;
uniform float u_DepthEnd;
uniform bool u_UseExponential;

// Height-based fog
uniform bool u_UseHeightFog;
uniform float u_HeightFalloff;
uniform float u_BaseHeight;
uniform float u_MaxHeight;

// Scattering & atmosphere
uniform float u_ScatteringIntensity;
uniform float u_ExtinctionFactor;
uniform vec3 u_SunScatteringColor;
uniform bool u_UseSunScattering;
uniform vec3 u_SunDirection; // Directional light direction

// Noise (optional)
uniform bool u_UseNoise;
uniform float u_NoiseScale;
uniform float u_NoiseSpeed;
uniform float u_NoiseStrength;
uniform float u_Time;

// Simple 3D noise function
float hash(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    return mix(
        mix(mix(hash(i + vec3(0, 0, 0)), hash(i + vec3(1, 0, 0)), f.x),
            mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
        mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x),
            mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y),
        f.z
    );
}

// Reconstruct world position from depth
vec3 worldPositionFromDepth(float depth, vec2 texCoord) {
    // Convert depth to NDC
    float z = depth * 2.0 - 1.0;

    // Reconstruct clip space position
    vec4 clipSpacePosition = vec4(texCoord * 2.0 - 1.0, z, 1.0);

    // Transform to view space
    vec4 viewSpacePosition = u_InvProjection * clipSpacePosition;
    viewSpacePosition /= viewSpacePosition.w;

    // Transform to world space
    vec4 worldSpacePosition = u_InvView * viewSpacePosition;

    return worldSpacePosition.xyz;
}

void main()
{
    vec3 sceneColor = texture(u_SourceTexture, vTexCoord).rgb;
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // Skip fog on skybox (depth = 1.0)
    if (depth >= 0.9999) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    // Reconstruct world position
    vec3 worldPos = worldPositionFromDepth(depth, vTexCoord);

    // Calculate distance from camera
    float distance = length(worldPos - u_CameraPos);

    // === DEPTH-BASED FOG DENSITY ===
    float depthFactor = 1.0;
    if (u_UseExponential) {
        // Exponential fog
        depthFactor = 1.0 - exp(-distance * u_Density);
    } else {
        // Linear fog
        depthFactor = smoothstep(u_DepthStart, u_DepthEnd, distance);
    }

    // === HEIGHT-BASED FOG DENSITY ===
    float heightFactor = 1.0;
    if (u_UseHeightFog) {
        // Exponential height falloff
        float heightAboveBase = max(0.0, worldPos.y - u_BaseHeight);
        heightFactor = exp(-heightAboveBase * u_HeightFalloff);

        // Clamp at max height
        if (worldPos.y > u_MaxHeight) {
            heightFactor *= exp(-(worldPos.y - u_MaxHeight) * u_HeightFalloff * 2.0);
        }
    }

    // === COMBINED DENSITY ===
    float combinedDensity = depthFactor * heightFactor;

    // === NOISE (optional detail) ===
    if (u_UseNoise) {
        vec3 noiseCoord = worldPos * u_NoiseScale + vec3(u_Time * u_NoiseSpeed);
        float noiseValue = noise3D(noiseCoord);
        combinedDensity *= (1.0 - u_NoiseStrength) + noiseValue * u_NoiseStrength;
    }

    // === SUN SCATTERING (optional) ===
    vec3 finalFogColor = u_FogColor;
    if (u_UseSunScattering) {
        vec3 viewDir = normalize(worldPos - u_CameraPos);
        float sunAlignment = max(0.0, dot(viewDir, normalize(-u_SunDirection)));

        // Mie scattering approximation (forward scattering)
        float mieScatter = pow(sunAlignment, 8.0) * u_ScatteringIntensity;

        finalFogColor = mix(u_FogColor, u_SunScatteringColor, mieScatter);
    }

    // === APPLY FOG ===
    // Beer's law for light extinction through fog
    float transmission = exp(-combinedDensity * u_ExtinctionFactor);

    vec3 finalColor = mix(finalFogColor, sceneColor, transmission);

    FragColor = vec4(finalColor, 1.0);
}
