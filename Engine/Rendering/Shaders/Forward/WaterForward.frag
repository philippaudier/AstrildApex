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
in vec2 vUVLayer1;
in vec2 vUVLayer2;
in vec4 vScreenPos;
in vec4 vReflectionPos; // Reflection position from vertex shader

// === PHASE 1: Base Parameters ===
uniform vec4  u_WaterColor;          // Base color of water (shallow)
uniform vec4  u_DeepWaterColor;      // Deep water color
uniform float u_Transparency;        // Overall transparency (0 = opaque, 1 = fully transparent)
uniform uint  u_ObjectId;            // For selection/picking
uniform float u_Time;                // Game time for animation (caustics, etc.)

// === PHASE 2: Normal Maps (Two-Layer) ===
uniform sampler2D u_NormalTex;       // First normal map layer
uniform sampler2D u_NormalTex2;      // Second normal map layer (for detail)
uniform float u_NormalStrength;      // Strength of first normal map
uniform float u_NormalStrength2;     // Strength of second normal map
uniform float u_NormalBlend;         // Blend factor between two normal maps
uniform float u_NormalMapScale;      // Tiling scale for normal maps

// === PHASE 2: Depth & Refraction ===
uniform sampler2D u_DepthTex;        // Scene depth buffer
uniform sampler2D u_SceneColorTex;   // Scene color texture for refraction
uniform float u_DepthFadeDistance;   // Distance over which water fades to opaque
uniform float u_RefractionStrength;  // Strength of refraction distortion
uniform int   u_UseRefraction;       // Enable/disable refraction (0 = off, 1 = on)

// === PHASE 3: PBR & Lighting Parameters ===
uniform float u_Roughness;           // Surface roughness (0 = mirror, 1 = diffuse)
uniform float u_Metallic;            // Metallic property (usually 0 for water)
uniform float u_Fresnel;             // Fresnel strength (reflection at grazing angles)
uniform float u_SpecularStrength;    // Strength of specular highlights

// === PHASE 4: Color Absorption (depth-based tinting) ===
uniform vec3  u_AbsorptionColor;     // Color that gets absorbed with depth (RGB)
uniform float u_AbsorptionStrength;  // Strength of color absorption

// === PHASE 5: Foam & Edge Effects ===
uniform sampler2D u_FoamTex;         // Foam texture
uniform float u_FoamAmount;          // Amount of foam at edges
uniform float u_FoamCutoff;          // Depth threshold for foam appearance
uniform vec4  u_FoamColor;           // Color of foam
uniform float u_FoamTextureScale;    // Tiling scale for foam texture
uniform float u_FoamAlphaClipThreshold; // Alpha clipping threshold for foam texture
uniform float u_EdgeFadeDistance;    // Distance for edge blending

// === PHASE 6: AAA Features ===
// Caustics
uniform int   u_UseCaustics;         // Enable/disable caustics (0 = off, 1 = on)
uniform float u_CausticsStrength;    // Intensity of caustics
uniform float u_CausticsScale;       // Tiling scale for caustics pattern
uniform float u_CausticsSpeed;       // Animation speed for caustics
uniform vec3  u_CausticsColor;       // Caustics tint color (RGB)
uniform float u_CausticsSplit;       // Chromatic aberration strength (RGB split)
uniform float u_CausticsDistortion;  // How much water normals affect caustics

// Planar Reflections
uniform int   u_UsePlanarReflections; // Enable/disable planar reflections (0 = off, 1 = on)
uniform float u_ReflectionBlur;       // Blur amount for reflections (0-1)
uniform sampler2D u_PlanarReflectionTex; // Planar reflection texture
uniform mat4  u_ReflectionViewProj;   // Reflection camera view-projection matrix
// Runtime flip controls (set from C# via shader uniform)
uniform int u_FlipReflectionX; // 0 = no flip, 1 = flip X (horizontal)
uniform int u_FlipReflectionY; // 0 = no flip, 1 = flip Y (vertical)

// === SSAO ===
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;

/// <summary>
/// Decode normal from normal map texture (tangent space)
/// </summary>
vec3 decodeNormalMap(sampler2D normalTex, vec2 uv, float strength)
{
    vec3 normalMap = texture(normalTex, uv).rgb;
    normalMap = normalMap * 2.0 - 1.0; // Convert from [0,1] to [-1,1]
    normalMap.xy *= strength;
    normalMap.z = sqrt(max(0.0, 1.0 - dot(normalMap.xy, normalMap.xy)));
    return normalize(normalMap);
}

/// <summary>
/// Generate physically-based caustics using water surface normals
/// Simulates light refraction through animated water surface
/// </summary>
vec3 generateCausticsFromNormals(vec2 worldPos, float time, float distortionStrength, float chromaticSplit)
{
    // Sample animated water normals at this position (simulating water surface above this point)
    vec2 uv1 = worldPos * u_CausticsScale + vec2(time * u_CausticsSpeed * 0.3, time * u_CausticsSpeed * 0.2);
    vec2 uv2 = worldPos * u_CausticsScale * 1.3 - vec2(time * u_CausticsSpeed * 0.4, -time * u_CausticsSpeed * 0.3);

    // Sample normals from both layers (these represent the water surface)
    vec3 normal1 = decodeNormalMap(u_NormalTex, uv1, u_NormalStrength);
    vec3 normal2 = decodeNormalMap(u_NormalTex2, uv2, u_NormalStrength2);
    vec3 waterNormal = normalize(mix(normal1, normal2, u_NormalBlend));

    // Calculate light refraction direction based on water normal
    // Snell's law simplification: water normal tilts refract light rays
    vec2 refractOffset = waterNormal.xy * distortionStrength;

    // === CHROMATIC ABERRATION ===
    // Different wavelengths refract differently (RGB split)
    float split = chromaticSplit;
    vec2 offsetR = refractOffset * (1.0 + split);      // Red refracts less
    vec2 offsetG = refractOffset;                       // Green (middle)
    vec2 offsetB = refractOffset * (1.0 - split);      // Blue refracts more

    // Calculate caustics intensity using divergence of normal field
    // Where normals converge (divergence < 0), light concentrates = bright caustics
    // We approximate this by sampling normals at offset positions and measuring change
    float epsilon = 0.02;

    // Sample neighboring normals for R, G, B channels
    vec3 nR_x = decodeNormalMap(u_NormalTex, uv1 + vec2(epsilon, 0), u_NormalStrength);
    vec3 nR_y = decodeNormalMap(u_NormalTex, uv1 + vec2(0, epsilon), u_NormalStrength);
    vec3 nG_x = decodeNormalMap(u_NormalTex, uv2 + vec2(epsilon, 0), u_NormalStrength);
    vec3 nG_y = decodeNormalMap(u_NormalTex, uv2 + vec2(0, epsilon), u_NormalStrength);

    // Compute gradient/divergence for each channel
    float divR = length(normal1 - nR_x) + length(normal1 - nR_y);
    float divG = length(waterNormal - nG_x) + length(waterNormal - nG_y);
    float divB = length(normal2 - nG_x) + length(normal2 - nG_y);

    // Convert divergence to caustics intensity
    // High divergence = light rays converging = bright caustics
    vec3 caustics;
    caustics.r = pow(saturate(divR * 15.0), 2.5);
    caustics.g = pow(saturate(divG * 15.0), 2.5);
    caustics.b = pow(saturate(divB * 15.0), 2.5);

    // Add animated detail pattern for more visual interest
    float detail = sin(uv1.x * 10.0 + time) * sin(uv1.y * 10.0 - time * 0.7) * 0.1 + 0.9;
    caustics *= detail;

    return caustics;
}

/// <summary>
/// Transform tangent-space normal to world space
/// </summary>
vec3 tangentToWorld(vec3 tangentNormal, vec3 worldNormal, vec3 worldPos)
{
    // Create TBN matrix
    vec3 Q1 = dFdx(worldPos);
    vec3 Q2 = dFdy(worldPos);
    vec2 st1 = dFdx(vUV);
    vec2 st2 = dFdy(vUV);

    vec3 N = normalize(worldNormal);
    vec3 T = normalize(Q1 * st2.t - Q2 * st1.t);
    vec3 B = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

// Note: fresnelSchlick is provided by Lighting.glsl

/// <summary>
/// Read scene depth from depth buffer
/// </summary>
float readDepth(vec2 screenUV)
{
    return texture(u_DepthTex, screenUV).r;
}

/// <summary>
/// Calculate water depth from depth buffer
/// </summary>
float calculateWaterDepth(vec2 screenUV, float waterDepth)
{
    float sceneDepth = readDepth(screenUV);

    // Linearize depths (assuming perspective projection)
    float near = 0.1;
    float far = 1000.0;
    float linearSceneDepth = (2.0 * near * far) / (far + near - sceneDepth * (far - near));
    float linearWaterDepth = (2.0 * near * far) / (far + near - waterDepth * (far - near));

    return max(0.0, linearSceneDepth - linearWaterDepth);
}

void main()
{
    vec3 N = normalize(vNormal);
    vec3 V = normalize(uCameraPos - vWorldPos);

    // === PHASE 2: Two-Layer Normal Mapping ===
    vec3 normalLayer1 = decodeNormalMap(u_NormalTex, vUVLayer1, u_NormalStrength);
    vec3 normalLayer2 = decodeNormalMap(u_NormalTex2, vUVLayer2, u_NormalStrength2);

    // Blend two normal layers
    vec3 tangentNormal = mix(normalLayer1, normalLayer2, u_NormalBlend);

    // Transform to world space
    N = tangentToWorld(tangentNormal, N, vWorldPos);

    // === PHASE 2: Depth-based effects ===
    vec2 screenUV = (vScreenPos.xy / vScreenPos.w) * 0.5 + 0.5;
    float waterDepth = gl_FragCoord.z;

    // Read scene depth from depth buffer
    float sceneDepth = texture(u_DepthTex, screenUV).r;

    // Calculate water depth (distance from water surface to geometry below)
    // Use proper perspective depth linearization
    float near = 0.1;
    float far = 1000.0;
    float sceneWaterDepth = 0.0; // Default: no geometry below (water surface)

    // Only calculate depth if there's geometry below the water
    if (sceneDepth > waterDepth) // There's geometry behind water surface
    {
        // Linearize both depths to world-space distance from camera
        float linearSceneDepth = (2.0 * near * far) / (far + near - sceneDepth * (far - near));
        float linearWaterDepth = (2.0 * near * far) / (far + near - waterDepth * (far - near));

        // Calculate depth difference (distance through water)
        sceneWaterDepth = max(0.0, linearSceneDepth - linearWaterDepth);
    }

    // Depth fade for transparency
    float depthFade = saturate(sceneWaterDepth / u_DepthFadeDistance);

    // === PHASE 3: Fresnel Effect ===
    float NdotV = saturate(dot(N, V));
    vec3 F0 = vec3(0.02); // Default F0 for water (dielectric)
    vec3 fresnelVec = fresnelSchlick(NdotV, F0);
    float fresnel = (fresnelVec.r + fresnelVec.g + fresnelVec.b) / 3.0 * u_Fresnel;

    // === PHASE 3: IBL - Diffuse (Irradiance) ===
    vec3 irradiance = sampleIrradiance(N);

    // Calculate base color with depth-based tinting
    vec3 baseColor = mix(u_WaterColor.rgb, u_DeepWaterColor.rgb, depthFade);

    // Apply color absorption based on depth (Phase 4)
    float absorptionFactor = exp(-sceneWaterDepth * u_AbsorptionStrength);
    baseColor *= mix(u_AbsorptionColor, vec3(1.0), absorptionFactor);

    // Diffuse contribution from IBL
    float roughness = u_Roughness;
    vec3 F0vec = mix(vec3(0.04), baseColor, u_Metallic);
    vec3 kd = (vec3(1.0) - F0vec) * (1.0 - u_Metallic);
    vec3 diffuse = irradiance * baseColor * kd;

    // === PHASE 3: IBL - Specular (Prefiltered Environment Map) ===
    vec3 R = reflect(-V, N);
    vec3 prefilteredColor = samplePrefilteredEnv(R, roughness);
    vec2 brdf = integrateBRDF(NdotV, roughness);
    vec3 specular = prefilteredColor * (F0vec * brdf.x + brdf.y);

    // Enhance specular with fresnel
    specular *= (1.0 + fresnel * 2.0);

    // === PHASE 3: Direct Lighting (Sun) ===
    vec3 L = normalize(-uDirLightDirection);
    vec3 H = normalize(V + L);
    float NdotL = saturate(dot(N, L));
    float NdotH = saturate(dot(N, H));

    // Blinn-Phong specular highlight
    float specPower = mix(4.0, 2048.0, 1.0 - roughness);
    float specHighlight = pow(NdotH, specPower) * u_SpecularStrength;

    vec3 directLight = uDirLightColor * NdotL * uDirLightIntensity;
    vec3 directSpecular = uDirLightColor * specHighlight * uDirLightIntensity;

    // === Shadows ===
    float shadow = 1.0;
    if (u_UseShadows > 0)
    {
        shadow = calculateShadow(vWorldPos, N);
    }

    directLight *= shadow;
    directSpecular *= shadow;

    // === PHASE 6: Caustics (Physically-Based) ===
    vec3 caustics = vec3(0.0);
    if (u_UseCaustics > 0 && sceneWaterDepth < 100.0) // Only show caustics if there's geometry below
    {
        // Generate physically-based caustics from water surface normals
        // Uses divergence of normal field to simulate light refraction convergence
        vec2 causticsUV = vWorldPos.xz;
        vec3 causticsPattern = generateCausticsFromNormals(
            causticsUV,
            u_Time * u_CausticsSpeed,
            u_CausticsDistortion,
            u_CausticsSplit
        );

        // Modulate caustics by light direction (caustics are strongest when light hits surface)
        float causticsModulation = max(0.0, dot(N, L)) * shadow;

        // Apply caustics color, strength, and light modulation
        // Caustics are already RGB from chromatic aberration
        caustics = u_CausticsColor * causticsPattern * u_CausticsStrength * causticsModulation * uDirLightIntensity;
    }

    // === PHASE 5: Foam (at shallow edges) ===
    vec3 foam = vec3(0.0);
    if (u_FoamAmount > 0.0 && sceneWaterDepth < u_FoamCutoff)
    {
        float foamFactor = 1.0 - saturate(sceneWaterDepth / u_FoamCutoff);
        // Use animated UVs for foam texture (same as normal map layer 1)
        vec4 foamTextureSample = texture(u_FoamTex, vUVLayer1 * u_FoamTextureScale);

        // Apply alpha clipping to foam texture
        // If alpha is below threshold, discard this foam pixel (creates foam pattern)
        if (foamTextureSample.a < u_FoamAlphaClipThreshold)
        {
            // Skip foam at this pixel (creates holes in foam based on texture alpha)
            foamFactor = 0.0;
        }

        foam = u_FoamColor.rgb * foamTextureSample.rgb * foamFactor * u_FoamAmount;
    }

    // === PHASE 4: Refraction ===
    vec3 refractionColor = vec3(0.0);
    if (u_UseRefraction > 0)
    {
        // Distort screen UVs based on normal map
        vec2 refractedUV = screenUV + tangentNormal.xy * u_RefractionStrength * 0.1;
        refractedUV = clamp(refractedUV, 0.0, 1.0);

        refractionColor = texture(u_SceneColorTex, refractedUV).rgb;

        // Mix refraction with water color based on depth
        refractionColor = mix(refractionColor, baseColor, depthFade * 0.5);
    }

    // === SSAO ===
    float ssao = 1.0;
    if (u_SSAOEnabled > 0)
    {
        ssao = texture(u_SSAOTexture, screenUV).r;
        ssao = mix(1.0, ssao, u_SSAOStrength);
    }

    // === PHASE 6: Planar Reflections ===
    // Implementation based on Rastertek Tutorial (https://www.rastertek.com/gl4linuxtut30.html)
    // Standard OpenGL planar reflection approach
    vec3 reflectionColor = vec3(0.0);

    if (u_UsePlanarReflections > 0)
    {
        // Perspective divide to convert from clip space to NDC [-1, 1]
        // vReflectionPos was calculated in vertex shader for better interpolation
        vec3 reflectionNDC = vReflectionPos.xyz / vReflectionPos.w;

        // Convert NDC [-1, 1] to texture coordinates [0, 1]
        vec2 reflectionUV = reflectionNDC.xy * 0.5 + 0.5;

        // Apply flips according to runtime uniforms (allows toggling X/Y independently)
        if (u_FlipReflectionX != 0)
            reflectionUV.x = 1.0 - reflectionUV.x;
        if (u_FlipReflectionY != 0)
            reflectionUV.y = 1.0 - reflectionUV.y;

        // Apply simple distortion to reflection UVs using the water surface normals
        // This uses the existing refraction strength parameter to perturb the lookup
        // and simulate small ripples distorting the reflected image.
        // Scale down the effect so it's subtle by default.
        float distortionScale = 0.075; // tweakable constant
        reflectionUV += tangentNormal.xy * u_RefractionStrength * distortionScale;

        // Sample reflection texture with optional blur
        if (u_ReflectionBlur > 0.001)
        {
            // Simple box blur for reflection
            vec3 blurredColor = vec3(0.0);
            float blurSize = u_ReflectionBlur * 0.01; // Scale blur amount to texel size
            int samples = 0;

            // 3x3 box blur
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    vec2 offset = vec2(float(x), float(y)) * blurSize;
                    blurredColor += texture(u_PlanarReflectionTex, reflectionUV + offset).rgb;
                    samples++;
                }
            }
            reflectionColor = blurredColor / float(samples);
        }
        else
        {
            // No blur - direct sample
            reflectionColor = texture(u_PlanarReflectionTex, reflectionUV).rgb;
        }
    }

    // === Combine all lighting ===
    vec3 ambient = diffuse * ssao;
    vec3 finalColor = ambient + specular + directLight * baseColor + directSpecular;

    // Add caustics (additive blending for light effect)
    finalColor += caustics;

    // Add foam on top
    finalColor += foam;

    // Mix with refraction and reflection based on Fresnel
    if (u_UseRefraction > 0 || u_UsePlanarReflections > 0)
    {
        // Fresnel determines how much we see refraction vs reflection
        // At grazing angles (high fresnel), we see more reflection
        // At steep angles (low fresnel), we see more refraction

        // Refraction contribution (stronger when looking down into water)
        float refractionMix = (1.0 - fresnel) * u_Transparency;
        if (u_UseRefraction > 0)
        {
            finalColor = mix(finalColor, refractionColor, refractionMix);
        }

        // Reflection contribution (stronger at grazing angles)
        if (u_UsePlanarReflections > 0)
        {
            // Blend planar reflection with the existing color based on fresnel
            // At grazing angles (high fresnel), more reflection visible
            // At steep angles (low fresnel), less reflection visible
            float reflectionMix = saturate(fresnel * 0.8 + 0.2); // Always show some reflection
            finalColor = mix(finalColor, reflectionColor, reflectionMix);
        }
    }

    // === Fog ===
    finalColor = processFog(finalColor, vWorldPos);

    // === Output ===
    // Calculate final alpha based on transparency and depth
    float alpha = mix(u_Transparency, 1.0, depthFade);
    alpha = saturate(alpha + fresnel); // Fresnel increases opacity at edges

    outColor = vec4(finalColor, alpha);
    outId = u_ObjectId;
}
