#version 420 core

#include "../Includes/Common.glsl"

layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

// Optional override uniforms for clipping (for debugging)
uniform int u_ClipPlaneEnabled; // Override if present, otherwise use UBO
uniform vec4 u_ClipPlane; // Override if present, otherwise use UBO

// Snow displacement parameters
uniform float u_SnowAccumulation;  // Accumulated snow (can exceed 1.0)
uniform float u_SnowDisplacement;  // Max displacement height
uniform float u_SnowSlopeMin;      // Min slope for snow placement (degrees)
uniform float u_SnowSlopeMax;      // Max slope for snow placement (degrees)
uniform float u_DisableSnowDisplacement; // Set to 1.0 to disable vertex displacement (for FPS arms, etc.)

out vec3 vWorldPos;
out vec3 vNormal;
out vec3 vObjectNormal;
out vec2 vUV;

// Calculate snow placement based on surface angle
// Uses OBJECT SPACE normal to ensure snow stays on geometry regardless of model orientation
float calculateSnowPlacement(vec3 objectNormal, float slopeMinDeg, float slopeMaxDeg)
{
    // In object space, "up" is still (0,1,0) relative to the model's local coordinates
    vec3 up = vec3(0, 1, 0);
    float dotProduct = dot(normalize(objectNormal), up);
    float angleRad = acos(clamp(dotProduct, -1.0, 1.0));
    float angleDeg = angleRad * 57.29577951; // radians to degrees

    float fadeWidth = 5.0;
    float fadeIn = smoothstep(slopeMinDeg, slopeMinDeg + fadeWidth, angleDeg);
    float fadeOut = 1.0 - smoothstep(slopeMaxDeg - fadeWidth, slopeMaxDeg, angleDeg);

    return clamp(fadeIn * fadeOut, 0.0, 1.0);
}

void main(){
    vec4 wp = u_Model * vec4(aPos,1.0);
    vec3 worldNormal = normalize(u_NormalMat * aNormal);

    // Calculate snow displacement using OBJECT SPACE normal (not world space)
    // This ensures snow placement is relative to the model's geometry, not its orientation
    // Example: Arms retain snow on top even when rotated
    vec3 objectNormal = normalize(aNormal);
    float snowPlacement = calculateSnowPlacement(objectNormal, u_SnowSlopeMin, u_SnowSlopeMax);
    float snowAmount = u_SnowAccumulation * snowPlacement;

    // Vertical displacement based on snow accumulation (only if displacement is enabled)
    // Uses exponential curve for natural build-up (more snow = more height)
    float displacementAmount = 0.0;
    if (u_DisableSnowDisplacement < 0.5) // Only displace if not disabled
    {
        displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * u_SnowDisplacement;
        
        // Displace vertex upward along world Y-axis
        wp.y += displacementAmount;
    }

    // Smooth normals for rounded snow edges (only if displacement is active)
    vec3 smoothedNormal = worldNormal;
    if (u_DisableSnowDisplacement < 0.5 && displacementAmount > 0.01)
    {
        // Blend normal towards up vector based on snow amount
        float normalSmoothFactor = clamp(snowAmount * 0.3, 0.0, 0.7);
        smoothedNormal = normalize(mix(worldNormal, vec3(0, 1, 0), normalSmoothFactor));
    }

    vWorldPos = wp.xyz;
    vNormal   = smoothedNormal;
    vObjectNormal = objectNormal;
    vUV = aUV * u_TextureTiling + u_TextureOffset;
    gl_Position = uViewProj * wp;

    // Clipping plane support (for planar reflections)
    // Try override uniform first, fallback to UBO
    float clipEnabled = float(u_ClipPlaneEnabled);  // Will be 0 if not set
    vec4 clipPlane = u_ClipPlane;  // Will be (0,0,0,0) if not set
    
    // If override not set, use UBO values
    if (clipEnabled < 0.5 && uClipPlaneEnabled > 0.5) {
        clipEnabled = uClipPlaneEnabled;
        clipPlane = uClipPlane;
    }
    
    if (clipEnabled > 0.5) {
        gl_ClipDistance[0] = dot(wp, clipPlane);
    } else {
        gl_ClipDistance[0] = 1.0; // Always pass if clipping disabled
    }
}
