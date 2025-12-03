#version 420 core

#include "../Includes/Common.glsl"
#include "../Includes/IBL.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"

layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;
in vec4 vScreenPos;

// Glass-specific parameters
uniform float u_RefractiveIndex;      // 1.0 = air, 1.33 = water, 1.5 = glass, 1.9 = diamond
uniform float u_DistortionStrength;   // 0.0 = no distortion, 1.0 = full physical refraction
uniform float u_Roughness;            // 0.0 = smooth glass, 1.0 = frosted glass
uniform float u_Thickness;            // Glass thickness (affects absorption)
uniform vec3  u_Tint;                 // Glass color tint (absorption color)
uniform float u_Opacity;              // 0.0 = fully transparent, 1.0 = opaque
uniform float u_ChromaticAberration; // 0.0 = none, 1.0 = full RGB split

// Fresnel parameters
uniform float u_FresnelPower;         // Controls Fresnel falloff (default: 5.0)
uniform float u_ReflectionStrength;   // Base reflection intensity

// Textures
uniform sampler2D u_NormalTex;
uniform sampler2D u_RoughnessTex;
uniform float u_NormalStrength;

uniform uint u_ObjectId;

// Calculate Fresnel reflection (Schlick's approximation)
float FresnelSchlick(float cosTheta, float F0) {
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, u_FresnelPower);
}

// Sample normal map
vec3 sampleNormalMapGlass(vec2 uv, vec3 worldNormal) {
    if (textureSize(u_NormalTex, 0) == ivec2(1, 1)) {
        return worldNormal; // No normal map
    }

    vec3 tangentNormal = texture(u_NormalTex, uv).xyz * 2.0 - 1.0;
    tangentNormal.xy *= u_NormalStrength;

    // Simple tangent-space to world-space (approximation)
    vec3 N = normalize(worldNormal);
    vec3 T = normalize(cross(N, vec3(0.0, 1.0, 0.0)));
    vec3 B = cross(N, T);
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

void main() {
    // Sample normal map
    vec3 N = sampleNormalMapGlass(vUV, normalize(vNormal));
    vec3 V = normalize(uCameraPos - vWorldPos);

    // Sample roughness
    float roughness = u_Roughness;
    if (textureSize(u_RoughnessTex, 0) != ivec2(1, 1)) {
        roughness *= texture(u_RoughnessTex, vUV).r;
    }

    // Calculate Fresnel (view angle effect)
    float NdotV = max(dot(N, V), 0.0);
    float F0 = 0.04; // Default for glass
    float fresnel = FresnelSchlick(NdotV, F0) * u_ReflectionStrength;

    // === REFRACTION ===
    // Calculate refraction vector using Snell's law
    float eta = 1.0 / u_RefractiveIndex; // Ratio of refractive indices (air to glass)
    vec3 refractDir = refract(-V, N, eta);

    // If total internal reflection occurs, fall back to reflection
    if (length(refractDir) < 0.001) {
        refractDir = reflect(-V, N);
    }

    // Apply distortion by perturbing the refraction direction with the normal
    vec3 perturbedRefract = normalize(mix(refractDir, N, u_DistortionStrength * 0.3));

    // Sample refracted color from environment map (IBL)
    vec3 refractedColor = vec3(0.0);

    if (u_ChromaticAberration > 0.001) {
        // Chromatic aberration: simulate dispersion by sampling at slightly different angles
        float dispersion = u_ChromaticAberration * 0.05;

        // Red light bends less (higher angle)
        vec3 refractR = normalize(mix(perturbedRefract, N, dispersion));
        float r = samplePrefilteredEnv(refractR, roughness * 0.5).r;

        // Green light (no offset)
        float g = samplePrefilteredEnv(perturbedRefract, roughness * 0.5).g;

        // Blue light bends more (lower angle)
        vec3 refractB = normalize(mix(perturbedRefract, -N, dispersion));
        float b = samplePrefilteredEnv(refractB, roughness * 0.5).b;

        refractedColor = vec3(r, g, b);
    } else {
        // No chromatic aberration - sample environment at refracted angle
        refractedColor = samplePrefilteredEnv(perturbedRefract, roughness * 0.5);
    }

    // Apply glass tint (absorption based on thickness)
    // Darker tint = more absorption, uses Beer's law approximation
    float absorption = exp(-u_Thickness * 2.0);
    vec3 tintedColor = refractedColor * mix(u_Tint, vec3(1.0), absorption);
    refractedColor = tintedColor;

    // === REFLECTION ===
    // Sample environment reflections (IBL) using prefiltered environment map
    vec3 R = reflect(-V, N);
    vec3 reflectionColor = samplePrefilteredEnv(R, roughness);

    // Blend refraction and reflection based on Fresnel
    vec3 finalColor = mix(refractedColor, reflectionColor, fresnel);

    // Apply fog
    finalColor = processFog(finalColor, vWorldPos);

    // Calculate alpha based on opacity and Fresnel
    // Glass is more opaque at grazing angles (Fresnel effect)
    float alpha = mix(u_Opacity, 1.0, fresnel);

    outColor = vec4(finalColor, alpha);
    outId = u_ObjectId;
}
