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
    float aoFactor;
    vec3 rockColor;     // Per-rock color variation includes moss
    vec2 triplanarUV;   // Triplanar UV for texture sampling
    vec3 blendWeights;  // Triplanar blend weights
} fs_in;

// === TEXTURES ===
uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;
uniform sampler2D u_RoughnessTex;
uniform int u_HasAlbedoTex;
uniform int u_HasNormalTex;
uniform int u_HasRoughnessTex;
uniform float u_TextureScale;

// === COLORS & PBR ===
uniform vec4 u_BaseColor;
uniform vec4 u_DarkColor;
uniform vec4 u_HighlightColor;
uniform vec4 u_MossColor;
uniform float u_Roughness;
uniform float u_Metallic;

// === LIGHTING ===
uniform vec3 u_AmbientColor;
uniform float u_AmbientIntensity;

// === SNOW ===
uniform float u_SnowCoverage;
uniform float u_SnowAccumulation;
uniform float u_SnowSlopeMin;
uniform float u_SnowSlopeMax;

// Output
out vec4 FragColor;

// ============================================================================
// PBR Functions (matching grass shader)
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

// Triplanar texture sampling
vec4 sampleTriplanar(sampler2D tex, vec3 worldPos, vec3 blendWeights, float scale)
{
    vec2 uvX = worldPos.yz * scale;
    vec2 uvY = worldPos.xz * scale;
    vec2 uvZ = worldPos.xy * scale;

    vec4 texX = texture(tex, uvX);
    vec4 texY = texture(tex, uvY);
    vec4 texZ = texture(tex, uvZ);

    return texX * blendWeights.x + texY * blendWeights.y + texZ * blendWeights.z;
}

// Triplanar normal sampling with proper tangent space conversion
vec3 sampleTriplanarNormal(sampler2D tex, vec3 worldPos, vec3 normal, vec3 blendWeights, float scale)
{
    vec2 uvX = worldPos.yz * scale;
    vec2 uvY = worldPos.xz * scale;
    vec2 uvZ = worldPos.xy * scale;

    vec3 normalX = texture(tex, uvX).rgb * 2.0 - 1.0;
    vec3 normalY = texture(tex, uvY).rgb * 2.0 - 1.0;
    vec3 normalZ = texture(tex, uvZ).rgb * 2.0 - 1.0;

    // Swizzle normals for each projection axis
    normalX = vec3(normalX.xy + normal.zy, normal.x);
    normalY = vec3(normalY.xy + normal.xz, normal.y);
    normalZ = vec3(normalZ.xy + normal.xy, normal.z);

    // Blend and normalize
    return normalize(normalX * blendWeights.x + normalY * blendWeights.y + normalZ * blendWeights.z);
}

void main()
{
    vec3 N = normalize(fs_in.normal);
    vec3 V = normalize(uCameraPos - fs_in.worldPos);
    vec3 L = normalize(-uDirLightDirection);
    vec3 H = normalize(V + L);

    // Calculate triplanar blend weights from normal
    vec3 blendWeights = abs(N);
    blendWeights = pow(blendWeights, vec3(4.0)); // Sharpen blend
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

    // === BASE COLOR ===
    vec3 albedo;

    if (u_HasAlbedoTex > 0)
    {
        // Sample albedo texture using triplanar projection
        vec4 texColor = sampleTriplanar(u_AlbedoTex, fs_in.worldPos, blendWeights, u_TextureScale);
        albedo = texColor.rgb * fs_in.rockColor;
    }
    else
    {
        // Procedural color from geometry shader
        // Simple surface noise based on world position
        float surfaceNoise = noise3D(fs_in.worldPos * 2.0) * 0.15;

        // Mix between dark (crevices via AO) and rock color
        albedo = mix(u_DarkColor.rgb, fs_in.rockColor, fs_in.aoFactor);
        albedo = mix(albedo, u_HighlightColor.rgb, pow(fs_in.aoFactor, 3.0) * 0.2);

        // Add surface noise variation
        albedo += vec3(surfaceNoise - 0.075);
    }

    // === NORMAL MAPPING ===
    vec3 shadingNormal = N;
    if (u_HasNormalTex > 0)
    {
        shadingNormal = sampleTriplanarNormal(u_NormalTex, fs_in.worldPos, N, blendWeights, u_TextureScale);
    }

    // === ROUGHNESS ===
    float roughness = u_Roughness;
    if (u_HasRoughnessTex > 0)
    {
        roughness = sampleTriplanar(u_RoughnessTex, fs_in.worldPos, blendWeights, u_TextureScale).r;
    }
    roughness = clamp(roughness, 0.04, 1.0);

    // === PBR LIGHTING ===

    // Rocks have low reflectivity (F0 for dielectrics)
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, u_Metallic);

    // Calculate per-light radiance
    vec3 radiance = uDirLightColor * uDirLightIntensity;

    // Cook-Torrance BRDF
    float NDF = DistributionGGX(shadingNormal, H, roughness);
    float G = GeometrySmith(shadingNormal, V, L, roughness);
    vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    vec3 numerator = NDF * G * F;
    float denominator = 4.0 * max(dot(shadingNormal, V), 0.0) * max(dot(shadingNormal, L), 0.0) + 0.0001;
    vec3 specular = numerator / denominator;

    // Energy conservation
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - u_Metallic;

    // Lambertian diffuse
    float NdotL = max(dot(shadingNormal, L), 0.0);

    // Calculate shadows
    float shadowFactor = CalculateShadow(fs_in.worldPos, shadingNormal, L);

    // === AMBIENT OCCLUSION ===
    float ao = fs_in.aoFactor;

    // === COMBINE LIGHTING ===
    vec3 diffuse = kD * albedo / PI;
    vec3 directLighting = (diffuse + specular) * radiance * NdotL * shadowFactor;

    // Ambient lighting with AO
    vec3 ambient = u_AmbientColor * u_AmbientIntensity * albedo * ao;

    // Fresnel rim lighting for rocks (reduced for PBR)
    float fresnel = pow(1.0 - max(dot(shadingNormal, V), 0.0), 4.0) * 0.08;
    vec3 rimLight = fresnel * u_HighlightColor.rgb * ao;

    // Final color
    vec3 finalColor = ambient + directLighting + rimLight;

    // === SNOW COVERAGE ===
    if (u_SnowCoverage > 0.0)
    {
        // Snow appears on upward-facing surfaces within slope range
        float minSlopeY = cos(radians(u_SnowSlopeMax));
        float maxSlopeY = cos(radians(u_SnowSlopeMin));

        float slopeFactor = smoothstep(minSlopeY - 0.1, minSlopeY + 0.1, shadingNormal.y) *
                           (1.0 - smoothstep(maxSlopeY - 0.1, maxSlopeY + 0.1, shadingNormal.y));

        float snowFactor = u_SnowCoverage * u_SnowAccumulation * slopeFactor;

        // Add noise to snow edge
        float snowNoise = noise3D(fs_in.worldPos * 5.0) * 0.3;
        snowFactor = smoothstep(0.3 + snowNoise, 0.7 + snowNoise, snowFactor);

        // Snow PBR properties
        vec3 snowColor = vec3(0.95, 0.96, 0.97);
        float snowRoughness = 0.6;

        // Calculate snow lighting with PBR
        float snowNDF = DistributionGGX(shadingNormal, H, snowRoughness);
        float snowG = GeometrySmith(shadingNormal, V, L, snowRoughness);
        vec3 snowF = FresnelSchlick(max(dot(H, V), 0.0), vec3(0.04));
        vec3 snowSpec = (snowNDF * snowG * snowF) / denominator;

        vec3 snowDiffuse = snowColor / PI;
        vec3 snowLighting = (snowDiffuse + snowSpec * 0.5) * radiance * NdotL * shadowFactor;
        snowLighting += u_AmbientColor * u_AmbientIntensity * snowColor * 1.2;

        finalColor = mix(finalColor, snowLighting, snowFactor);
    }

    // === FOG ===
    if (uFogEnabled > 0) {
        float dist = length(fs_in.worldPos - uCameraPos);
        float fogFactor = smoothstep(uFogStart, uFogEnd, dist) * uFogOpacity;
        finalColor = mix(finalColor, uFogColor, fogFactor);
    }

    FragColor = vec4(finalColor, 1.0);
}
