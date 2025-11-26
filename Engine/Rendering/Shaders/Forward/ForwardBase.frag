#version 420 core

#include "../Includes/Common.glsl"
#include "../Includes/IBL.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"
#include "../Includes/Shadows.glsl"

layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;

// === BASE TEXTURES ===
uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;

// === PBR TEXTURES ===
uniform sampler2D u_EmissiveTex;
uniform sampler2D u_MetallicTex;        // Metallic map (R channel)
uniform sampler2D u_RoughnessTex;       // Roughness map (R channel)
uniform sampler2D u_MetallicRoughnessTex; // GLTF 2.0 combined (G=roughness, B=metallic)
uniform sampler2D u_OcclusionTex;       // Ambient occlusion (R channel)
uniform sampler2D u_HeightTex;          // Height/Parallax (R channel)

// === DETAIL TEXTURES ===
uniform sampler2D u_DetailMaskTex;      // Detail mask (R channel)
uniform sampler2D u_DetailAlbedoTex;    // Detail albedo (RGB)
uniform sampler2D u_DetailNormalTex;    // Detail normal (RGB)

// Debug switches (0 = off). Can be set from C# with SetInt("u_DebugShowAlbedo", 1) or
// SetInt("u_DebugShowNormals", 1) to visualize the respective data.
uniform int u_DebugShowAlbedo;
uniform int u_DebugShowNormals;
uniform int u_DebugShowAO;  // Debug: show AO texture
// Shadow debugging uniforms (optional)
uniform vec4  u_AlbedoColor;
uniform int u_TransparencyMode; // 0 = opaque, 1 = transparent
uniform float u_NormalStrength;

// === PBR PARAMETERS ===
uniform float u_Metallic;
uniform float u_Smoothness;
uniform float u_OcclusionStrength;
uniform vec3  u_EmissiveColor;
uniform float u_HeightScale;

uniform uint  u_ObjectId;

// Triplanar mapping settings
uniform int u_UseTriplanar; // 0 = off, 1 = on
uniform float u_TriplanarScale; // Scale factor for world-space UVs
uniform float u_TriplanarBlendSharpness; // Controls blend sharpness between projections

// Stylization parameters
uniform float u_Saturation;
uniform float u_Brightness;
uniform float u_Contrast;
uniform float u_Hue;
uniform float u_Emission;

// SSAO uniforms
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;
// Debug: show shadow projection / sampling when non-zero
uniform int u_DebugShowShadows;

// Stylization utility functions
vec3 adjustSaturation(vec3 color, float saturation) {
    vec3 grayscale = vec3(dot(color, vec3(0.299, 0.587, 0.114)));
    return mix(grayscale, color, saturation);
}

vec3 adjustBrightness(vec3 color, float brightness) {
    return color * brightness;
}

vec3 adjustContrast(vec3 color, float contrast) {
    return (color - 0.5) * contrast + 0.5;
}

vec3 rgb2hsv(vec3 rgb) {
    vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(rgb.bg, k.wz), vec4(rgb.gb, k.xy), step(rgb.b, rgb.g));
    vec4 q = mix(vec4(p.xyw, rgb.r), vec4(rgb.r, p.yzx), step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 hsv) {
    vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(hsv.xxx + k.xyz) * 6.0 - k.www);
    return hsv.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), hsv.y);
}

vec3 adjustHue(vec3 color, float hue) {
    // Convert RGB to HSV, adjust hue, convert back to RGB
    vec3 hsv = rgb2hsv(color);
    hsv.x = fract(hsv.x + hue * 0.5); // hue is in -1 to 1 range, convert to 0-1
    return hsv2rgb(hsv);
}

// Triplanar texture sampling with proper normal blending
struct TriplanarSample {
    vec3 albedo;
    vec3 normal;
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

    TriplanarSample result;
    result.albedo = albedo;
    result.normal = blendedNormal;
    return result;
}

// General-purpose triplanar helpers for colors, grayscale and normals
void ComputeTriplanarUVs(vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness,
                         out vec3 blendWeights, out vec2 uvX, out vec2 uvY, out vec2 uvZ)
{
    blendWeights = abs(worldNormal);
    blendWeights = pow(blendWeights, vec3(blendSharpness));
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-6);

    uvX = worldPos.yz * scale;
    uvY = worldPos.xz * scale;
    uvZ = worldPos.xy * scale;
}

vec3 SampleTriplanarColor(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    vec3 bw; vec2 ux, uy, uz;
    ComputeTriplanarUVs(worldPos, worldNormal, scale, blendSharpness, bw, ux, uy, uz);
    vec3 cX = texture(tex, ux).rgb;
    vec3 cY = texture(tex, uy).rgb;
    vec3 cZ = texture(tex, uz).rgb;
    return cX * bw.x + cY * bw.y + cZ * bw.z;
}

float SampleTriplanarGray(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    vec3 bw; vec2 ux, uy, uz;
    ComputeTriplanarUVs(worldPos, worldNormal, scale, blendSharpness, bw, ux, uy, uz);
    float vX = texture(tex, ux).r;
    float vY = texture(tex, uy).r;
    float vZ = texture(tex, uz).r;
    return vX * bw.x + vY * bw.y + vZ * bw.z;
}

vec3 SampleTriplanarNormalMap(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    // Similar to SampleTriplanar but handles normal map decoding and tangent->world for each projection
    vec3 bw; vec2 ux, uy, uz;
    ComputeTriplanarUVs(worldPos, worldNormal, scale, blendSharpness, bw, ux, uy, uz);

    vec3 nX = texture(tex, ux).xyz * 2.0 - 1.0;
    vec3 nY = texture(tex, uy).xyz * 2.0 - 1.0;
    vec3 nZ = texture(tex, uz).xyz * 2.0 - 1.0;

    nX.y = -nX.y; nY.y = -nY.y; nZ.y = -nZ.y;

    // Transform per-projection tangent-space normals to world-like space (matching SampleTriplanar)
    vec3 worldNormalX = vec3(nX.z, nX.x, nX.y);
    vec3 worldNormalY = vec3(nY.x, nY.z, nY.y);
    vec3 worldNormalZ = vec3(nZ.x, nZ.y, nZ.z);
    worldNormalX.x = -worldNormalX.x;

    vec3 blended = worldNormalX * bw.x + worldNormalY * bw.y + worldNormalZ * bw.z;
    return blended;
}

void main(){
    // Handle triplanar mapping if enabled
    vec3 baseNormal = normalize(vNormal);
    vec3 sampledAlbedo;
    vec3 sampledNormal;
    vec2 effectiveUV;

    // Detect which optional textures are present (1x1 placeholders yield size 1x1)
    bool hasMetallicRoughnessTex = textureSize(u_MetallicRoughnessTex, 0) != ivec2(1, 1);
    bool hasMetallicTex = !hasMetallicRoughnessTex && textureSize(u_MetallicTex, 0) != ivec2(1, 1);
    bool hasRoughnessTex = !hasMetallicRoughnessTex && textureSize(u_RoughnessTex, 0) != ivec2(1, 1);
    bool hasOcclusionTex = textureSize(u_OcclusionTex, 0) != ivec2(1, 1);
    bool hasEmissiveTex = textureSize(u_EmissiveTex, 0) != ivec2(1, 1);
    bool hasHeightTex = textureSize(u_HeightTex, 0) != ivec2(1, 1);
    bool hasDetailMask = textureSize(u_DetailMaskTex, 0) != ivec2(1, 1);
    bool hasDetailAlbedo = textureSize(u_DetailAlbedoTex, 0) != ivec2(1, 1);
    bool hasDetailNormal = textureSize(u_DetailNormalTex, 0) != ivec2(1, 1);

    if (u_UseTriplanar == 1)
    {
        // Use triplanar mapping - world-space projection for main maps
        sampledAlbedo = SampleTriplanarColor(u_AlbedoTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness);
        sampledNormal = SampleTriplanarNormalMap(u_NormalTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness) * u_NormalStrength;
        effectiveUV = vUV; // kept for non-triplanar fallbacks / detail blending

        // Sample PBR scalar/combined textures via triplanar when present
        // Metallic / Roughness
        if (hasMetallicRoughnessTex) {
            vec3 mr = SampleTriplanarColor(u_MetallicRoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness);
            // GLTF convention: G = roughness, B = metallic
            // Store temporarily in user-defined variables by writing to effectiveUV.x/y? We'll set later when computing material properties.
            // To avoid scope issues, we'll set globals via local variables below where needed.
        }

        // Occlusion and height and emissive / detail textures can also be sampled triplanar
        // We'll sample them lazily later when used (e.g., AO applied to ambient, emissive added at end)
    }
    else
    {
        // Standard UV mapping
        sampledAlbedo = texture(u_AlbedoTex, vUV).rgb;
        sampledNormal = sampleNormalMap(u_NormalTex, vUV, u_NormalStrength, baseNormal);
        effectiveUV = vUV;
    }

    // Create material properties manually to use triplanar-sampled albedo/normal
    MaterialProperties material;
    material.baseColor = sampledAlbedo * u_AlbedoColor.rgb;
    material.normal = (u_UseTriplanar == 1) ? normalize(baseNormal + sampledNormal * 0.1) : sampledNormal;

    // Sample metallic and roughness from textures if available (always use UV for these)
    float metallic = u_Metallic;
    float roughness = smoothnessToRoughness(u_Smoothness);
    // If triplanar is enabled, prefer triplanar sampling for scalar/combined textures
    if (textureSize(u_MetallicRoughnessTex, 0) != ivec2(1, 1)) {
        vec3 metallicRoughness = (u_UseTriplanar == 1) ? SampleTriplanarColor(u_MetallicRoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                                           : texture(u_MetallicRoughnessTex, effectiveUV).rgb;
        roughness = metallicRoughness.g;
        metallic = metallicRoughness.b;
    } else {
        if (textureSize(u_MetallicTex, 0) != ivec2(1, 1)) {
            metallic = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_MetallicTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                             : texture(u_MetallicTex, effectiveUV).r;
        }
        if (textureSize(u_RoughnessTex, 0) != ivec2(1, 1)) {
            roughness = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_RoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                               : texture(u_RoughnessTex, effectiveUV).r;
        } else {
            roughness = smoothnessToRoughness(u_Smoothness);
        }
    }

    // Optional detail albedo overlay (if provided)
    if (textureSize(u_DetailMaskTex, 0) != ivec2(1,1) && textureSize(u_DetailAlbedoTex, 0) != ivec2(1,1)) {
        float mask = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_DetailMaskTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                           : texture(u_DetailMaskTex, effectiveUV).r;
        vec3 detailCol = (u_UseTriplanar == 1) ? SampleTriplanarColor(u_DetailAlbedoTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                               : texture(u_DetailAlbedoTex, effectiveUV).rgb;
        material.baseColor = mix(material.baseColor, detailCol, clamp(mask, 0.0, 1.0));
    }

    // Optional detail normal blending
    if (textureSize(u_DetailNormalTex, 0) != ivec2(1,1)) {
        vec3 detailN = (u_UseTriplanar == 1) ? SampleTriplanarNormalMap(u_DetailNormalTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                             : sampleNormalMap(u_DetailNormalTex, effectiveUV, 1.0, baseNormal);
        // Add subtle detail normal contribution
        material.normal = normalize(material.normal + detailN * 0.5);
    }

    material.roughness = saturate(roughness);
    material.metallic = saturate(metallic);
    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);

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

    if (u_DebugShowAO != 0 && hasOcclusionTex) {
        // visualize AO texture as grayscale
        float ao = texture(u_OcclusionTex, vUV).r;
        outColor = vec4(ao, ao, ao, 1.0);
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
    float shadowFactor = calculateShadowWithNL(vWorldPos, viewPos, N, L);

    // Note: Shadow debug visualization blends later so normal shading remains visible.
    
    Lo += dirLighting * shadowFactor;

    // Point lights
    Lo += calculatePointLights(vWorldPos, N, V, material);

    // Spot lights
    Lo += calculateSpotLights(vWorldPos, N, V, material);

    // Ambient lighting with SSAO and AO texture
    vec3 ambient;
    if (u_TransparencyMode == 0) {
        // Opaque materials: apply SSAO
        ambient = calculateAmbientLightingWithSSAO(material, vWorldPos, gl_FragCoord.xy, u_ScreenSize,
                                                   u_SSAOTexture, u_SSAOEnabled, u_SSAOStrength);

        // Apply baked ambient occlusion texture (only if a real texture is bound, not placeholder)
        if (hasOcclusionTex) {
            float ao = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_OcclusionTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                              : texture(u_OcclusionTex, vUV).r;
            ambient *= mix(1.0, ao, u_OcclusionStrength);
        }
    } else {
        // Transparent materials: no SSAO
        ambient = calculateAmbientLighting(material, vWorldPos);
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

    // Apply stylization effects
    color = adjustSaturation(color, u_Saturation);
    color = adjustBrightness(color, u_Brightness);
    color = adjustContrast(color, u_Contrast);
    color = adjustHue(color, u_Hue);

    // Add emissive (texture-based with color tint + emission strength)
    vec3 emissiveTex = vec3(0.0);
    if (textureSize(u_EmissiveTex, 0) != ivec2(1,1)) {
        emissiveTex = (u_UseTriplanar == 1) ? SampleTriplanarColor(u_EmissiveTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                            : texture(u_EmissiveTex, vUV).rgb;
    }
    vec3 emissive = emissiveTex * u_EmissiveColor * u_Emission;
    color += emissive;

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
