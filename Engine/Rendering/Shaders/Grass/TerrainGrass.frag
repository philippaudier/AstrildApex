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

// Terrain color sampling (to match grass to terrain)
uniform sampler2D u_TerrainColorTex;
uniform int u_HasTerrainColorTex;
uniform float u_TerrainColorScale;
uniform float u_TerrainColorInfluence; // 0-1 how much terrain color affects grass

// PBR Parameters
uniform float u_GrassRoughness;        // 0.6-0.9 typical for grass
uniform float u_SubsurfaceStrength;    // Translucency strength
uniform float u_AmbientOcclusionBase;  // AO at grass base (darker)

// Lighting
uniform vec3 u_AmbientColor;
uniform float u_AmbientIntensity;

// Snow
uniform float u_SnowCoverage;      // 0-1 snow intensity
uniform float u_SnowAccumulation;  // 0-1 accumulated snow

// ============================================================================
// PBR Functions
// ============================================================================
// Note: PI is already defined in Common.glsl

// GGX/Trowbridge-Reitz Normal Distribution Function
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return a2 / max(denom, 0.0001);
}

// Schlick-GGX Geometry Function
float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

// Smith's Geometry Function
float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx1 = GeometrySchlickGGX(NdotV, roughness);
    float ggx2 = GeometrySchlickGGX(NdotL, roughness);
    return ggx1 * ggx2;
}

// Fresnel-Schlick approximation
vec3 FresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// Subsurface scattering approximation for grass
vec3 SubsurfaceScattering(vec3 lightDir, vec3 viewDir, vec3 normal, vec3 albedo, float thickness)
{
    // Calculate how much light passes through the grass blade
    vec3 H = normalize(lightDir + normal * 0.5);
    float VdotH = pow(saturate(dot(viewDir, -H)), 2.0);

    // Subsurface color (lighter, more yellow-green for grass)
    vec3 subsurfaceColor = albedo * vec3(1.2, 1.3, 0.9);

    // Thickness affects how much light comes through (thin at tips)
    float subsurface = VdotH * thickness;

    return subsurfaceColor * subsurface;
}

void main()
{
    // Base color from geometry shader
    vec3 albedo = fs_in.color;

    // Sample terrain color to blend grass with ground
    if (u_HasTerrainColorTex > 0 && u_TerrainColorInfluence > 0.0)
    {
        vec2 terrainUV = fs_in.worldPos.xz * u_TerrainColorScale;
        vec3 terrainColor = texture(u_TerrainColorTex, terrainUV).rgb;

        // Blend terrain color more at the base, less at tips
        float terrainBlend = u_TerrainColorInfluence * (1.0 - fs_in.heightFactor * 0.7);
        albedo = mix(albedo, albedo * terrainColor * 1.5, terrainBlend);
    }

    // Apply texture if available
    if (u_HasAlbedoTex)
    {
        vec4 texColor = texture(u_AlbedoTex, fs_in.uv);
        albedo *= texColor.rgb;

        // Alpha clipping for grass texture
        if (texColor.a < 0.3)
            discard;
    }

    // Darken grass color to be more realistic (grass is not as bright as it seems)
    albedo *= 0.7;

    // === PBR LIGHTING ===
    vec3 N = normalize(fs_in.normal);
    vec3 V = normalize(uCameraPos - fs_in.worldPos);
    vec3 L = normalize(-uDirLightDirection);
    vec3 H = normalize(V + L);

    // Grass has low reflectivity (F0 for dielectrics)
    vec3 F0 = vec3(0.04);

    // Get roughness (grass is quite rough)
    float roughness = clamp(u_GrassRoughness, 0.5, 1.0);
    if (roughness < 0.01) roughness = 0.75; // Default if not set

    // Calculate per-light radiance
    vec3 radiance = uDirLightColor * uDirLightIntensity;

    // Cook-Torrance BRDF
    float NDF = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
    vec3 specular = numerator / denominator;

    // Energy conservation
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;

    // Lambertian diffuse
    float NdotL = max(dot(N, L), 0.0);

    // Wrap lighting for softer grass appearance
    float wrap = 0.4;
    float wrappedNdotL = max(0.0, (dot(N, L) + wrap) / (1.0 + wrap));

    // Calculate shadows
    float shadowFactor = CalculateShadow(fs_in.worldPos, N, L);

    // === SUBSURFACE SCATTERING ===
    float subsurfaceStr = u_SubsurfaceStrength;
    if (subsurfaceStr < 0.01) subsurfaceStr = 0.4; // Default

    // Grass blades are thinner at tips (more translucent)
    float thickness = mix(0.2, 1.0, 1.0 - fs_in.heightFactor);
    vec3 subsurface = SubsurfaceScattering(L, V, N, albedo, thickness) * subsurfaceStr;

    // Subsurface is visible even in shadow (light passes through)
    vec3 subsurfaceContrib = subsurface * radiance * (0.3 + 0.7 * shadowFactor);

    // === AMBIENT OCCLUSION ===
    // Grass is darker at the base (occluded by other blades and ground)
    float aoBase = u_AmbientOcclusionBase;
    if (aoBase < 0.01) aoBase = 0.3; // Default

    float ao = mix(aoBase, 1.0, smoothstep(0.0, 0.5, fs_in.heightFactor));

    // === COMBINE LIGHTING ===
    // Diffuse contribution with wrap lighting
    vec3 diffuse = kD * albedo / PI;
    vec3 directLighting = (diffuse * wrappedNdotL + specular * NdotL) * radiance * shadowFactor;

    // Ambient lighting with AO
    vec3 ambient = u_AmbientColor * u_AmbientIntensity * albedo * ao;

    // Final color
    vec3 finalColor = ambient + directLighting + subsurfaceContrib;

    // === SNOW COVERAGE ===
    if (u_SnowCoverage > 0.0)
    {
        float snowFactor = u_SnowCoverage * u_SnowAccumulation;
        float heightBias = smoothstep(0.3, 0.8, fs_in.heightFactor);
        float slopeBias = smoothstep(0.3, 0.9, N.y);
        float snowAmount = snowFactor * heightBias * slopeBias;

        vec3 snowColor = vec3(0.95, 0.96, 0.97);
        vec3 snowLit = snowColor * (ambient + radiance * wrappedNdotL * shadowFactor);
        finalColor = mix(finalColor, snowLit, snowAmount);
    }

    // === FOG ===
    if (uFogEnabled > 0) {
        float dist = length(fs_in.worldPos - uCameraPos);
        float fogFactor = smoothstep(uFogStart, uFogEnd, dist) * uFogOpacity;
        finalColor = mix(finalColor, uFogColor, fogFactor);
    }

    // === ALPHA ===
    // Soft edges at blade tips
    float alpha = 1.0;
    if (fs_in.heightFactor > 0.85)
    {
        alpha = 1.0 - smoothstep(0.85, 1.0, fs_in.heightFactor);
    }

    FragColor = vec4(finalColor, alpha);
}
