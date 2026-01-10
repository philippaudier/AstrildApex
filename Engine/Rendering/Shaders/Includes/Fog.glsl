// Fog.glsl - Fog calculations and effects with advanced FBM noise

// Simple 3D hash for procedural noise
float hash3D(vec3 p) {
    p = fract(p * vec3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 19.19);
    return fract((p.x + p.y) * p.z);
}

// 3D Perlin-like noise
float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f); // Smoothstep
    
    float a = hash3D(i);
    float b = hash3D(i + vec3(1.0, 0.0, 0.0));
    float c = hash3D(i + vec3(0.0, 1.0, 0.0));
    float d = hash3D(i + vec3(1.0, 1.0, 0.0));
    float e = hash3D(i + vec3(0.0, 0.0, 1.0));
    float g = hash3D(i + vec3(1.0, 0.0, 1.0));
    float h = hash3D(i + vec3(0.0, 1.0, 1.0));
    float k = hash3D(i + vec3(1.0, 1.0, 1.0));
    
    return mix(mix(mix(a, b, f.x), mix(c, d, f.x), f.y),
               mix(mix(e, g, f.x), mix(h, k, f.x), f.y), f.z);
}

// FBM (Fractal Brownian Motion) noise for volumetric fog
float fbmNoise3D(vec3 p, int octaves, float lacunarity, float gain) {
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float maxValue = 0.0;
    
    for (int i = 0; i < octaves; i++) {
        value += amplitude * noise3D(p * frequency);
        maxValue += amplitude;
        amplitude *= gain;
        frequency *= lacunarity;
    }
    
    return value / maxValue;
}

// Calculate linear fog factor
float calculateLinearFogFactor(vec3 worldPos, vec3 cameraPos, float fogStart, float fogEnd) {
    float dist = length(cameraPos - worldPos);
    return saturate((fogEnd - dist) / max(EPSILON, (fogEnd - fogStart)));
}

// Calculate exponential fog factor
float calculateExponentialFogFactor(vec3 worldPos, vec3 cameraPos, float fogDensity) {
    float dist = length(cameraPos - worldPos);
    return 1.0 / exp(dist * fogDensity);
}

// Calculate exponential squared fog factor
float calculateExponentialSquaredFogFactor(vec3 worldPos, vec3 cameraPos, float fogDensity) {
    float dist = length(cameraPos - worldPos);
    float factor = dist * fogDensity;
    return 1.0 / exp(factor * factor);
}

// Apply fog to the final color using linear interpolation
vec3 applyFog(vec3 color, vec3 fogColor, float fogFactor) {
    return mix(fogColor, color, fogFactor);
}

// Main fog processing function with height-based enhancement
// Uses world position height and distance to simulate fog accumulation in valleys
// Main fog processing function with advanced FBM, layers, and scattering
vec3 processFog(vec3 color, vec3 worldPos) {
    if (uFogEnabled == 0) return color;

    // === DISTANCE-BASED FOG ===
    vec3 viewDir = normalize(worldPos - uCameraPos);
    float distance = length(worldPos - uCameraPos);
    float distanceFogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
    
    // === HEIGHT-BASED FOG DENSITY ===
    // Calculate height above fog layer base
    float heightAboveFogLayer = worldPos.y - uFogLayerHeight;
    
    // Exponential fog falloff with height - more fog near fog layer height
    float heightFalloffRate = 0.05 + uFogDensity * 0.5;
    float heightDensity = exp(-max(0.0, heightAboveFogLayer) * heightFalloffRate);
    
    // Extra density below fog layer (valleys, depressions)
    if (heightAboveFogLayer < 0.0) {
        float depthBelowLayer = abs(heightAboveFogLayer);
        heightDensity *= 1.0 + depthBelowLayer * 0.2;
        heightDensity = clamp(heightDensity, 0.0, 2.0);
    }
    
    // === VOLUMETRIC NOISE (FBM) ===
    // Animate fog position with time
    vec3 fogSamplePos = worldPos * uFogNoiseScale + vec3(uTime * uFogNoiseSpeed * 0.1, 0.0, uTime * uFogNoiseSpeed * 0.15);
    
    // Calculate FBM noise for volumetric fog variation
    float fogNoise = fbmNoise3D(fogSamplePos, uFogFBMOctaves, uFogFBMLacunarity, uFogFBMGain);
    
    // Apply noise to density (creates wispy, evolving fog)
    float noiseDensityModulation = mix(0.7, 1.3, fogNoise);
    heightDensity *= noiseDensityModulation;
    
    // === COMBINE DISTANCE AND HEIGHT ===
    float combinedFogFactor = distanceFogFactor * (1.0 - heightDensity * 0.7);
    combinedFogFactor = saturate(combinedFogFactor);
    
    // === EXPONENTIAL FOG OPTION ===
    if (uFogDensity > 0.01) {
        float expFog = 1.0 - exp(-distance * uFogDensity * heightDensity);
        combinedFogFactor = mix(combinedFogFactor, 1.0 - expFog, 0.3);
    }
    
    // === APPLY THICKNESS (OPACITY) ===
    // More fog = more opaque, controlled by thickness parameter
    float fogOpacity = (1.0 - combinedFogFactor) * uFogThickness * uFogOpacity;
    fogOpacity = saturate(fogOpacity);
    combinedFogFactor = 1.0 - fogOpacity;
    
    // === SUN SCATTERING IN FOG ===
    // Calculate forward scattering (sun glow through fog)
    float sunDot = dot(viewDir, -uDirLightDirection);
    float scattering = pow(saturate(sunDot), 8.0) * uFogScattering;
    
    // === ENVIRONMENT BRIGHTNESS ===
    // Modulate fog color by light intensity for physically correct day/night cycle
    float environmentBrightness = saturate(uDirLightIntensity + 0.01);
    vec3 modulatedFogColor = uFogColor * environmentBrightness;
    
    // Add sun scattering glow to fog color
    vec3 sunScatterColor = uDirLightColor * scattering * uDirLightIntensity;
    modulatedFogColor += sunScatterColor * 0.5;
    
    return applyFog(color, modulatedFogColor, combinedFogFactor);
}

// Enhanced fog processing with multiple fog types (for future use)
vec3 processFogAdvanced(vec3 color, vec3 worldPos, int fogType, float fogDensity) {
    if (uFogEnabled == 0) return color;

    float distance = length(worldPos - uCameraPos);
    float fogFactor;

    switch (fogType) {
        case 0: // Linear fog
            fogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
            break;
        case 1: // Exponential fog
            fogFactor = calculateExponentialFogFactor(worldPos, uCameraPos, fogDensity);
            break;
        case 2: // Exponential squared fog
            fogFactor = calculateExponentialSquaredFogFactor(worldPos, uCameraPos, fogDensity);
            break;
        default:
            fogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
            break;
    }
    
    // === HEIGHT-BASED ENHANCEMENT ===
    float baseHeight = 0.0;
    float heightAboveBase = worldPos.y - baseHeight;
    float heightFalloffRate = 0.05 + fogDensity * 0.5;
    float heightDensity = exp(-max(0.0, heightAboveBase) * heightFalloffRate);
    
    // Extra density for valleys
    if (heightAboveBase < 0.0) {
        float depthBelowBase = abs(heightAboveBase);
        heightDensity *= 1.0 + depthBelowBase * 0.2;
        heightDensity = clamp(heightDensity, 0.0, 2.0);
    }
    
    // Modulate fog with height
    fogFactor = fogFactor * (1.0 - heightDensity * 0.7);
    fogFactor = saturate(fogFactor);

    // === ENVIRONMENT BRIGHTNESS ===
    // Modulate fog color by light intensity for physically correct day/night cycle
    // Fog should be bright during day, dark at night
    float environmentBrightness = saturate(uDirLightIntensity + 0.01);
    vec3 modulatedFogColor = uFogColor * environmentBrightness;

    return applyFog(color, modulatedFogColor, fogFactor);
}
