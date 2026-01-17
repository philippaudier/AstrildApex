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

// Main fog processing function - simplified and predictable
// FogStart/FogEnd control the distance range, FogDensity controls intensity
vec3 processFog(vec3 color, vec3 worldPos) {
    if (uFogEnabled == 0) return color;

    // === DISTANCE-BASED FOG (PRIMARY) ===
    vec3 viewDir = normalize(worldPos - uCameraPos);
    float distance = length(worldPos - uCameraPos);

    // Linear fog based on distance: 1 = clear (before FogStart), 0 = full fog (after FogEnd)
    float distanceFogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);

    // === OPTIONAL EXPONENTIAL FOG ===
    // When FogDensity > 0, blend in exponential fog for more realistic falloff
    if (uFogDensity > 0.001) {
        float expFog = exp(-distance * uFogDensity * 0.01); // 0.01 scale for reasonable values
        // Blend: higher density = more exponential character
        float blendFactor = saturate(uFogDensity * 2.0);
        distanceFogFactor = mix(distanceFogFactor, expFog, blendFactor * 0.5);
    }

    // === HEIGHT-BASED FOG (SUBTLE) ===
    // Only apply height fog if FogLayerHeight > 0 and FogThickness > 0
    float heightInfluence = 0.0;
    if (uFogThickness > 0.01 && uFogLayerHeight > -1000.0) {
        float heightAboveFogLayer = worldPos.y - uFogLayerHeight;

        // Smooth height falloff - fog is denser at/below fog layer height
        // Use a gentler curve that doesn't dominate the distance fog
        if (heightAboveFogLayer < uFogThickness) {
            float normalizedHeight = saturate((uFogThickness - heightAboveFogLayer) / max(1.0, uFogThickness));
            heightInfluence = normalizedHeight * 0.3; // Max 30% extra fog from height
        }
    }

    // === COMBINE FOG FACTORS ===
    // Start with distance fog, then subtract height influence
    float combinedFogFactor = distanceFogFactor - heightInfluence;
    combinedFogFactor = saturate(combinedFogFactor);

    // === OPTIONAL VOLUMETRIC NOISE ===
    // Only apply noise where fog is actually visible (fogAmount > 0)
    // This prevents noise from appearing in clear areas (before FogStart)
    float baseFogAmount = 1.0 - combinedFogFactor;
    if (uFogNoiseScale > 0.0001 && baseFogAmount > 0.01) {
        vec3 fogSamplePos = worldPos * uFogNoiseScale + vec3(uTime * uFogNoiseSpeed * 0.1, 0.0, uTime * uFogNoiseSpeed * 0.15);
        float fogNoise = fbmNoise3D(fogSamplePos, uFogFBMOctaves, uFogFBMLacunarity, uFogFBMGain);
        // Scale noise by fog amount - more fog = more noise visible
        float noiseInfluence = (fogNoise - 0.5) * 0.3 * baseFogAmount;
        combinedFogFactor = saturate(combinedFogFactor + noiseInfluence);
    }

    // === APPLY OPACITY ===
    // FogOpacity controls overall fog strength (0 = no fog, 1 = full fog)
    float fogAmount = (1.0 - combinedFogFactor) * uFogOpacity;
    fogAmount = saturate(fogAmount);

    // === SUN SCATTERING IN FOG ===
    float sunDot = dot(viewDir, -uDirLightDirection);
    float scattering = pow(saturate(sunDot), 8.0) * uFogScattering * fogAmount;

    // === FOG COLOR - PHYSICALLY BASED ===
    // Real fog behavior:
    // - Fog scatters light (Mie scattering) - appears the color of the light source
    // - At night with no light: fog is nearly invisible (very dark)
    // - At day: fog is bright white/gray from sunlight scattering
    // - At golden hour: fog takes warm orange/pink tints
    // - Moonlit nights: subtle blue-gray tint

    // Base fog color from light sources
    // Fog reflects/scatters the available light in the scene
    vec3 sunContribution = uDirLightColor * uDirLightIntensity;
    vec3 ambientContribution = uAmbientColor * uAmbientIntensity * 0.5;

    // Night fog base (very dark, slight blue from moonlight/starlight)
    vec3 nightBaseFog = vec3(0.015, 0.02, 0.035);

    // Day fog base (neutral gray-white from scattered sunlight)
    vec3 dayBaseFog = vec3(0.7, 0.72, 0.75);

    // Golden hour warm tint
    vec3 goldenFog = vec3(1.0, 0.85, 0.6);

    vec3 modulatedFogColor;
    if (uFogColorMode == 1) {
        // Ambient mode: physically-based fog coloring
        // Fog color = scattered light from sun + ambient
        vec3 scatteredLight = sunContribution * 0.8 + ambientContribution;
        // Blend between dark night fog and light-colored day fog
        modulatedFogColor = mix(nightBaseFog, scatteredLight, uDayNightBlend);
        // Add golden hour influence
        modulatedFogColor = mix(modulatedFogColor, goldenFog * uDirLightIntensity, uGoldenHourBlend * 0.4);
    } else if (uFogColorMode == 2) {
        // Skybox mode: match horizon/sky colors
        vec3 skyInfluence = mix(nightBaseFog, dayBaseFog, uDayNightBlend);
        // Tint by sun color during day
        skyInfluence = mix(skyInfluence, uDirLightColor * 0.6, uDayNightBlend * 0.3);
        // Strong golden hour effect
        modulatedFogColor = mix(skyInfluence, goldenFog * 0.8, uGoldenHourBlend * 0.5);
    } else if (uFogColorMode == 3) {
        // IBL mode: use environment lighting more directly
        vec3 envLight = sunContribution + ambientContribution * 2.0;
        modulatedFogColor = mix(nightBaseFog * 1.5, envLight * 0.7, uDayNightBlend);
    } else {
        // Custom mode (0): use user-defined FogColor
        // Still modulate by available light for realism
        float lightLevel = max(uDirLightIntensity, uAmbientIntensity * 0.3);
        lightLevel = mix(0.08, 1.0, lightLevel); // Minimum visibility at night
        modulatedFogColor = uFogColor * lightLevel;
    }

    // Sun scattering glow (forward scattering when looking towards sun)
    // Only visible when sun is up and fog is present
    vec3 sunScatterColor = uDirLightColor * scattering * uDirLightIntensity;
    modulatedFogColor += sunScatterColor * 0.35;

    // Final blend: fogAmount controls how much fog color replaces scene color
    return mix(color, modulatedFogColor, fogAmount);
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
