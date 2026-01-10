#ifndef CLOUD_NOISE_GLSL
#define CLOUD_NOISE_GLSL

// ============================================================================
// CLOUD NOISE LIBRARY
// Procedural noise functions for realistic cloud generation
// Supports: Cirrus, Cumulus, Stratus, Storm cloud types
// ============================================================================

// Hash function for pseudo-random value generation
// Returns value in [0, 1] range
float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

// 2D hash returning vec2
vec2 hash2(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(vec2(p.x * p.y, p.x + p.y));
}

// ============================================================================
// WORLEY/CELLULAR NOISE
// Creates cell-like patterns, great for cloud edges and billowy shapes
// ============================================================================

// Multi-distance Worley noise - returns (F1, F2, F2-F1)
// F1 = closest distance, F2 = second closest, F2-F1 = edge distance (great for billowy features)
vec3 worleyNoise2DMulti(vec2 uv, float scale) {
    uv *= scale;
    vec2 id = floor(uv);
    vec2 f = fract(uv);

    float minDist1 = 10.0; // Closest
    float minDist2 = 10.0; // Second closest

    // Check 3x3 neighborhood for two closest cell points
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 neighbor = vec2(float(x), float(y));
            vec2 point = hash2(id + neighbor);
            vec2 diff = neighbor + point - f;
            float dist = length(diff);

            // Track two closest distances
            if (dist < minDist1) {
                minDist2 = minDist1;
                minDist1 = dist;
            } else if (dist < minDist2) {
                minDist2 = dist;
            }
        }
    }

    // Normalize to [0, 1] range (sqrt(2) is max distance in unit square)
    float norm = 1.41421356;
    return vec3(minDist1 / norm, minDist2 / norm, (minDist2 - minDist1) / norm);
}

// Simple Worley noise (just F1 distance)
float worleyNoise2D(vec2 uv, float scale) {
    return worleyNoise2DMulti(uv, scale).x;
}

// Inverted Worley noise (lighter at cell centers)
float worleyNoiseInv(vec2 uv, float scale) {
    return 1.0 - worleyNoise2D(uv, scale);
}

// Billowy Worley noise using F2-F1 (edge distance) - creates cotton-like patterns
float worleyBillowy(vec2 uv, float scale) {
    vec3 worley = worleyNoise2DMulti(uv, scale);
    return 1.0 - worley.z; // Use edge distance for billowy effect
}

// ============================================================================
// PERLIN NOISE
// Smooth gradient noise, excellent for base cloud shapes
// ============================================================================
float perlinNoise2D(vec2 uv, float scale) {
    uv *= scale;
    vec2 i = floor(uv);
    vec2 f = fract(uv);

    // Four corners of the cell
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    // Smoothstep interpolation (C2 continuous)
    vec2 u = f * f * (3.0 - 2.0 * f);

    // Bilinear interpolation
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// ============================================================================
// FRACTAL BROWNIAN MOTION (FBM)
// Layered noise with diminishing amplitude - creates detail at multiple scales
// ============================================================================
float fbm(vec2 uv, int octaves, float lacunarity, float gain) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float maxValue = 0.0; // For normalization

    for (int i = 0; i < octaves; i++) {
        value += amplitude * perlinNoise2D(uv * frequency, 1.0);
        maxValue += amplitude;
        frequency *= lacunarity; // Increase frequency
        amplitude *= gain;        // Decrease amplitude
    }

    // Normalize to [0, 1] range
    return value / maxValue;
}

// FBM with worley base (useful for detailed billowy clouds)
float fbmWorley(vec2 uv, int octaves, float lacunarity, float gain) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float maxValue = 0.0;

    for (int i = 0; i < octaves; i++) {
        value += amplitude * worleyNoiseInv(uv * frequency, 1.0);
        maxValue += amplitude;
        frequency *= lacunarity;
        amplitude *= gain;
    }

    return value / maxValue;
}

// FBM using billowy Worley (F2-F1) - creates fluffy, cotton-like clouds
float fbmBillowy(vec2 uv, int octaves, float lacunarity, float gain) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float maxValue = 0.0;

    for (int i = 0; i < octaves; i++) {
        value += amplitude * worleyBillowy(uv * frequency, 1.0);
        maxValue += amplitude;
        frequency *= lacunarity;
        amplitude *= gain;
    }

    return value / maxValue;
}

// Hybrid FBM: Perlin base + Worley billowy detail - best for realistic cumulus
float fbmHybrid(vec2 uv, int octaves, float lacunarity, float gain, float worleyMix) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float maxValue = 0.0;

    for (int i = 0; i < octaves; i++) {
        float perlin = perlinNoise2D(uv * frequency, 1.0);
        float worley = worleyBillowy(uv * frequency, 1.0);
        float mixed = mix(perlin, worley, worleyMix);

        value += amplitude * mixed;
        maxValue += amplitude;
        frequency *= lacunarity;
        amplitude *= gain;
    }

    return value / maxValue;
}

// ============================================================================
// TURBULENCE
// Absolute value FBM - creates swirling, chaotic patterns
// ============================================================================
float turbulence(vec2 uv, int octaves, float lacunarity, float gain) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float maxValue = 0.0;

    for (int i = 0; i < octaves; i++) {
        value += amplitude * abs(perlinNoise2D(uv * frequency, 1.0) * 2.0 - 1.0);
        maxValue += amplitude;
        frequency *= lacunarity;
        amplitude *= gain;
    }

    return value / maxValue;
}

// ============================================================================
// ADVANCED CLOUD SHAPE GENERATOR WITH DUAL-LAYER SCROLLING NOISE
// Main function that generates cloud patterns based on type with configurable FBM
// Returns density value in [0, 1] range
// Parameters:
// - fbmOctaves: Number of FBM layers (more = more detail)
// - fbmLacunarity: Frequency multiplier per octave (typically 2.0)
// - fbmGain: Amplitude multiplier per octave (typically 0.5)
// - fbmStrength: Overall FBM contribution strength
// ============================================================================
struct CloudNoiseParams {
    int fbmOctaves;
    float fbmLacunarity;
    float fbmGain;
    float fbmStrength;
    float worleyWeight;
    float perlinWeight;
    float erosion;         // How much detail erodes the base shape (creates holes/tears)
    float sharpness;       // Edge sharpness (low = soft, high = defined edges)
};

float cloudShapeAdvanced(vec2 uv, int cloudType, float coverage, CloudNoiseParams params) {
    float noise = 0.0;

    // ========== CIRRUS (Type 0) ==========
    // Thin, wispy, high-altitude clouds with fine streaks
    if (cloudType == 0) {
        // Base: Worley cells for wispy structure
        float base = worleyNoise2D(uv, 8.0);

        // Detail: Multi-octave FBM for fine streaks
        float streaks = fbm(uv * 16.0, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);
        
        // Additional turbulence for wispy appearance
        float turb = turbulence(uv * 12.0, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);

        // Combine with configurable weights
        noise = base * 0.5 + streaks * params.fbmStrength * 0.3 + turb * params.fbmStrength * 0.2;

        // Apply erosion (creates breaks/tears in cirrus)
        float erosionMask = fbm(uv * 20.0, 3, 2.3, 0.6);
        noise *= mix(1.0, erosionMask, params.erosion * 0.5);

        // Power curve for thin, wispy edges with configurable sharpness
        noise = pow(saturate(noise), mix(1.5, 3.5, params.sharpness));
    }

    // ========== CUMULUS (Type 1) ==========
    // Fluffy, puffy clouds with billowy edges and fine detail
    else if (cloudType == 1) {
        // Base: Hybrid FBM combining Perlin smoothness with Worley billowiness
        float basePerlin = fbm(uv, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);
        float baseWorley = fbmBillowy(uv, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);
        float base = mix(basePerlin, baseWorley, params.worleyWeight);

        // Detail: Billowy Worley FBM for fluffy cotton texture
        float billows = fbmBillowy(uv * 2.5, params.fbmOctaves, params.fbmLacunarity * 1.1, params.fbmGain * 0.9);

        // Fine detail layer for realistic texture
        float fineDetail = worleyBillowy(uv, 8.0) * params.fbmStrength * 0.3;

        // Combine with stronger billowy contribution
        noise = base * (1.0 - params.fbmStrength * 0.3) + billows * params.fbmStrength * 0.5 + fineDetail;

        // Apply erosion to create realistic cloud breaks
        float erosionMask = fbmBillowy(uv * 3.0, 3, 2.2, 0.55);
        noise *= mix(1.0, erosionMask, params.erosion);

        // Power curve for defined, puffy edges with configurable sharpness
        noise = pow(saturate(noise), mix(0.6, 1.2, params.sharpness));
    }

    // ========== STRATUS (Type 2) ==========
    // Layered, uniform, low-altitude clouds with subtle variation
    else if (cloudType == 2) {
        // Base: Low-frequency perlin for smooth layer
        float base = fbm(uv * 2.0, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);

        // Detail: FBM for texture variation
        float texture = fbm(uv * 8.0, params.fbmOctaves, params.fbmLacunarity * 1.2, params.fbmGain * 0.8);

        // Combine with gentle blending
        noise = base * (1.0 - params.fbmStrength * 0.2) + texture * params.fbmStrength * 0.3;

        // Minimal erosion for stratus (they're generally continuous)
        float erosionMask = perlinNoise2D(uv * 6.0, 1.0);
        noise *= mix(1.0, erosionMask, params.erosion * 0.3);

        // Smooth out for continuous layer
        noise = smoothstep(mix(0.2, 0.4, params.sharpness), mix(0.7, 0.9, params.sharpness), noise);
    }

    // ========== STORM/CUMULONIMBUS (Type 3) ==========
    // Dense, dark, turbulent storm clouds with chaotic detail
    else if (cloudType == 3) {
        // Base: Mix of Worley and Perlin for dense structure
        float worley = fbmWorley(uv * 3.0, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);
        float perlin = fbm(uv * 3.0, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);
        float base = mix(perlin, worley, params.worleyWeight);

        // Detail: Heavy turbulence for chaotic storm look
        float chaos = turbulence(uv * 6.0, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);
        
        // Additional FBM for anvil-like structure
        float anvilShape = fbmBillowy(uv * 1.5, params.fbmOctaves, params.fbmLacunarity, params.fbmGain);

        // Combine with strong FBM contribution
        noise = base * 0.5 + chaos * params.fbmStrength * 0.3 + anvilShape * params.fbmStrength * 0.3;

        // Erosion creates dramatic breaks in storm clouds
        float erosionMask = turbulence(uv * 8.0, 3, 2.1, 0.6);
        noise *= mix(1.0, erosionMask, params.erosion * 0.7);

        // Boost density and contrast for dark, heavy clouds
        noise = saturate(noise * 1.3);
        noise = pow(noise, mix(0.7, 1.0, params.sharpness));
    }

    // Apply coverage remapping
    // Coverage controls how much of the sky is covered: higher coverage = more clouds
    float coverageThreshold = 1.0 - coverage;
    noise = smoothstep(coverageThreshold - 0.3, coverageThreshold + 0.1, noise);

    return saturate(noise);
}

// Legacy wrapper for backward compatibility
float cloudShape(vec2 uv, int cloudType, float coverage, float detail) {
    CloudNoiseParams params;
    params.fbmOctaves = 4;
    params.fbmLacunarity = 2.0;
    params.fbmGain = 0.5;
    params.fbmStrength = detail;
    params.worleyWeight = 0.6;
    params.perlinWeight = 0.4;
    params.erosion = 0.3;
    params.sharpness = 0.5;
    
    return cloudShapeAdvanced(uv, cloudType, coverage, params);
}

// ============================================================================
// CLOUD DENSITY WITH HEIGHT VARIATION
// Optional: vary cloud density based on altitude (y-coordinate)
// ============================================================================
float cloudDensityWithHeight(vec2 uv, float height, int cloudType, float coverage, float detail) {
    // Get base cloud shape
    float density = cloudShape(uv, cloudType, coverage, detail);

    // Modulate by height (clouds thinner at top and bottom of layer)
    float heightFalloff = 1.0 - abs(height - 0.5) * 2.0; // Peak at 0.5, fade at 0 and 1
    heightFalloff = pow(heightFalloff, 0.5); // Smooth falloff

    return density * heightFalloff;
}

// ============================================================================
// UTILITY: REMAP FUNCTION
// ============================================================================
float remap(float value, float oldMin, float oldMax, float newMin, float newMax) {
    return newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin);
}

#endif // CLOUD_NOISE_GLSL
