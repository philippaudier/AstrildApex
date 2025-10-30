#version 420 core

#include "../Includes/Common.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"
#include "../Includes/Shadows.glsl"

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
    float shadowFactor = calculateShadowWithNL(vWorldPos, viewPos, N, L);

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
