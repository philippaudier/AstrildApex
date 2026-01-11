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

// Input from geometry shader
in GS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;
    float heightFactor;
    vec3 color;
} fs_in;

// Output
layout(location = 0) out vec4 FragColor;

// Grass texture (optional)
uniform sampler2D u_AlbedoTex;
uniform bool u_HasAlbedoTex;

// Lighting
uniform vec3 u_AmbientColor;
uniform float u_AmbientIntensity;

// Snow
uniform float u_SnowCoverage;      // 0-1 snow intensity
uniform float u_SnowAccumulation;  // 0-1 accumulated snow

void main()
{
    // Base color from geometry shader
    vec3 albedo = fs_in.color;
    
    // Apply texture if available
    if (u_HasAlbedoTex)
    {
        vec4 texColor = texture(u_AlbedoTex, fs_in.uv);
        albedo *= texColor.rgb;
        
        // Alpha clipping for grass texture
        if (texColor.a < 0.3)
            discard;
    }
    
    // Simple lighting (ambient + directional)
    vec3 normal = normalize(fs_in.normal);
    vec3 lightDir = normalize(uDirLightDirection);

    // Ambient
    vec3 ambient = u_AmbientColor * u_AmbientIntensity;

    // Diffuse (wrap lighting for softer grass)
    float NdotL = dot(normal, -lightDir);
    float wrap = 0.5; // Wrap factor for subsurface scattering approximation
    float diffuse = max(0.0, (NdotL + wrap) / (1.0 + wrap));

    // Calculate shadows
    float shadowFactor = CalculateShadow(fs_in.worldPos, normal, -lightDir);

    vec3 directional = uDirLightColor * uDirLightIntensity * diffuse * shadowFactor;

    // Combine lighting
    vec3 lighting = ambient + directional;
    vec3 finalColor = albedo * lighting;

    // Apply snow coverage
    if (u_SnowCoverage > 0.0)
    {
        // Snow appears on top parts of grass blades (high heightFactor)
        // and on upward-facing surfaces (high normal.y)
        float snowFactor = u_SnowCoverage * u_SnowAccumulation;
        float heightBias = smoothstep(0.3, 0.8, fs_in.heightFactor); // Snow accumulates on upper parts
        float slopeBias = smoothstep(0.3, 0.9, normal.y); // Snow on upward-facing surfaces

        float snowAmount = snowFactor * heightBias * slopeBias;

        // Snow color (bright white)
        vec3 snowColor = vec3(0.95, 0.96, 0.97);

        // Blend with snow
        finalColor = mix(finalColor, snowColor * lighting, snowAmount);
    }

    // Apply simple fog if enabled
    if (uFogEnabled > 0) {
        float dist = length(fs_in.worldPos - uCameraPos);
        float fogFactor = smoothstep(uFogStart, uFogEnd, dist) * uFogOpacity;
        finalColor = mix(finalColor, uFogColor, fogFactor);
    }
    
    // Alpha for soft edges at blade tips
    float alpha = 1.0;
    if (fs_in.heightFactor > 0.8)
    {
        alpha = 1.0 - smoothstep(0.8, 1.0, fs_in.heightFactor);
    }
    
    FragColor = vec4(finalColor, alpha);
}
