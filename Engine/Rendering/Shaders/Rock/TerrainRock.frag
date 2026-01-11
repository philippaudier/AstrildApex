#version 420 core

#include "../Includes/Common.glsl"

// ============================================================================
// Simple Shadow Mapping (inline - copied from ShadowsSimple.glsl)
// ============================================================================
uniform sampler2DShadow u_ShadowMap;
uniform mat4 u_ShadowMatrix;
uniform int u_UseShadows;
uniform float u_ShadowMapSize;
uniform float u_ShadowBias;
uniform float u_ShadowStrength;

float PCF_Simple(vec2 shadowCoord, float compareDepth)
{
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);

    // 3x3 kernel sampling
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            shadow += texture(u_ShadowMap, vec3(shadowCoord + offset, compareDepth));
        }
    }

    return shadow / 9.0;
}

float CalculateShadow(vec3 worldPos, vec3 normal, vec3 lightDir)
{
    if (u_UseShadows == 0)
        return 1.0;

    float normalDot = max(dot(normal, lightDir), 0.0);
    float slopeBias = u_ShadowBias * sqrt(1.0 - normalDot * normalDot);
    vec3 biasedWorldPos = worldPos + normal * (u_ShadowBias + slopeBias);

    vec4 lightSpacePos = u_ShadowMatrix * vec4(biasedWorldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    projCoords = projCoords * 0.5 + 0.5;

    if (projCoords.x < 0.0 || projCoords.x > 1.0 ||
        projCoords.y < 0.0 || projCoords.y > 1.0 ||
        projCoords.z < 0.0 || projCoords.z > 1.0)
    {
        return 1.0;
    }

    float distFromCenter = length(projCoords.xy - vec2(0.5));
    if (distFromCenter > 0.5)
    {
        return 1.0;
    }

    float shadowValue = PCF_Simple(projCoords.xy, projCoords.z);
    return mix(1.0 - u_ShadowStrength, 1.0, shadowValue);
}
// ============================================================================

// Input from geometry shader (simplified - moss pre-blended in GS)
in GS_OUT {
    vec3 worldPos;
    vec3 normal;
    float aoFactor;
    vec3 rockColor;  // Already includes moss blend
} fs_in;

// Uniforms
uniform vec4 u_BaseColor;
uniform vec4 u_DarkColor;
uniform vec4 u_HighlightColor;
uniform vec4 u_MossColor;
uniform float u_Roughness;
uniform float u_Metallic;

// Lighting
uniform vec3 u_AmbientColor;
uniform float u_AmbientIntensity;
uniform vec3 u_SunDirection;
uniform vec3 u_SunColor;
uniform float u_SunIntensity;

// Snow
uniform float u_SnowCoverage;      // 0-1 snow intensity
uniform float u_SnowAccumulation;  // 0-1 accumulated snow
uniform float u_SnowSlopeMin;      // Minimum slope for snow (degrees)
uniform float u_SnowSlopeMax;      // Maximum slope for snow (degrees)

// Output
out vec4 FragColor;

// Simple noise for surface detail
float hash(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    
    float n = i.x + i.y * 157.0 + 113.0 * i.z;
    return mix(
        mix(mix(hash(vec3(n, 0.0, 0.0)), hash(vec3(n + 1.0, 0.0, 0.0)), f.x),
            mix(hash(vec3(n + 157.0, 0.0, 0.0)), hash(vec3(n + 158.0, 0.0, 0.0)), f.x), f.y),
        mix(mix(hash(vec3(n + 113.0, 0.0, 0.0)), hash(vec3(n + 114.0, 0.0, 0.0)), f.x),
            mix(hash(vec3(n + 270.0, 0.0, 0.0)), hash(vec3(n + 271.0, 0.0, 0.0)), f.x), f.y),
        f.z);
}

void main()
{
    vec3 N = normalize(fs_in.normal);
    vec3 V = normalize(uCameraPos - fs_in.worldPos);
    vec3 L = normalize(-u_SunDirection);
    vec3 H = normalize(L + V);
    
    // === BASE COLOR ===
    
    // Simple surface noise based on world position
    float surfaceNoise = noise3D(fs_in.worldPos * 2.0) * 0.15;
    
    // Mix between dark (crevices via AO) and rock color
    vec3 baseColor = mix(u_DarkColor.rgb, fs_in.rockColor, fs_in.aoFactor);
    baseColor = mix(baseColor, u_HighlightColor.rgb, pow(fs_in.aoFactor, 3.0) * 0.2);
    
    // Add surface noise variation
    baseColor += vec3(surfaceNoise - 0.075);
    
    // fs_in.rockColor already includes moss blend from geometry shader
    vec3 finalColor = baseColor;
    float finalRoughness = u_Roughness;
    
    // === LIGHTING ===

    // Ambient
    vec3 ambient = u_AmbientColor * u_AmbientIntensity * fs_in.aoFactor;

    // Calculate shadows
    float shadowFactor = CalculateShadow(fs_in.worldPos, N, L);

    // Diffuse (Lambert)
    float NdotL = max(dot(N, L), 0.0);
    vec3 diffuse = u_SunColor * u_SunIntensity * NdotL * shadowFactor;

    // Specular (simplified GGX)
    float NdotH = max(dot(N, H), 0.0);
    float roughSq = finalRoughness * finalRoughness;
    float spec = pow(NdotH, mix(128.0, 4.0, roughSq)) * (1.0 - finalRoughness) * 0.3;
    vec3 specular = u_SunColor * spec * (1.0 - u_Metallic * 0.5) * shadowFactor;

    // Fresnel rim lighting for rocks
    float fresnel = pow(1.0 - max(dot(N, V), 0.0), 3.0) * 0.15;
    
    // === FINAL COLOR ===
    vec3 litColor = finalColor * (ambient + diffuse) + specular + fresnel * u_HighlightColor.rgb;

    // Apply snow coverage
    if (u_SnowCoverage > 0.0)
    {
        // Snow appears on upward-facing surfaces within slope range
        // Convert slope constraints from degrees to normal.y range
        float minSlopeY = cos(radians(u_SnowSlopeMax)); // cos(max angle) = min Y
        float maxSlopeY = cos(radians(u_SnowSlopeMin)); // cos(min angle) = max Y

        // Check if surface is within slope range for snow
        float slopeFactor = smoothstep(minSlopeY - 0.1, minSlopeY + 0.1, N.y) *
                           (1.0 - smoothstep(maxSlopeY - 0.1, maxSlopeY + 0.1, N.y));

        float snowFactor = u_SnowCoverage * u_SnowAccumulation * slopeFactor;

        // Add noise to snow edge for natural transition
        float snowNoise = noise3D(fs_in.worldPos * 5.0) * 0.3;
        snowFactor = smoothstep(0.3 + snowNoise, 0.7 + snowNoise, snowFactor);

        // Snow color (bright white with slight blue tint)
        vec3 snowColor = vec3(0.95, 0.96, 0.97);
        vec3 snowLighting = ambient * 1.2 + diffuse * 0.9; // Snow is brighter

        // Blend with snow
        litColor = mix(litColor, snowColor * snowLighting, snowFactor);
    }

    // Simple tone mapping
    litColor = litColor / (litColor + vec3(1.0));

    FragColor = vec4(litColor, 1.0);
}
