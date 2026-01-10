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
uniform float uCloudOpacity;       // 0-1: overall opacity multiplier
uniform int uCloudType;            // 0=cirrus, 1=cumulus, 2=stratus, 3=storm
uniform vec3 uCloudWindOffset;     // XYZ offset for animation (accumulated over time)
uniform float uCloudScattering;    // Sun scattering intensity
uniform vec3 uCloudSunDir;         // Direction to sun
uniform vec3 uCloudSunColor;       // Sun color
uniform float uCloudSunIntensity;  // Sun/Moon light intensity (brightness)
uniform float uCloudAmbient;       // Ambient light contribution
uniform float uCloudSpeed;         // Animation speed multiplier
uniform float uCloudDetailSpeed;   // Detail animation speed for organic shape changes
uniform sampler2D uDitheringTex;   // Dithering texture for smooth gradients

// Fine-tune parameters
uniform float uCloudNoiseScale;    // Global noise scale multiplier
uniform float uCloudMorphSpeed;    // Organic morphing speed
uniform float uCloudEdgeSoftness;  // Edge softness/hardness
uniform float uCloudBillowiness;   // Cotton/billowy appearance strength
uniform float uCloudTurbulence;    // Shape distortion/turbulence over time
uniform float uCloudDetailStrength;// Fine detail strength

// === DUAL-LAYER SCROLLING NOISE PARAMETERS ===
// Layer 1: Primary noise layer (large-scale cloud shapes)
uniform float uNoiseLayer1Speed;   // Speed multiplier for layer 1 scrolling
uniform vec2 uNoiseLayer1Direction; // Direction of layer 1 scrolling (normalized)
uniform float uNoiseLayer1Scale;    // Scale multiplier for layer 1

// Layer 2: Secondary noise layer (detail/erosion)
uniform float uNoiseLayer2Speed;   // Speed multiplier for layer 2 scrolling
uniform vec2 uNoiseLayer2Direction; // Direction of layer 2 scrolling (normalized)
uniform float uNoiseLayer2Scale;    // Scale multiplier for layer 2

// === FBM PARAMETERS (Per-Type Customizable) ===
uniform int uFBMOctaves;           // Number of FBM octaves (2-8)
uniform float uFBMLacunarity;      // Frequency multiplier per octave (1.5-3.0)
uniform float uFBMGain;            // Amplitude multiplier per octave (0.3-0.7)
uniform float uFBMStrength;        // Overall FBM contribution (0.0-1.0)
uniform float uWorleyWeight;       // Worley noise weight in hybrid mixing (0.0-1.0)
uniform float uErosion;            // Erosion strength (creates holes/tears) (0.0-1.0)
uniform float uSharpness;          // Edge sharpness (0.0-1.0)

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
// Takes light intensity into account for day/night variation (like WaterOcean)
// ============================================================================
vec3 calculateCloudLighting(vec3 viewDir, vec3 baseColor, float density) {
    // Sun direction dot product
    float sunDot = dot(viewDir, uCloudSunDir);

    // Forward scattering (silver lining effect)
    float scatter = fastScattering(sunDot, uCloudScattering);

    // === ENVIRONMENT BRIGHTNESS (like WaterOcean) ===
    // Calculate overall scene brightness for physically correct cloud response
    // Clouds should be dark at night, bright during day
    // Minimum 0.05 ensures clouds aren't completely black at night
    float environmentBrightness = saturate(uDirLightIntensity + 0.05);
    
    // Scale lighting by environment brightness (much darker at night)
    float lightIntensity = uCloudSunIntensity * environmentBrightness;

    // Ambient term (sky color contribution) - reduced at night
    vec3 ambient = baseColor * uCloudAmbient * environmentBrightness;

    // Sun contribution (more visible when looking towards sun) - scaled by environment brightness
    vec3 sunContribution = uCloudSunColor * scatter * environmentBrightness;

    // Direct illumination from sun (diffuse-like term)
    // Clouds facing the sun are brighter
    float sunFacing = saturate(dot(vec3(0, 1, 0), uCloudSunDir)); // Assume clouds face up
    vec3 directLight = uCloudSunColor * sunFacing * environmentBrightness;

    // Combine lighting with environment brightness modulation
    vec3 finalColor = baseColor * environmentBrightness * 0.3 + // Base color with light
                      directLight * 0.5 +                          // Direct illumination
                      sunContribution +                            // Scattering
                      ambient;                                     // Ambient

    // Darken dense clouds (self-shadowing approximation)
    float darkening = mix(1.0, 0.7, density * 0.5);
    finalColor *= darkening;

    // Minimum brightness for night (moonlight/starlight) - very subtle
    vec3 nightMinimum = baseColor * 0.02 * environmentBrightness; // 2% minimum visibility, scaled by brightness
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

        // === DUAL-LAYER SCROLLING NOISE SYSTEM ===
        // Calculate base UV with parallax and global noise scale
        vec2 baseUV = sphereToUV(dir) * LAYER_SCALES[i] * uCloudNoiseScale;

        // === LAYER 1: PRIMARY NOISE (Large-scale cloud shapes) ===
        // Calculate scrolling offset for layer 1 with independent speed and direction
        vec2 layer1Offset = uCloudWindOffset.xy * LAYER_SPEEDS[i] * uNoiseLayer1Speed;
        layer1Offset += uNoiseLayer1Direction * uTime * uCloudSpeed * uNoiseLayer1Speed * 0.1;
        vec2 uv1 = baseUV * uNoiseLayer1Scale + layer1Offset;

        // === LAYER 2: SECONDARY NOISE (Detail/erosion/morphing) ===
        // Calculate scrolling offset for layer 2 with independent speed and direction
        vec2 layer2Offset = uCloudWindOffset.xy * LAYER_SPEEDS[i] * uNoiseLayer2Speed;
        layer2Offset += uNoiseLayer2Direction * uTime * uCloudSpeed * uNoiseLayer2Speed * 0.15;
        vec2 uv2 = baseUV * uNoiseLayer2Scale + layer2Offset;

        // === ORGANIC SHAPE EVOLUTION ===
        // Add time-based distortion for evolving cloud shapes on both layers
        // Use uCloudDetailSpeed for secondary layer evolution speed
        float timeOffset = uTime * uCloudMorphSpeed * 0.1;
        float detailTimeOffset = uTime * uCloudDetailSpeed * 0.15;

        // Add turbulence factor for chaotic distortion
        float turbulenceTime = uTime * uCloudTurbulence * 0.2;
        
        // Distort UV1 (primary layer) with animated noise for organic movement
        // Add turbulence to create more chaotic, evolving shapes
        float distortionStrength1 = 0.25 * uCloudMorphSpeed; // Increased from 0.15 for more visible morphing
        vec2 turbulence1 = vec2(
            perlinNoise2D(uv1 * 1.5 + vec2(turbulenceTime * 0.5, -turbulenceTime * 0.4), 3.0),
            perlinNoise2D(uv1 * 1.5 + vec2(-turbulenceTime * 0.45, turbulenceTime * 0.5), 3.0)
        );
        turbulence1 = (turbulence1 - 0.5) * 0.15 * uCloudTurbulence;
        
        vec2 distortion1 = vec2(
            perlinNoise2D(uv1 * 0.5 + vec2(timeOffset * 0.3, timeOffset * 0.2), 2.0),
            perlinNoise2D(uv1 * 0.5 + vec2(timeOffset * 0.2, -timeOffset * 0.3), 2.0)
        );
        distortion1 = (distortion1 - 0.5) * distortionStrength1;
        vec2 animatedUV1 = uv1 + distortion1 + turbulence1;

        // Distort UV2 (secondary layer) with different parameters and detail speed for independent motion
        float distortionStrength2 = 0.3 * uCloudDetailSpeed; // Use detail speed here
        vec2 turbulence2 = vec2(
            perlinNoise2D(uv2 * 1.8 + vec2(-turbulenceTime * 0.6, turbulenceTime * 0.5), 3.5),
            perlinNoise2D(uv2 * 1.8 + vec2(turbulenceTime * 0.55, -turbulenceTime * 0.6), 3.5)
        );
        turbulence2 = (turbulence2 - 0.5) * 0.18 * uCloudTurbulence;
        
        vec2 distortion2 = vec2(
            perlinNoise2D(uv2 * 0.6 + vec2(detailTimeOffset * 0.4, -detailTimeOffset * 0.25), 2.5),
            perlinNoise2D(uv2 * 0.6 + vec2(-detailTimeOffset * 0.35, detailTimeOffset * 0.3), 2.5)
        );
        distortion2 = (distortion2 - 0.5) * distortionStrength2;
        vec2 animatedUV2 = uv2 + distortion2 + turbulence2;

        // === ADVANCED FBM WITH CONFIGURABLE PARAMETERS ===
        // Prepare noise parameters struct for advanced cloud generation
        CloudNoiseParams noiseParams;
        noiseParams.fbmOctaves = uFBMOctaves;
        noiseParams.fbmLacunarity = uFBMLacunarity;
        noiseParams.fbmGain = uFBMGain;
        noiseParams.fbmStrength = uFBMStrength;
        noiseParams.worleyWeight = uWorleyWeight;
        noiseParams.perlinWeight = 1.0 - uWorleyWeight;
        noiseParams.erosion = uErosion;
        noiseParams.sharpness = uSharpness;

        // Generate primary cloud shape using layer 1 UV
        float baseNoise = cloudShapeAdvanced(animatedUV1, uCloudType, uCloudCoverage, noiseParams);
        
        // Generate secondary detail/erosion layer using layer 2 UV
        // This creates independent motion and evolution for fine details
        float detailNoise = cloudShapeAdvanced(animatedUV2, uCloudType, uCloudCoverage * 0.8, noiseParams);
        
        // === DUAL-LAYER COMPOSITION ===
        // Combine primary and secondary layers for organic morphing
        // Primary layer defines main shape, secondary adds detail and erosion
        float noise = baseNoise;
        
        // Apply secondary layer as detail/erosion based on erosion parameter
        // Low erosion = additive detail (more fluffy)
        // High erosion = subtractive detail (creates breaks/tears)
        float detailContribution = detailNoise * uCloudDetailStrength;
        noise = mix(
            noise * (0.7 + detailContribution * 0.3),  // Additive detail
            noise * detailNoise,                        // Multiplicative erosion
            uErosion
        );
        
        // Add temporal variation (breathing/morphing effect)
        float breathe = perlinNoise2D(animatedUV1 * 0.2 + vec2(timeOffset * 0.1), 1.0) * 
                       sin(timeOffset * 0.5) * 0.1 * uCloudMorphSpeed;
        noise += breathe;

        // === BILLOWINESS (Cotton/Fluffy Appearance) ===
        // Add billowy/puffy detail to make clouds look more cotton-like
        if (uCloudBillowiness > 0.01) {
            // Multi-scale billowy noise for fluffy appearance
            vec2 billowUV = animatedUV1 * 2.5;
            float billow1 = perlinNoise2D(billowUV + vec2(timeOffset * 0.15), 2.0);
            float billow2 = perlinNoise2D(billowUV * 1.7 + vec2(-timeOffset * 0.12, timeOffset * 0.18), 1.5);
            
            // Combine billowy layers
            float billowPattern = (billow1 * 0.6 + billow2 * 0.4);
            
            // Apply billowiness as additive puffiness (creates rounded bulges)
            // More billowiness = more puffy/cotton-like
            float billowContribution = billowPattern * uCloudBillowiness * 0.3;
            noise = saturate(noise + billowContribution * noise); // Amplify existing clouds
        }

        // Clamp and ensure valid range
        noise = saturate(noise);

        // === EDGE SOFTNESS ===
        // Apply edge softness for smooth cloud boundaries
        float alpha = noise;
        
        // Apply edge softness: fade edges more gradually with higher softness
        if (uCloudEdgeSoftness > 0.01) {
            float edgeRange = mix(0.05, 0.25, uCloudEdgeSoftness);
            alpha = smoothstep(edgeRange, 1.0 - edgeRange, noise);
        }

        // Modulate alpha by layer weight
        alpha *= LAYER_WEIGHTS[i];

        // Horizon fade (clouds fade near horizon for realism)
        float horizonFade = smoothstep(-0.05, 0.2, dirY);
        alpha *= horizonFade;

        // Apply overall density control
        alpha *= uCloudDensity;
        
        // Apply overall opacity multiplier
        alpha *= uCloudOpacity;

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
