#version 330 core
layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;
in vec3 vViewDirTangent;

// ============================================================================
// Simple Shadow Mapping - Inlined for compatibility
// ============================================================================

// Shadow uniforms
uniform sampler2DShadow u_ShadowMap;
uniform mat4 u_ShadowMatrix;
uniform int u_UseShadows;
uniform float u_ShadowMapSize;
uniform float u_ShadowBias;
uniform float u_ShadowStrength;
uniform float u_ShadowDistance;

// Simple PCF with 3x3 kernel (9 samples)
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

// Calculate shadow factor for a world-space position
// PERFORMANCE: Uses cameraPos parameter for distance-based early-out
float CalculateShadow(vec3 worldPos, vec3 normal, vec3 lightDir, vec3 cameraPos)
{
    if (u_UseShadows == 0)
        return 1.0;

    // PERFORMANCE: Early-out based on shadow distance (avoids expensive PCF for distant fragments)
    if (u_ShadowDistance > 0.0)
    {
        float distToCamera = length(worldPos - cameraPos);
        if (distToCamera > u_ShadowDistance)
            return 1.0;
    }

    // Apply normal-based bias in world space
    float normalDot = max(dot(normal, lightDir), 0.0);
    float slopeBias = u_ShadowBias * sqrt(1.0 - normalDot * normalDot);
    vec3 biasedWorldPos = worldPos + normal * (u_ShadowBias + slopeBias);

    // Transform to light space
    vec4 lightSpacePos = u_ShadowMatrix * vec4(biasedWorldPos, 1.0);
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;
    projCoords = projCoords * 0.5 + 0.5;

    // Check bounds - early-out before expensive PCF sampling
    if (projCoords.x < 0.0 || projCoords.x > 1.0 ||
        projCoords.y < 0.0 || projCoords.y > 1.0 ||
        projCoords.z < 0.0 || projCoords.z > 1.0)
    {
        return 1.0;
    }

    // Perform PCF shadow sampling
    float shadowValue = PCF_Simple(projCoords.xy, projCoords.z);

    // Apply shadow strength: mix between (1.0 - strength) and 1.0
    return mix(1.0 - u_ShadowStrength, 1.0, shadowValue);
}

// ============================================================================

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

    int uClipPlaneEnabled; float _pad17; float _pad18; float _pad19;
    vec4 uClipPlane; // plane equation: normal.xyz, d
};

uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;
uniform sampler2D u_HeightTex;
uniform sampler2D u_OcclusionTex;
// Debug switches (0 = off). Can be set from C# with SetInt("u_DebugShowAlbedo", 1) or
// SetInt("u_DebugShowNormals", 1) to visualize the respective data.
uniform int u_DebugShowAlbedo;
uniform int u_DebugShowNormals;
uniform vec4  u_AlbedoColor;
uniform int u_TransparencyMode; // 0 = opaque, 1 = transparent
uniform float u_NormalStrength;
uniform float u_Metallic;
uniform float u_Smoothness;
uniform float u_HeightScale;
uniform float u_OcclusionStrength;
uniform uint  u_ObjectId;

// Triplanar mapping settings
uniform int u_UseTriplanar; // 0 = off, 1 = on
uniform float u_TriplanarScale; // Scale factor for world-space UVs
uniform float u_TriplanarBlendSharpness; // Controls blend sharpness between projections

// SSAO uniforms
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;

#include "Includes/IBL.glsl"

// Parallax Occlusion Mapping
vec2 ParallaxOcclusionMapping(vec2 texCoords, vec3 viewDir, float heightScale)
{
    // If height scale is zero or very small, skip parallax mapping
    if (heightScale < 0.001) return texCoords;

    // Number of depth layers - reduced for better performance
    // Original: 8-32 layers (too expensive)
    // Optimized: 4-8 layers (good quality/performance balance)
    const float minLayers = 4.0;
    const float maxLayers = 8.0;
    float numLayers = mix(maxLayers, minLayers, abs(dot(vec3(0.0, 0.0, 1.0), viewDir)));

    // Calculate the size of each layer
    float layerDepth = 1.0 / numLayers;
    float currentLayerDepth = 0.0;

    // The amount to shift the texture coordinates per layer (from vector P)
    vec2 P = viewDir.xy * heightScale;
    vec2 deltaTexCoords = P / numLayers;

    // Get initial values
    vec2 currentTexCoords = texCoords;
    float currentDepthMapValue = texture(u_HeightTex, currentTexCoords).r;

    // Iterate until we find the first point below the surface
    while(currentLayerDepth < currentDepthMapValue)
    {
        // Shift texture coordinates along direction of P
        currentTexCoords -= deltaTexCoords;
        // Get depthmap value at current texture coordinates
        currentDepthMapValue = texture(u_HeightTex, currentTexCoords).r;
        // Get depth of next layer
        currentLayerDepth += layerDepth;
    }

    // Get texture coordinates before collision (reverse operations)
    vec2 prevTexCoords = currentTexCoords + deltaTexCoords;

    // Get depth after and before collision for linear interpolation
    float afterDepth  = currentDepthMapValue - currentLayerDepth;
    float beforeDepth = texture(u_HeightTex, prevTexCoords).r - currentLayerDepth + layerDepth;

    // Interpolation of texture coordinates
    float weight = afterDepth / (afterDepth - beforeDepth);
    vec2 finalTexCoords = prevTexCoords * weight + currentTexCoords * (1.0 - weight);

    return finalTexCoords;
}

// Triplanar texture sampling with proper normal blending
struct TriplanarSample {
    vec3 albedo;
    vec3 normal;
    float occlusion;
};

TriplanarSample SampleTriplanar(vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    // Calculate blend weights based on surface normal
    vec3 blendWeights = abs(worldNormal);

    // Apply sharpness to blend (higher = sharper transitions)
    blendWeights = pow(blendWeights, vec3(blendSharpness));

    // Normalize blend weights so they sum to 1
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

    // Calculate UVs for each projection plane
    vec2 uvX = worldPos.yz * scale; // Side projection (X-axis)
    vec2 uvY = worldPos.xz * scale; // Top/Bottom projection (Y-axis)
    vec2 uvZ = worldPos.xy * scale; // Front/Back projection (Z-axis)

    // Sample albedo from all three projections
    vec3 albedoX = texture(u_AlbedoTex, uvX).rgb;
    vec3 albedoY = texture(u_AlbedoTex, uvY).rgb;
    vec3 albedoZ = texture(u_AlbedoTex, uvZ).rgb;

    // Blend albedo
    vec3 albedo = albedoX * blendWeights.x +
                  albedoY * blendWeights.y +
                  albedoZ * blendWeights.z;

    // Sample normal maps from all three projections
    vec3 normalX = texture(u_NormalTex, uvX).xyz * 2.0 - 1.0;
    vec3 normalY = texture(u_NormalTex, uvY).xyz * 2.0 - 1.0;
    vec3 normalZ = texture(u_NormalTex, uvZ).xyz * 2.0 - 1.0;

    // Flip Y channel for OpenGL normal maps
    normalX.y = -normalX.y;
    normalY.y = -normalY.y;
    normalZ.y = -normalZ.y;

    // Transform tangent-space normals to world space for each projection
    // X-axis projection (YZ plane): tangent=Y, bitangent=Z, normal=X
    vec3 worldNormalX = vec3(normalX.z, normalX.x, normalX.y);
    // Y-axis projection (XZ plane): tangent=X, bitangent=Z, normal=Y
    vec3 worldNormalY = vec3(normalY.x, normalY.z, normalY.y);
    // Z-axis projection (XY plane): tangent=X, bitangent=Y, normal=Z
    vec3 worldNormalZ = vec3(normalZ.x, normalZ.y, normalZ.z);

    // Flip X-projection to match world-space orientation
    worldNormalX.x = -worldNormalX.x;

    // Blend world-space normals
    vec3 blendedNormal = worldNormalX * blendWeights.x +
                         worldNormalY * blendWeights.y +
                         worldNormalZ * blendWeights.z;

    // Sample occlusion from all three projections
    float occlusionX = texture(u_OcclusionTex, uvX).r;
    float occlusionY = texture(u_OcclusionTex, uvY).r;
    float occlusionZ = texture(u_OcclusionTex, uvZ).r;

    // Blend occlusion
    float occlusion = occlusionX * blendWeights.x +
                      occlusionY * blendWeights.y +
                      occlusionZ * blendWeights.z;

    TriplanarSample result;
    result.albedo = albedo;
    result.normal = blendedNormal;
    result.occlusion = occlusion;
    return result;
}

vec3 FresnelSchlick(float cosTheta, vec3 F0){ return F0 + (1.0 - F0)*pow(1.0 - cosTheta, 5.0); }


vec3 CalculatePBR(vec3 N, vec3 V, vec3 L, vec3 lightColor, float lightIntensity, vec3 baseCol, float rough, float metal, vec3 F0) {
    vec3 H = normalize(V + L);
    float NdotL = max(dot(N, L), 0.001);
    float NdotV = max(dot(N, V), 0.001);
    float NdotH = max(dot(N, H), 0.0);
    float VdotH = max(dot(V, H), 0.0);


    float a = rough * rough;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;
    float D = a2 / (3.14159 * denom * denom);


    float k = (rough + 1.0) * (rough + 1.0) / 8.0;
    float Gv = NdotV / (NdotV * (1.0 - k) + k);
    float Gl = NdotL / (NdotL * (1.0 - k) + k);
    float G = Gv * Gl;


    vec3 F = FresnelSchlick(VdotH, F0);


    vec3 spec = (D * G * F) / max(4.0 * NdotL * NdotV, 0.001);
    vec3 kd = (1.0 - F) * (1.0 - metal);
    vec3 diffuse = kd * baseCol / 3.14159;

    return (diffuse + spec) * lightColor * lightIntensity * NdotL;
}

void main(){

    // Clipping plane for water reflections
    if (uClipPlaneEnabled == 1) {
        // Calculate distance from fragment to clip plane
        // Plane equation: dot(normal, point) + d = 0
        float distance = dot(uClipPlane.xyz, vWorldPos) + uClipPlane.w;
        if (distance < 0.0) {
            discard; // Fragment is below water surface, don't render
        }
    }

    // Choose between triplanar or standard UV mapping
    vec3 baseCol;
    vec3 normalMap;
    vec3 baseNormal = normalize(vNormal);
    float occlusionValue = 1.0; // Default to no occlusion
    vec2 adjustedUV = vUV; // FIX: Declare outside scope for transparency alpha sampling

    if (u_UseTriplanar == 1)
    {
        // Use triplanar mapping - world-space projection
        TriplanarSample triSample = SampleTriplanar(vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness);
        baseCol = triSample.albedo * u_AlbedoColor.rgb;

        // Apply normal strength to triplanar normals
        normalMap = triSample.normal * u_NormalStrength;

        // Use triplanar-sampled occlusion
        occlusionValue = triSample.occlusion;
    }
    else
    {
        // Standard UV mapping with optional parallax
        if (u_HeightScale > 0.001)
        {
            adjustedUV = ParallaxOcclusionMapping(vUV, normalize(vViewDirTangent), u_HeightScale);
        }

        baseCol = texture(u_AlbedoTex, adjustedUV).rgb * u_AlbedoColor.rgb;

        normalMap = texture(u_NormalTex, adjustedUV).rgb * 2.0 - 1.0;
        normalMap.y = -normalMap.y;
        normalMap.xy *= u_NormalStrength;

        // Sample occlusion texture
        occlusionValue = texture(u_OcclusionTex, adjustedUV).r;
    }

    // Calculate final normal
    vec3 N = normalize(baseNormal + normalMap * 0.1);

    vec3 V = normalize(uCameraPos - vWorldPos);
    float smoothness = clamp(u_Smoothness, 0.0, 1.0);
    float rough = clamp(1.0 - smoothness, 0.01, 0.99);
    float metal = clamp(u_Metallic, 0.0, 1.0);
    vec3 F0 = mix(vec3(0.04), baseCol, metal);


    vec3 Lo = vec3(0.0);

    // PERFORMANCE: Calculate directional light direction once and reuse for both PBR and shadows
    vec3 dirLightDir = normalize(-uDirLightDirection);

    if (uDirLightIntensity > 0.0) {
        Lo += CalculatePBR(N, V, dirLightDir, uDirLightColor, uDirLightIntensity, baseCol, rough, metal, F0);
    }


    vec4 pointPos[4] = vec4[4](uPointLightPos0, uPointLightPos1, uPointLightPos2, uPointLightPos3);
    vec4 pointColor[4] = vec4[4](uPointLightColor0, uPointLightColor1, uPointLightColor2, uPointLightColor3);
    
    for (int i = 0; i < min(uPointLightCount, 4); i++) {
        vec3 lightPos = pointPos[i].xyz;
        float range = pointPos[i].w;
        vec3 lightColor = pointColor[i].rgb;
        float intensity = pointColor[i].a;
        
        vec3 L = lightPos - vWorldPos;
        float distance = length(L);
        L = normalize(L);
        

        float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
        if (range > 0.0) {
            float falloff = clamp(1.0 - distance / range, 0.0, 1.0);
            attenuation *= falloff * falloff;
        }
        
        Lo += CalculatePBR(N, V, L, lightColor, intensity * attenuation, baseCol, rough, metal, F0);
    }


    vec4 spotPos[2] = vec4[2](uSpotLightPos0, uSpotLightPos1);
    vec4 spotDir[2] = vec4[2](uSpotLightDir0, uSpotLightDir1);
    vec4 spotColor[2] = vec4[2](uSpotLightColor0, uSpotLightColor1);
    float spotAngle[2] = float[2](uSpotLightAngle0, uSpotLightAngle1);
    float spotInnerAngle[2] = float[2](uSpotLightInnerAngle0, uSpotLightInnerAngle1);
    
    for (int i = 0; i < min(uSpotLightCount, 2); i++) {
        vec3 lightPos = spotPos[i].xyz;
        float range = spotPos[i].w;
        vec3 lightDir = normalize(spotDir[i].xyz);
        vec3 lightColor = spotColor[i].rgb;
        float intensity = spotColor[i].a;
        float outerAngle = spotAngle[i];
        float innerAngle = spotInnerAngle[i];
        
        vec3 L = lightPos - vWorldPos;
        float distance = length(L);
        L = normalize(L);
        

        float cosOuterAngle = cos(radians(outerAngle * 0.5));
        float cosInnerAngle = cos(radians(innerAngle * 0.5));
        float spotEffect = dot(-L, lightDir);
        
        if (spotEffect > cosOuterAngle) {

            float spotAttenuation = 1.0;
            if (spotEffect < cosInnerAngle) {

                float falloffRange = cosInnerAngle - cosOuterAngle;
                float falloffFactor = (spotEffect - cosOuterAngle) / falloffRange;
                spotAttenuation = smoothstep(0.0, 1.0, falloffFactor);
            }
            
            float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
            if (range > 0.0) {
                float rangeFalloff = clamp(1.0 - distance / range, 0.0, 1.0);
                attenuation *= rangeFalloff * rangeFalloff;
            }
            
            Lo += CalculatePBR(N, V, L, lightColor, intensity * attenuation * spotAttenuation, baseCol, rough, metal, F0);
        }
    }



    // Debug overrides: let caller visualize albedo or normal sampling directly
    if (u_DebugShowAlbedo != 0) {
        outColor = vec4(baseCol, 1.0);
        outId = u_ObjectId;
        return;
    }

    if (u_DebugShowNormals != 0) {
        // visualize normal in 0..1 range
        vec3 nvis = normalize(N) * 0.5 + 0.5;
        outColor = vec4(nvis, 1.0);
        outId = u_ObjectId;
        return;
    }

    // Calculate shadow factor for directional light
    // PERFORMANCE: Reuse dirLightDir calculated earlier (avoids redundant normalize)
    float shadowFactor = 1.0;
    if (uDirLightIntensity > 0.0) {
        shadowFactor = CalculateShadow(vWorldPos, N, dirLightDir, uCameraPos);
    }

    // Apply shadows to direct lighting ONLY (not to ambient)
    // This is critical: shadows should not affect ambient/SSAO
    Lo *= shadowFactor;

    // Apply SSAO to ambient lighting only
    float ssaoFactor = 1.0;
    if (u_SSAOEnabled == 1) {
        vec2 screenCoord = gl_FragCoord.xy / u_ScreenSize;
        float ssaoValue = texture(u_SSAOTexture, screenCoord).r;
        // Mix between no SSAO (1.0) and full SSAO based on strength
        ssaoFactor = mix(1.0, ssaoValue, u_SSAOStrength);
    }

    // Apply texture-based ambient occlusion (from material)
    ssaoFactor *= mix(1.0, occlusionValue, u_OcclusionStrength);

    // Ambient is affected by SSAO and texture AO, NOT by shadows
    vec3 ambient = vec3(0.0);
    // If IBL is available, use it for both diffuse and specular ambient contribution
    // calculateIBL returns diffuse+specular contribution already factoring F0 and metallic response
    if (int(u_HasIBL) != 0) {
        ambient = calculateIBL(N, V, rough, F0, baseCol, metal) * uAmbientIntensity * ssaoFactor;
    } else {
        ambient = baseCol * uAmbientColor * uAmbientIntensity * (1.0 - metal * 0.5) * ssaoFactor;
    }

    // Combine: ambient (with SSAO) + direct lighting (with shadows)
    vec3 col = ambient + Lo;


    if (uFogEnabled != 0) {
        float dist = length(uCameraPos - vWorldPos);
        float fogFactor = clamp((uFogEnd - dist) / max(1e-5, (uFogEnd - uFogStart)), 0.0, 1.0);

        col = mix(uFogColor, col, fogFactor);
    }

    float outAlpha = 1.0;
    if (u_TransparencyMode != 0) {
        // If an albedo texture is present, use its alpha channel multiplied by the albedo color alpha.
        float texAlpha = 1.0;
        // We sample the albedo texture's alpha; if no alpha channel, this will be 1.0 from the white 1x1 default.
        texAlpha = texture(u_AlbedoTex, adjustedUV).a;
        outAlpha = clamp(texAlpha * u_AlbedoColor.a, 0.0, 1.0);
    }

    outColor = vec4(col, outAlpha);
    outId    = u_ObjectId;
}
