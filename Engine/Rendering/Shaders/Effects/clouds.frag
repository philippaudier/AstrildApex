#version 420 core

// ============================================================================
// CLOUD FRAGMENT SHADER
// Renders layered 2D procedural clouds with lighting and scattering
// ============================================================================

#include "../Includes/Common.glsl"
#include "../Includes/CloudNoise.glsl"

// Inputs from vertex shader
in vec3 vWorldDir;
in vec2 vScreenPos;
in float vVertexY;

// Output
out vec4 FragColor;

// Cloud uniforms
uniform float uCloudCoverage;      // 0-1: cloud coverage amount
uniform float uCloudDensity;       // 0-1: opacity/thickness
uniform int uCloudType;            // 0=cirrus, 1=cumulus, 2=stratus, 3=storm
uniform vec3 uCloudWindOffset;     // XYZ offset for animation (accumulated over time)
uniform float uCloudScattering;    // Sun scattering intensity
uniform vec3 uCloudSunDir;         // Direction to sun
uniform vec3 uCloudSunColor;       // Sun color
uniform float uCloudSunIntensity;  // Sun/Moon light intensity (brightness)
uniform float uCloudAmbient;       // Ambient light contribution
uniform float uCloudDetailSpeed;   // Detail animation speed for organic shape changes
uniform sampler2D uDitheringTex;   // Dithering texture for smooth gradients

// Fine-tune parameters
uniform float uCloudNoiseScale;    // Global noise scale multiplier
uniform float uCloudMorphSpeed;    // Organic morphing speed
uniform float uCloudEdgeSoftness;  // Edge softness/hardness
uniform float uCloudBillowiness;   // Cotton/billowy appearance strength
uniform float uCloudDetailStrength;// Fine detail strength

// Layer configuration (3 altitude layers for parallax)
const int NUM_LAYERS = 3;
const vec3 LAYER_ALTITUDES = vec3(10000.0, 4000.0, 1500.0); // High, mid, low (in meters)
const vec3 LAYER_SPEEDS = vec3(1.2, 0.8, 0.5);              // Parallax animation speeds
const vec3 LAYER_SCALES = vec3(0.5, 1.0, 1.5);              // Noise scales
const vec3 LAYER_WEIGHTS = vec3(0.3, 0.5, 0.7);             // Density contribution

// ============================================================================
// SPHERE UV MAPPING
// Convert direction vector to planar UV coordinates
// ============================================================================
vec2 sphereToUV(vec3 dir) {
    // Project XZ plane onto UV space with perspective division
    // The (1.0 + dir.y) term creates perspective: clouds at horizon are stretched
    float scale = 1.0 / max(0.1, 1.0 + dir.y * 0.5);
    return dir.xz * scale;
}

// ============================================================================
// HENYEY-GREENSTEIN PHASE FUNCTION (Simplified)
// Models anisotropic scattering (forward scattering bias)
// ============================================================================
float henyeyGreenstein(float cosTheta, float g) {
    float g2 = g * g;
    return (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5);
}

// Simplified scattering for performance
float fastScattering(float cosTheta, float strength) {
    // Approximate forward scattering with power function
    float scatter = pow(saturate(cosTheta), 8.0);
    return scatter * strength;
}

// ============================================================================
// CLOUD LIGHTING
// Calculate lighting contribution from sun and ambient
// Takes light intensity into account for day/night variation
// ============================================================================
vec3 calculateCloudLighting(vec3 viewDir, vec3 baseColor, float density) {
    // Sun direction dot product
    float sunDot = dot(viewDir, uCloudSunDir);

    // Forward scattering (silver lining effect)
    float scatter = fastScattering(sunDot, uCloudScattering);

    // === LIGHT INTENSITY MODULATION ===
    // Scale lighting by sun/moon intensity (much darker at night)
    float lightIntensity = uCloudSunIntensity;

    // Ambient term (sky color contribution) - reduced at night
    vec3 ambient = baseColor * uCloudAmbient * lightIntensity;

    // Sun contribution (more visible when looking towards sun) - scaled by intensity
    vec3 sunContribution = uCloudSunColor * scatter * lightIntensity;

    // Direct illumination from sun (diffuse-like term)
    // Clouds facing the sun are brighter
    float sunFacing = saturate(dot(vec3(0, 1, 0), uCloudSunDir)); // Assume clouds face up
    vec3 directLight = uCloudSunColor * sunFacing * lightIntensity;

    // Combine lighting
    vec3 finalColor = baseColor * lightIntensity * 0.3 + // Base color with light
                      directLight * 0.5 +                  // Direct illumination
                      sunContribution +                    // Scattering
                      ambient;                             // Ambient

    // Darken dense clouds (self-shadowing approximation)
    float darkening = mix(1.0, 0.7, density * 0.5);
    finalColor *= darkening;

    // Minimum brightness for night (moonlight/starlight) - very subtle
    vec3 nightMinimum = baseColor * 0.02; // 2% minimum visibility
    finalColor = max(finalColor, nightMinimum);

    return finalColor;
}

// ============================================================================
// MAIN SHADER
// ============================================================================
void main() {
    // Normalize world direction
    vec3 dir = normalize(vWorldDir);
    float dirY = dir.y;

    // Discard pixels below horizon
    if (dirY < -0.05) {
        discard;
    }

    // Base cloud color (affected by time of day)
    // uDayNightBlend comes from Common.glsl Global UBO
    vec3 daySkyColor = vec3(0.95, 0.97, 1.0);    // Bright white-blue
    vec3 nightSkyColor = vec3(0.3, 0.35, 0.45);  // Dark blue-grey
    vec3 baseColor = mix(nightSkyColor, daySkyColor, uDayNightBlend);

    // Golden hour tinting
    // uGoldenHourBlend comes from Common.glsl Global UBO
    if (uGoldenHourBlend > 0.01) {
        vec3 goldenTint = vec3(1.0, 0.7, 0.5); // Orange-pink
        baseColor = mix(baseColor, goldenTint, uGoldenHourBlend * 0.6);
    }

    // Storm clouds are darker
    if (uCloudType == 3) {
        baseColor *= 0.6; // Darken for storm
    }

    // Accumulated cloud color and alpha
    vec4 cloudColor = vec4(0.0);

    // ========== RENDER CLOUD LAYERS ==========
    for (int i = 0; i < NUM_LAYERS; i++) {
        // Skip if already fully opaque
        if (cloudColor.a >= 0.98) {
            break;
        }

        // Calculate UV with wind offset, parallax, and global noise scale
        vec2 baseUV = sphereToUV(dir) * LAYER_SCALES[i] * uCloudNoiseScale;
        vec2 windOffset = uCloudWindOffset.xy * LAYER_SPEEDS[i];
        vec2 uv = baseUV + windOffset;

        // === ORGANIC SHAPE EVOLUTION ===
        // Add time-based distortion for evolving cloud shapes
        float timeOffset = uTime * uCloudMorphSpeed * 0.1;

        // Distort UV coordinates with animated noise for organic movement
        // Strength controlled by morph speed
        float distortionStrength = 0.15 * uCloudMorphSpeed;
        vec2 distortion = vec2(
            perlinNoise2D(uv * 0.5 + vec2(timeOffset * 0.3, timeOffset * 0.2), 2.0),
            perlinNoise2D(uv * 0.5 + vec2(timeOffset * 0.2, -timeOffset * 0.3), 2.0)
        );
        distortion = (distortion - 0.5) * distortionStrength;
        vec2 animatedUV = uv + distortion;

        // Generate cloud shape from procedural noise with animated UV
        // Use uCloudDetailStrength instead of hardcoded 0.5
        float noise = cloudShape(animatedUV, uCloudType, uCloudCoverage, uCloudDetailStrength);

        // Add temporal variation to the noise itself (breathing effect)
        float breathe = perlinNoise2D(uv * 0.2, 1.0) * sin(timeOffset * 0.5) * 0.1 * uCloudMorphSpeed;
        noise = saturate(noise + breathe);

        // Coverage remapping (creates soft edges) - controlled by edge softness
        float edgeRange = mix(0.1, 0.5, uCloudEdgeSoftness); // Softness range
        float threshold = uCloudCoverage - edgeRange;
        float softness = uCloudCoverage + edgeRange * 0.3;
        float alpha = smoothstep(threshold, softness, noise);

        // Modulate alpha by layer weight
        alpha *= LAYER_WEIGHTS[i];

        // Horizon fade (clouds fade near horizon for realism)
        float horizonFade = smoothstep(-0.05, 0.2, dirY);
        alpha *= horizonFade;

        // Apply overall density control
        alpha *= uCloudDensity;

        // === VOLUMETRIC DITHERING EFFECT (DISABLED) ===
        // Disabled for now - can be re-enabled later if needed
        /*
        float ditherPattern = texture(uDitheringTex, vScreenPos * 10.0 + uv * 3.0).r;
        if (alpha < 0.3) {
            float volumetricThreshold = alpha * 0.95;
            if (ditherPattern > volumetricThreshold) {
                continue;
            }
        }
        */

        // Skip if this layer contributes nothing
        if (alpha < 0.01) {
            continue;
        }

        // Calculate lighting for this layer
        vec3 layerColor = calculateCloudLighting(dir, baseColor, alpha);

        // Accumulate layer using over operator (back-to-front blending)
        cloudColor.rgb += layerColor * alpha * (1.0 - cloudColor.a);
        cloudColor.a += alpha * (1.0 - cloudColor.a);
    }

    // ========== GRADIENT SMOOTHING DITHERING ==========
    // Apply subtle blue noise dithering to reduce banding in smooth gradients
    float dither = texture(uDitheringTex, vScreenPos * 10.0).r;
    dither = (dither - 0.5) * 0.01; // Very small dither amount for gradient smoothing
    cloudColor.a += dither;

    // Clamp alpha
    cloudColor.a = saturate(cloudColor.a);

    // Early discard for performance
    if (cloudColor.a < 0.01) {
        discard;
    }

    // Output final color
    FragColor = cloudColor;
}
