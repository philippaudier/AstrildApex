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
// CLOUD SHAPE GENERATOR
// Main function that generates cloud patterns based on type
// Returns density value in [0, 1] range
// ============================================================================
float cloudShape(vec2 uv, int cloudType, float coverage, float detail) {
    float noise = 0.0;

    // ========== CIRRUS (Type 0) ==========
    // Thin, wispy, high-altitude clouds
    // Characteristics: High frequency, streaky, low coverage
    if (cloudType == 0) {
        // Base: Worley cells for wispy structure
        float base = worleyNoise2D(uv, 8.0);

        // Detail: High-frequency perlin for streaks
        float streaks = perlinNoise2D(uv, 32.0) * detail * 0.3;

        // Combine
        noise = base + streaks;

        // Power curve for thin, wispy edges
        noise = pow(saturate(noise), 2.5);
    }

    // ========== CUMULUS (Type 1) ==========
    // Fluffy, puffy clouds with defined edges
    // Characteristics: Billowy, medium coverage, cotton-like (UE4-style)
    else if (cloudType == 1) {
        // Base: Hybrid FBM combining Perlin smoothness with Worley billowiness
        // Higher worleyMix (0.6) for more cotton/billowy appearance
        float base = fbmHybrid(uv, 4, 2.0, 0.5, 0.6);

        // Detail: Billowy Worley FBM for fluffy edges and cotton texture
        float billows = fbmBillowy(uv * 2.0, 3, 2.2, 0.55) * detail;

        // Combine with stronger billowy contribution
        noise = base * 0.6 + billows * 0.4;

        // Add multi-scale detail for organic look
        float fineDetail = worleyBillowy(uv, 8.0) * detail * 0.2;
        noise += fineDetail;

        // Power curve for defined, puffy edges (like cotton balls)
        noise = pow(saturate(noise), 0.8);
    }

    // ========== STRATUS (Type 2) ==========
    // Layered, uniform, low-altitude clouds
    // Characteristics: Smooth, widespread, continuous layer
    else if (cloudType == 2) {
        // Base: Low-frequency perlin for smooth layer
        float base = perlinNoise2D(uv, 2.0);

        // Detail: Subtle FBM for texture variation
        float texture = fbm(uv * 8.0, 2, 2.0, 0.5) * detail * 0.2;

        // Combine
        noise = base + texture;

        // Smooth out for continuous layer
        noise = smoothstep(0.3, 0.7, noise);
    }

    // ========== STORM/CUMULONIMBUS (Type 3) ==========
    // Dense, dark, turbulent storm clouds
    // Characteristics: Very thick, high coverage, chaotic
    else if (cloudType == 3) {
        // Base: Mix of Worley and Perlin for dense structure
        float worley = worleyNoiseInv(uv, 4.0);
        float perlin = perlinNoise2D(uv, 3.0);
        float base = worley * 0.6 + perlin * 0.4;

        // Detail: Heavy turbulence for chaotic storm look
        float chaos = fbm(uv * 6.0, 4, 2.0, 0.5) * detail * 0.5;

        // Combine
        noise = base + chaos;

        // Boost density for dark, heavy clouds
        noise = saturate(noise * 1.2);
    }

    // Apply coverage remapping
    // This controls how much of the sky is covered by clouds
    noise = smoothstep(coverage - 0.3, coverage + 0.1, noise);

    return saturate(noise);
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
