#version 330 core
#include "../Includes/Common.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Shadows.glsl"
#include "../Includes/Fog.glsl"

// Inputs from vertex shader
in vec3 v_WorldPos;
in vec3 v_ViewPos;
in vec3 v_Normal;
in vec2 v_UV;
in vec4 v_SplatWeights;
flat in uvec4 v_SplatIndices;
in float v_Height;

// Terrain layer texture samplers (support up to 8 layers)
uniform sampler2D u_LayerAlbedo[8];
uniform sampler2D u_LayerNormal[8];
uniform int u_HasLayer[8];

// Layer material properties
uniform vec2 u_LayerTiling[8];
uniform vec2 u_LayerOffset[8];
uniform float u_LayerMetallic[8];
uniform float u_LayerSmoothness[8];

// Terrain-specific uniforms
uniform float u_SeaLevel;
uniform vec4 u_TerrainBaseColor;
uniform float u_TerrainMetallic;
uniform float u_TerrainRoughness;

// Height-based layer blending parameters
uniform float u_HeightBlendPower;

// SSAO uniforms (for ambient occlusion support)
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;

out vec4 FragColor;

// Calculate slope angle in degrees
float calculateSlopeAngle(vec3 normal) {
    return degrees(acos(clamp(normal.y, 0.0, 1.0)));
}

// Sample a terrain layer with material properties
MaterialProperties sampleTerrainLayer(int layerIndex, vec2 uv, vec3 normal) {
    MaterialProperties material;

    if (layerIndex >= 8 || u_HasLayer[layerIndex] == 0) {
        // Fallback to terrain base material
        material.baseColor = u_TerrainBaseColor.rgb;
        material.metallic = u_TerrainMetallic;
        material.roughness = u_TerrainRoughness;
        material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);
        material.normal = normal;
        return material;
    }

    vec2 tiledUV = uv * u_LayerTiling[layerIndex] + u_LayerOffset[layerIndex];

    // Use shared helper which applies normal map sampling (with Y flip) and smoothness->roughness conversion
    material = setupMaterialProperties(u_LayerAlbedo[layerIndex], u_LayerNormal[layerIndex], tiledUV,
                                       vec4(1.0), 1.0, u_LayerMetallic[layerIndex], u_LayerSmoothness[layerIndex], normal);

    return material;
}

void main()
{
    // Clipping plane for water reflections
    // CRITICAL: Do this BEFORE normalizing to ensure accurate world position
    if (uClipPlaneEnabled == 1) {
        // Calculate distance from fragment to clip plane
        // Plane equation: dot(normal, point) + d = 0
        // For water: we clip everything below the water level (y < waterLevel)
        float distance = dot(uClipPlane.xyz, v_WorldPos) + uClipPlane.w;

        if (distance < 0.0) {
            discard; // Fragment is below water surface, don't render
        }
    }

    vec3 normal = normalize(v_Normal);
    vec3 viewDir = normalize(uCameraPos - v_WorldPos);
    float slopeAngle = calculateSlopeAngle(normal);

    // Initialize final material properties
    MaterialProperties finalMaterial;
    finalMaterial.baseColor = u_TerrainBaseColor.rgb;
    finalMaterial.metallic = u_TerrainMetallic;
    finalMaterial.roughness = 1.0 - u_TerrainRoughness;
    finalMaterial.F0 = mix(vec3(0.04), finalMaterial.baseColor, finalMaterial.metallic);
    finalMaterial.normal = normal;

    // Blend terrain layers based on splat weights
    float totalWeight = 0.0;

    // Debug: If no splatmap data or all weights are zero, use a fallback test pattern
    bool hasSplatData = (v_SplatWeights.x + v_SplatWeights.y + v_SplatWeights.z + v_SplatWeights.w) > 0.001;

    for (int i = 0; i < 4; i++) {
        float weight = v_SplatWeights[i];
        if (weight > 0.001) {
            uint layerIndex = v_SplatIndices[i];
            if (layerIndex < 8u) {
                MaterialProperties layerMaterial = sampleTerrainLayer(int(layerIndex), v_UV, normal);

                // Blend material properties
                if (totalWeight == 0.0) {
                    finalMaterial = layerMaterial;
                } else {
                    float normalizedWeight = weight / (totalWeight + weight);
                    finalMaterial.baseColor = mix(finalMaterial.baseColor, layerMaterial.baseColor, normalizedWeight);
                    finalMaterial.metallic = mix(finalMaterial.metallic, layerMaterial.metallic, normalizedWeight);
                    finalMaterial.roughness = mix(finalMaterial.roughness, layerMaterial.roughness, normalizedWeight);
                    finalMaterial.normal = normalize(mix(finalMaterial.normal, layerMaterial.normal, normalizedWeight));
                    finalMaterial.F0 = mix(finalMaterial.F0, layerMaterial.F0, normalizedWeight);
                }

                totalWeight += weight;
            }
        }
    }

    // Ensure we have a valid material - if no splat data, try to use first available layer
    if (totalWeight < 0.001) {
        // Check if we have any layers available
        bool foundLayer = false;
        for (int i = 0; i < 8; i++) {
            if (u_HasLayer[i] == 1) {
                finalMaterial = sampleTerrainLayer(i, v_UV, normal);
                foundLayer = true;
                break;
            }
        }

        // If no layers available, use terrain base material
        if (!foundLayer) {
            finalMaterial.baseColor = u_TerrainBaseColor.rgb;
            finalMaterial.metallic = u_TerrainMetallic;
            finalMaterial.roughness = 1.0 - u_TerrainRoughness;
            finalMaterial.F0 = mix(vec3(0.04), finalMaterial.baseColor, finalMaterial.metallic);
        }
    }

    // DEBUG: Visual debugging to identify the issue
    // Debug mode: Green = has splatdata, Red = no splatdata, Blue = has layers but no splat
    if (false) {
        // Check if we have layers available
        bool hasLayers = false;
        for (int i = 0; i < 8; i++) {
            if (u_HasLayer[i] == 1) {
                hasLayers = true;
                break;
            }
        }

        if (hasSplatData) {
            FragColor = vec4(0,1,0, 1.0); // Green = splatmaps working
        } else if (hasLayers) {
            FragColor = vec4(0,0,1, 1.0); // Blue = layers exist but no splatmaps
        } else {
            FragColor = vec4(1,0,0, 1.0); // Red = no layers at all
        }
        return;
    }

    // Calculate final color using PBR lighting
    vec3 finalColor = vec3(0.0);

    // Directional light contribution with shadows
    if (uDirLightIntensity > 0.0) {
        vec3 lightDir = normalize(-uDirLightDirection);

        // Calculate shadow factor (1.0 = fully lit, 0.0 = fully shadowed)
        float shadowFactor = calculateShadowWithNL(v_WorldPos, v_ViewPos, finalMaterial.normal, lightDir);

        finalColor += shadowFactor * calculatePBRLighting(finalMaterial.normal, viewDir, lightDir,
                         uDirLightColor, uDirLightIntensity, finalMaterial);
    }

    // Point lights contribution
    for (int i = 0; i < min(uPointLightCount, 4); i++) {
        vec4 lightPos = (i == 0) ? uPointLightPos0 :
                        (i == 1) ? uPointLightPos1 :
                        (i == 2) ? uPointLightPos2 : uPointLightPos3;
        vec4 lightColor = (i == 0) ? uPointLightColor0 :
                          (i == 1) ? uPointLightColor1 :
                          (i == 2) ? uPointLightColor2 : uPointLightColor3;

    vec3 lightDir = lightPos.xyz - v_WorldPos;
        float distance = length(lightDir);
        lightDir /= distance;

        float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
        vec3 lightContrib = calculatePBRLighting(finalMaterial.normal, viewDir, lightDir,
                                               lightColor.rgb, lightColor.w * attenuation, finalMaterial);
        finalColor += lightContrib;
    }

    // Add ambient lighting with SSAO support
    vec3 ambientColor = uAmbientColor * uAmbientIntensity * finalMaterial.baseColor;

    // Apply SSAO if enabled
    if (u_SSAOEnabled == 1) {
        // Calculate screen space coordinates for SSAO texture sampling
        vec2 screenCoord = gl_FragCoord.xy / u_ScreenSize;
        float ssaoFactor = texture(u_SSAOTexture, screenCoord).r;

        // Apply SSAO strength
        ssaoFactor = mix(1.0, ssaoFactor, u_SSAOStrength);
        ambientColor *= ssaoFactor;
    }

    finalColor += ambientColor;

    // Apply fog if enabled (use processFog from Fog.glsl)
    vec3 foggedColor = processFog(finalColor, v_WorldPos);

    FragColor = vec4(foggedColor, 1.0);
}