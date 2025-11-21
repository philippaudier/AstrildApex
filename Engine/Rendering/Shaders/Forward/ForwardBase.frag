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

void main(){
    // Setup material properties with PBR texture support
    // Check if we have valid PBR textures (non-white 1x1 placeholder)
    // Note: This is a simplified check - in production you'd pass flags from C++
    bool hasMetallicRoughnessTex = textureSize(u_MetallicRoughnessTex, 0) != ivec2(1, 1);
    bool hasMetallicTex = !hasMetallicRoughnessTex && textureSize(u_MetallicTex, 0) != ivec2(1, 1);
    bool hasRoughnessTex = !hasMetallicRoughnessTex && textureSize(u_RoughnessTex, 0) != ivec2(1, 1);
    bool hasOcclusionTex = textureSize(u_OcclusionTex, 0) != ivec2(1, 1);
    
    MaterialProperties material = setupMaterialPropertiesPBR(
        u_AlbedoTex, u_NormalTex, vUV,
        u_AlbedoColor, u_NormalStrength,
        u_Metallic, u_Smoothness, vNormal,
        u_MetallicTex, u_RoughnessTex, u_MetallicRoughnessTex,
        hasMetallicTex, hasRoughnessTex, hasMetallicRoughnessTex
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
            float ao = texture(u_OcclusionTex, vUV).r;
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
    vec3 emissive = texture(u_EmissiveTex, vUV).rgb * u_EmissiveColor * u_Emission;
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
