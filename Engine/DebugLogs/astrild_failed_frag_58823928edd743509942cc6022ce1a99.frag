#version 330 core

// Begin include: ../Includes/Common.glsl
// Common.glsl - Common constants, uniforms, structures and utility functions

// Mathematical constants
#define PI 3.14159265359
#define EPSILON 1e-6

// Global uniform block (shared across all shaders)
layout(std140) uniform Global {
    mat4 uView; mat4 uProj; mat4 uViewProj;
    vec3 uCameraPos; float _pad1;

    vec3 uDirLightDirection; float _pad2;
    vec3 uDirLightColor; float uDirLightIntensity;


    int uPointLightCount; float _pad3; float _pad4; float _pad5;
    vec4 uPointLightPos0; vec4 uPointLightColor0;
    vec4 uPointLightPos1; vec4 uPointLightColor1;
    vec4 uPointLightPos2; vec4 uPointLightColor2;
    vec4 uPointLightPos3; vec4 uPointLightColor3;


    int uSpotLightCount; float _pad6; float _pad7; float _pad8;
    vec4 uSpotLightPos0; vec4 uSpotLightDir0; vec4 uSpotLightColor0; float uSpotLightAngle0; float uSpotLightInnerAngle0; float _pad9; float _pad10;
    vec4 uSpotLightPos1; vec4 uSpotLightDir1; vec4 uSpotLightColor1; float uSpotLightAngle1; float uSpotLightInnerAngle1; float _pad11; float _pad12;

    vec3 uAmbientColor; float uAmbientIntensity;
    vec3 uSkyboxTint; float uSkyboxExposure;

    int uFogEnabled; float _pad13; float _pad14; float _pad15;
    vec3 uFogColor; float uFogStart;
    float uFogEnd; vec3 _pad16;
};

// Runtime debug: flip normal Y component for imported normal maps / conventions
uniform int u_FlipNormalY; // 0 = no, 1 = flip

// Material properties structure
struct MaterialProperties {
    vec3 baseColor;
    float roughness;
    float metallic;
    vec3 F0;
    vec3 normal;
};

// SSAO uniforms (optional, only used when SSAO is enabled)
// Note: These uniforms are always set by the renderer, even when SSAO is disabled
// u_SSAOEnabled = 0 when disabled, 1 when enabled

// Utility functions
float saturate(float x) {
    return clamp(x, 0.0, 1.0);
}

vec3 saturate(vec3 x) {
    return clamp(x, 0.0, 1.0);
}

// Sample and process normal map
vec3 sampleNormalMap(sampler2D normalTex, vec2 uv, float strength, vec3 worldNormal) {
    vec3 normalMap = texture(normalTex, uv).rgb * 2.0 - 1.0;
    // Respect runtime flip flag (DX vs GL) - only flip when requested
    if (u_FlipNormalY == 1) normalMap.y = -normalMap.y;
    normalMap.xy *= strength;

    // Simple normal blending - match PBR shader behavior
    vec3 baseNormal = normalize(worldNormal);
    // Use same factor as PBR shader (0.1) for consistent specular highlights
    return normalize(baseNormal + normalMap * 0.1);
}

// Convert smoothness to roughness
float smoothnessToRoughness(float smoothness) {
    return clamp(1.0 - smoothness, 0.01, 0.99);
}

// Setup material properties from uniforms and textures
MaterialProperties setupMaterialProperties(sampler2D albedoTex, sampler2D normalTex, vec2 uv,
                                          vec4 albedoColor, float normalStrength,
                                          float metallic, float smoothness, vec3 worldNormal) {
    MaterialProperties material;

    material.baseColor = texture(albedoTex, uv).rgb * albedoColor.rgb;
    material.roughness = smoothnessToRoughness(saturate(smoothness));
    material.metallic = saturate(metallic);
    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);
    material.normal = sampleNormalMap(normalTex, uv, normalStrength, worldNormal);

    return material;
}

// End include: ../Includes/Common.glsl
// Begin include: ../Includes/Lighting.glsl
// Lighting.glsl - PBR lighting calculations and light processing

// Fresnel-Schlick approximation (with clamping)
vec3 fresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// GGX/Trowbridge-Reitz normal distribution function
float distributionGGX(float NdotH, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH2 = NdotH * NdotH;
    float denom = NdotH2 * (a2 - 1.0) + 1.0;
    denom = PI * denom * denom;
    return a2 / max(denom, 1e-7);
}

// Smith-GGX geometry using Schlick-GGX per-direction term
float geometrySchlickGGX(float NdotV, float roughness) {
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k);
}

float geometrySmith(float NdotV, float NdotL, float roughness) {
    float ggxV = geometrySchlickGGX(NdotV, roughness);
    float ggxL = geometrySchlickGGX(NdotL, roughness);
    return ggxV * ggxL;
}

// Core PBR lighting calculation
vec3 calculatePBRLighting(vec3 N, vec3 V, vec3 L, vec3 lightColor, float lightIntensity,
                         MaterialProperties material) {
    vec3 H = normalize(V + L);

    // Use looser clamps to avoid hard artifacts; handle small denominators explicitly
    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.0);
    float NdotH = max(dot(N, H), 0.0);
    float HdotV = max(dot(H, V), 0.0);

    // Early out if not lit
    if (NdotL <= 0.0) return vec3(0.0);

    // Calculate PBR terms
    float D = distributionGGX(NdotH, material.roughness);
    float G = geometrySmith(NdotV, NdotL, material.roughness);
    vec3 F = fresnelSchlick(HdotV, material.F0);

    // Specular with epsilon to avoid division issues
    vec3 numerator = D * G * F;
    float denom = 4.0 * max(NdotV, 1e-4) * max(NdotL, 1e-4);
    vec3 spec = numerator / denom;

    // Energy-conserving diffuse
    vec3 kS = F;
    vec3 kD = (vec3(1.0) - kS) * (1.0 - material.metallic);
    vec3 diffuse = kD * material.baseColor / PI;

    return (diffuse + spec) * lightColor * lightIntensity * NdotL;
}

// Directional light calculation
vec3 calculateDirectionalLight(vec3 N, vec3 V, MaterialProperties material) {
    if (uDirLightIntensity <= 0.0) return vec3(0.0);

    vec3 L = normalize(-uDirLightDirection);
    return calculatePBRLighting(N, V, L, uDirLightColor, uDirLightIntensity, material);
}

// Point light calculation with distance attenuation
vec3 calculatePointLight(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material,
                        vec3 lightPos, float range, vec3 lightColor, float intensity) {
    vec3 L = lightPos - worldPos;
    float distance = length(L);
    L /= distance; // normalize

    // Physically plausible attenuation with smooth falloff near range
    float attenuation = 1.0 / (distance * distance + 1.0);
    if (range > 0.0) {
        float distanceRatio = distance / range;
        float windowFunction = pow(saturate(1.0 - pow(distanceRatio, 4.0)), 2.0);
        attenuation *= windowFunction;
    }

    return calculatePBRLighting(N, V, L, lightColor, intensity * attenuation, material);
}

// Spot light calculation with cone attenuation
vec3 calculateSpotLight(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material,
                       vec3 lightPos, float range, vec3 lightDir, vec3 lightColor, float intensity,
                       float outerAngle, float innerAngle) {
    vec3 L = lightPos - worldPos;
    float distance = length(L);
    L = normalize(L);

    // Check if point is within spotlight cone
    float cosOuterAngle = cos(radians(outerAngle * 0.5));
    float cosInnerAngle = cos(radians(innerAngle * 0.5));
    float spotEffect = dot(-L, normalize(lightDir));

    if (spotEffect <= cosOuterAngle) return vec3(0.0);

    // Calculate spot attenuation
    float spotAttenuation = 1.0;
    if (spotEffect < cosInnerAngle) {
        float falloffRange = cosInnerAngle - cosOuterAngle;
        float falloffFactor = (spotEffect - cosOuterAngle) / falloffRange;
        spotAttenuation = smoothstep(0.0, 1.0, falloffFactor);
    }

    // Calculate distance attenuation
    float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
    if (range > 0.0) {
        float rangeFalloff = saturate(1.0 - distance / range);
        attenuation *= rangeFalloff * rangeFalloff;
    }

    return calculatePBRLighting(N, V, L, lightColor, intensity * attenuation * spotAttenuation, material);
}

// Process all point lights
vec3 calculatePointLights(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material) {
    vec3 result = vec3(0.0);

    vec4 pointPos[4] = vec4[4](uPointLightPos0, uPointLightPos1, uPointLightPos2, uPointLightPos3);
    vec4 pointColor[4] = vec4[4](uPointLightColor0, uPointLightColor1, uPointLightColor2, uPointLightColor3);

    for (int i = 0; i < min(uPointLightCount, 4); i++) {
        vec3 lightPos = pointPos[i].xyz;
        float range = pointPos[i].w;
        vec3 lightColor = pointColor[i].rgb;
        float intensity = pointColor[i].a;

        result += calculatePointLight(worldPos, N, V, material, lightPos, range, lightColor, intensity);
    }

    return result;
}

// Process all spot lights
vec3 calculateSpotLights(vec3 worldPos, vec3 N, vec3 V, MaterialProperties material) {
    vec3 result = vec3(0.0);

    vec4 spotPos[2] = vec4[2](uSpotLightPos0, uSpotLightPos1);
    vec4 spotDir[2] = vec4[2](uSpotLightDir0, uSpotLightDir1);
    vec4 spotColor[2] = vec4[2](uSpotLightColor0, uSpotLightColor1);
    float spotAngle[2] = float[2](uSpotLightAngle0, uSpotLightAngle1);
    float spotInnerAngle[2] = float[2](uSpotLightInnerAngle0, uSpotLightInnerAngle1);

    for (int i = 0; i < min(uSpotLightCount, 2); i++) {
        vec3 lightPos = spotPos[i].xyz;
        float range = spotPos[i].w;
        vec3 lightDir = spotDir[i].xyz;
        vec3 lightColor = spotColor[i].rgb;
        float intensity = spotColor[i].a;
        float outerAngle = spotAngle[i];
        float innerAngle = spotInnerAngle[i];

        result += calculateSpotLight(worldPos, N, V, material, lightPos, range, lightDir,
                                   lightColor, intensity, outerAngle, innerAngle);
    }

    return result;
}

// Calculate ambient lighting contribution
vec3 calculateAmbientLighting(MaterialProperties material) {
    return material.baseColor * uAmbientColor * uAmbientIntensity * (1.0 - material.metallic * 0.5);
}

// Calculate ambient lighting with SSAO
vec3 calculateAmbientLightingWithSSAO(MaterialProperties material, vec2 screenCoord, vec2 screenSize,
                                     sampler2D ssaoTexture, int ssaoEnabled, float ssaoStrength) {
    vec3 ambient = calculateAmbientLighting(material);

    // Apply SSAO if enabled
    if (ssaoEnabled != 0) {
        vec2 ssaoUV = screenCoord / screenSize;
        float ssaoFactor = texture(ssaoTexture, ssaoUV).r;

        // Allow full darkening; leave control to ssaoStrength
        ssaoFactor = clamp(ssaoFactor, 0.0, 1.0);
        ssaoFactor = mix(1.0, ssaoFactor, clamp(ssaoStrength, 0.0, 1.0));

        ambient *= ssaoFactor;
    }

    return ambient;
}

// End include: ../Includes/Lighting.glsl
// Begin include: ../Includes/Fog.glsl
// Fog.glsl - Fog calculations and effects

// Calculate linear fog factor
float calculateLinearFogFactor(vec3 worldPos, vec3 cameraPos, float fogStart, float fogEnd) {
    float dist = length(cameraPos - worldPos);
    return saturate((fogEnd - dist) / max(EPSILON, (fogEnd - fogStart)));
}

// Calculate exponential fog factor
float calculateExponentialFogFactor(vec3 worldPos, vec3 cameraPos, float fogDensity) {
    float dist = length(cameraPos - worldPos);
    return 1.0 / exp(dist * fogDensity);
}

// Calculate exponential squared fog factor
float calculateExponentialSquaredFogFactor(vec3 worldPos, vec3 cameraPos, float fogDensity) {
    float dist = length(cameraPos - worldPos);
    float factor = dist * fogDensity;
    return 1.0 / exp(factor * factor);
}

// Apply fog to the final color using linear interpolation
vec3 applyFog(vec3 color, vec3 fogColor, float fogFactor) {
    return mix(fogColor, color, fogFactor);
}

// Main fog processing function (using current fog settings from Global uniform)
vec3 processFog(vec3 color, vec3 worldPos) {
    if (uFogEnabled == 0) return color;

    float fogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
    return applyFog(color, uFogColor, fogFactor);
}

// Enhanced fog processing with multiple fog types (for future use)
vec3 processFogAdvanced(vec3 color, vec3 worldPos, int fogType, float fogDensity) {
    if (uFogEnabled == 0) return color;

    float fogFactor;

    switch (fogType) {
        case 0: // Linear fog
            fogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
            break;
        case 1: // Exponential fog
            fogFactor = calculateExponentialFogFactor(worldPos, uCameraPos, fogDensity);
            break;
        case 2: // Exponential squared fog
            fogFactor = calculateExponentialSquaredFogFactor(worldPos, uCameraPos, fogDensity);
            break;
        default:
            fogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
            break;
    }

    return applyFog(color, uFogColor, fogFactor);
}

// End include: ../Includes/Fog.glsl
// Begin include: ../Includes/Shadows.glsl
// Shadows.glsl - Shadow mapping with Cascaded Shadow Maps (CSM) support
// Use sampler2DShadow when hardware depth compare is enabled (ShadowManager sets CompareRefToTexture)
uniform sampler2DShadow u_ShadowMap;
uniform mat4 u_ShadowMatrix; // Legacy: single shadow map matrix
uniform int u_UseShadows;
uniform float u_ShadowBias; // legacy constant bias (kept for compatibility)
uniform float u_ShadowBiasConst; // constant bias term (in world units)
uniform float u_ShadowSlopeScale; // slope-scale term multiplied by (1 - N·L)
uniform float u_ShadowMapSize; // ex: 2048.0
uniform float u_PCFRadius; // ex: 1.5 pour adoucissement modéré

// CSM uniforms
#define MAX_CASCADES 4
uniform int u_CascadeCount; // Number of cascades (1 = legacy mode, 2-4 = CSM)
uniform mat4 u_CascadeMatrices[MAX_CASCADES]; // Light-space matrices per cascade
uniform float u_CascadeSplits[MAX_CASCADES]; // Far plane distances for each cascade
uniform vec4 u_AtlasTransforms[MAX_CASCADES]; // (scaleX, scaleY, offsetX, offsetY) per cascade

// Forward declarations for overloaded calculateShadow variants to help some drivers
float calculateShadow(vec3 worldPos, vec3 viewPos);
float calculateShadow(vec3 worldPos);
float calculateShadow(vec3 worldPos, vec3 viewPos, vec3 N, vec3 L);

// VRAI PCF avec sampling multiple
// Note: use sampler2DShadow when the shadow texture uses hardware depth comparison.
// PCF sampling with a provided bias value
float sampleShadowPCF(sampler2DShadow shadowMap, vec2 uv, float compareDepth, float bias)
{
    // bias is passed by caller (computed per-sample using normal/L)
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);

    int radius = int(u_PCFRadius);
    int samples = 0;

    // When using sampler2DShadow, texture() does the comparison with the provided reference value.
    // We sample multiple texels around uv and average the results for PCF.
    for (int x = -radius; x <= radius; ++x) {
        for (int y = -radius; y <= radius; ++y) {
            vec2 offset = vec2(x, y) * texelSize;
            // texture(sampler2DShadow, vec3(uv, refDepth)) returns 1.0 if refDepth <= storedDepth (lit), 0.0 otherwise
            float s = texture(shadowMap, vec3(uv + offset, compareDepth - bias));
            shadow += s;
            samples++;
        }
    }

    return shadow / float(samples);
}

// Select cascade index based on view-space Z depth
// Returns -1 if outside all cascades
int selectCascade(float viewZ)
{
    // viewZ is positive (distance from camera)
    for (int i = 0; i < u_CascadeCount; i++)
    {
        if (viewZ <= u_CascadeSplits[i])
            return i;
    }
    return u_CascadeCount - 1; // Use last cascade if beyond all splits
}

// Calculate shadow factor for directional light with CSM support
float calculateDirectionalShadowCSM(vec3 worldPos, vec3 viewPos)
{
    // Select cascade based on view-space depth
    float viewZ = length(viewPos); // Distance from camera
    int cascadeIndex = selectCascade(viewZ);
    
    // Transform to light space using selected cascade matrix
    vec4 lightSpacePos = u_CascadeMatrices[cascadeIndex] * vec4(worldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    
    // Convert from NDC [-1,1] to texture coordinates [0,1]
    projCoords = projCoords * 0.5 + 0.5;
    
    // Apply atlas transform to map to correct tile in shadow atlas
    vec4 atlasTransform = u_AtlasTransforms[cascadeIndex];
    vec2 atlasUV = projCoords.xy * atlasTransform.xy + atlasTransform.zw;
    
    // Check bounds
    if (atlasUV.x < 0.0 || atlasUV.x > 1.0 || 
        atlasUV.y < 0.0 || atlasUV.y > 1.0 || 
        projCoords.z > 1.0) {
        return 1.0; // Outside shadow map = fully lit
    }
    
    // Compute bias using normal-aware slope-scale if provided by uniforms
    // If caller doesn't have N/L, fallback to u_ShadowBiasConst
    float bias = u_ShadowBiasConst;
    // Note: For CSM path we don't have N/L here, so use constant bias
    float shadow = sampleShadowPCF(u_ShadowMap, atlasUV, projCoords.z, bias);
    
    return shadow;
}

// Legacy: Calculate shadow factor for directional light (single shadow map)
float calculateDirectionalShadow(vec3 worldPos)
{
    vec4 lightSpacePos = u_ShadowMatrix * vec4(worldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    projCoords = projCoords * 0.5 + 0.5;
    
    // Check bounds
    if (projCoords.x < 0.0 || projCoords.x > 1.0 || 
        projCoords.y < 0.0 || projCoords.y > 1.0 || 
        projCoords.z > 1.0) {
        return 1.0;
    }
    
    // Legacy: use constant bias by default
    float bias = u_ShadowBiasConst;
    float shadow = sampleShadowPCF(u_ShadowMap, projCoords.xy, projCoords.z, bias);
    
    return shadow;
}

// Main shadow calculation - automatically uses CSM if enabled, legacy otherwise
float calculateShadow(vec3 worldPos, vec3 viewPos) {
    if (u_UseShadows == 0) return 1.0;
    
    // Use CSM if cascade count > 1, otherwise use legacy single shadow map
    if (u_CascadeCount > 1) {
        return calculateDirectionalShadowCSM(worldPos, viewPos);
    } else {
        return calculateDirectionalShadow(worldPos);
    }
}

// Legacy compatibility: calculateShadow without viewPos parameter
float calculateShadow(vec3 worldPos) {
    return calculateShadow(worldPos, vec3(0.0)); // viewPos not available, use legacy path
}

// Overloads that accept normal and light vectors so shaders can compute a normal-aware bias
float calculateDirectionalShadowCSM(vec3 worldPos, vec3 viewPos, vec3 N, vec3 L)
{
    if (u_UseShadows == 0) return 1.0;
    // Select cascade based on view-space depth
    float viewZ = length(viewPos);
    int cascadeIndex = selectCascade(viewZ);

    vec4 lightSpacePos = u_CascadeMatrices[cascadeIndex] * vec4(worldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    projCoords = projCoords * 0.5 + 0.5;

    vec4 atlasTransform = u_AtlasTransforms[cascadeIndex];
    vec2 atlasUV = projCoords.xy * atlasTransform.xy + atlasTransform.zw;

    if (atlasUV.x < 0.0 || atlasUV.x > 1.0 || atlasUV.y < 0.0 || atlasUV.y > 1.0 || projCoords.z > 1.0)
        return 1.0;

    float nDotL = max(dot(normalize(N), normalize(L)), 0.0);
    float bias = u_ShadowBiasConst + u_ShadowSlopeScale * (1.0 - nDotL);
    float shadow = sampleShadowPCF(u_ShadowMap, atlasUV, projCoords.z, bias);
    return shadow;
}

float calculateDirectionalShadow(vec3 worldPos, vec3 N, vec3 L)
{
    vec4 lightSpacePos = u_ShadowMatrix * vec4(worldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    projCoords = projCoords * 0.5 + 0.5;

    if (projCoords.x < 0.0 || projCoords.x > 1.0 || projCoords.y < 0.0 || projCoords.y > 1.0 || projCoords.z > 1.0)
        return 1.0;

    float nDotL = max(dot(normalize(N), normalize(L)), 0.0);
    float bias = u_ShadowBiasConst + u_ShadowSlopeScale * (1.0 - nDotL);
    float shadow = sampleShadowPCF(u_ShadowMap, projCoords.xy, projCoords.z, bias);
    return shadow;
}

// New calculateShadow that accepts N and L and dispatches accordingly
float calculateShadow(vec3 worldPos, vec3 viewPos, vec3 N, vec3 L)
{
    if (u_UseShadows == 0) return 1.0;
    if (u_CascadeCount > 1) return calculateDirectionalShadowCSM(worldPos, viewPos, N, L);
    return calculateDirectionalShadow(worldPos, N, L);
}

// End include: ../Includes/Shadows.glsl

layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;

uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;
// Debug switches (0 = off). Can be set from C# with SetInt("u_DebugShowAlbedo", 1) or
// SetInt("u_DebugShowNormals", 1) to visualize the respective data.
uniform int u_DebugShowAlbedo;
uniform int u_DebugShowNormals;
// Shadow debugging uniforms (optional)
uniform vec4  u_AlbedoColor;
uniform int u_TransparencyMode; // 0 = opaque, 1 = transparent
uniform float u_NormalStrength;
uniform float u_Metallic;
uniform float u_Smoothness;
uniform uint  u_ObjectId;

// SSAO uniforms
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;
// Debug: show shadow projection / sampling when non-zero
uniform int u_DebugShowShadows;

void main(){
    // Setup material properties from uniforms and textures
    MaterialProperties material = setupMaterialProperties(
        u_AlbedoTex, u_NormalTex, vUV,
        u_AlbedoColor, u_NormalStrength,
        u_Metallic, u_Smoothness, vNormal
    );

    // Debug overrides: let caller visualize albedo or normal sampling directly
    if (u_DebugShowAlbedo != 0) {
        outColor = vec4(material.baseColor, 1.0);
        outId = u_ObjectId;
        return;
    }

    if (u_DebugShowNormals != 0) {
        // visualize normal in 0..1 range
        vec3 nvis = normalize(material.normal) * 0.5 + 0.5;
        outColor = vec4(nvis, 1.0);
        outId = u_ObjectId;
        return;
    }

    // Calculate lighting
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 N = material.normal;

    // Accumulate lighting contributions
    vec3 Lo = vec3(0.0);

    // Directional light
    // Directional light with simple shadow mapping
    vec3 dirLighting = calculateDirectionalLight(N, V, material);
    
    // Calculate shadow factor with CSM support (viewPos = worldPos - cameraPos for distance calc)
    vec3 viewPos = vWorldPos - uCameraPos;
    // Compute light vector for bias calculation
    vec3 L = normalize(-uDirLightDirection);
    float shadowFactor = calculateShadow(vWorldPos, viewPos, N, L);

    // Note: Shadow debug visualization blends later so normal shading remains visible.
    
    Lo += dirLighting * shadowFactor;

    // Point lights
    Lo += calculatePointLights(vWorldPos, N, V, material);

    // Spot lights
    Lo += calculateSpotLights(vWorldPos, N, V, material);

    // Ambient lighting with SSAO (only for opaque materials)
    vec3 ambient;
    if (u_TransparencyMode == 0) {
        // Opaque materials: apply SSAO
        ambient = calculateAmbientLightingWithSSAO(material, gl_FragCoord.xy, u_ScreenSize,
                                                   u_SSAOTexture, u_SSAOEnabled, u_SSAOStrength);
    } else {
        // Transparent materials: no SSAO
        ambient = calculateAmbientLighting(material);
    }



    // DEBUG: Show SSAO texture as grayscale for testing (DISABLED)
    // if (u_SSAOEnabled != 0 && u_TransparencyMode == 0) {
    //     vec2 ssaoUV = gl_FragCoord.xy / u_ScreenSize;
    //     float ssaoValue = texture(u_SSAOTexture, ssaoUV).r;
    //     // Show SSAO texture directly as grayscale for debugging
    //     outColor = vec4(ssaoValue, ssaoValue, ssaoValue, 1.0);
    //     outId = u_ObjectId;
    //     return;
    // }

    vec3 color = ambient + Lo;

    // Apply fog
    color = processFog(color, vWorldPos);

    // Shadows now working correctly - no debug visualization needed!

    // Handle transparency
    float outAlpha = 1.0;
    if (u_TransparencyMode != 0) {
        // If an albedo texture is present, use its alpha channel multiplied by the albedo color alpha.
        float texAlpha = texture(u_AlbedoTex, vUV).a;
        outAlpha = saturate(texAlpha * u_AlbedoColor.a);
    }

    // If shadow debug visualization is enabled, blend a diagnostic color on top
    // of the lit result so the scene is still visible while we inspect sampling.
    if (u_DebugShowShadows != 0) {
        vec4 lightSpacePos = u_ShadowMatrix * vec4(vWorldPos, 1.0);
        vec3 lp = lightSpacePos.xyz / lightSpacePos.w;
        vec3 projCoords = lp * 0.5 + 0.5; // NDC -> [0,1]
        // For sampler2DShadow, we must provide vec3(uv, compareDepth); the texture() call returns comparison result (0 or 1).
        // Since we want the stored depth value for debug vis, we read .r from a manual textureLod or use a texelFetch.
        // However, sampler2DShadow doesn't support .r access. As a workaround for debug vis, just use projCoords.z as proxy.
        float sampledDepth = projCoords.z; // Approximate visualization (cannot read raw depth from sampler2DShadow)

        bool inside = projCoords.x >= 0.0 && projCoords.x <= 1.0 &&
                      projCoords.y >= 0.0 && projCoords.y <= 1.0 &&
                      projCoords.z >= 0.0 && projCoords.z <= 1.0;

        vec3 debugRgb;
        if (inside) {
            debugRgb = vec3(sampledDepth, projCoords.z, 1.0 - projCoords.z);
        } else {
            debugRgb = vec3(0.20, 0.20, 0.20);
        }

        // Only blend when inside the shadow map projection and use a subtle tint
        // so the underlying shading remains visible. Adjust dbgFactor for
        // stronger/weaker visualization.
        float dbgFactor = 0.12; // subtle by default
        if (inside) {
            color = mix(color, debugRgb, dbgFactor);
        }
    }

    outColor = vec4(color, outAlpha);
    outId    = u_ObjectId;
}
