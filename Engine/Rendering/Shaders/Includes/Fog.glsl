// Fog.glsl - Fog calculations and effects

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
vec3 processFog(vec3 color, vec3 worldPos) {
    if (uFogEnabled == 0) return color;

    // === DISTANCE-BASED FOG ===
    float distance = length(worldPos - uCameraPos);
    float distanceFogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
    
    // === HEIGHT-BASED FOG DENSITY ===
    // Create a "base height" concept where fog is thickest
    // Use FogDensity from WeatherComponent to control the base level
    float baseHeight = 0.0; // Ground level
    float heightAboveBase = worldPos.y - baseHeight;
    
    // Exponential fog falloff with height - more fog near ground
    // Use uFogDensity to control how quickly fog dissipates with altitude
    float heightFalloffRate = 0.05 + uFogDensity * 0.5; // Scale with fog density setting
    float heightDensity = exp(-max(0.0, heightAboveBase) * heightFalloffRate);
    
    // Add extra density for areas BELOW base height (valleys, depressions)
    if (heightAboveBase < 0.0) {
        float depthBelowBase = abs(heightAboveBase);
        // Exponentially increase fog density the deeper we go
        heightDensity *= 1.0 + depthBelowBase * 0.2; // Up to 2x more fog in deep valleys
        heightDensity = clamp(heightDensity, 0.0, 2.0);
    }
    
    // === COMBINE DISTANCE AND HEIGHT ===
    // Height density modulates the distance fog
    // Less fog factor = more fog visible
    float combinedFogFactor = distanceFogFactor * (1.0 - heightDensity * 0.7);
    combinedFogFactor = saturate(combinedFogFactor);
    
    // === EXPONENTIAL FOG OPTION ===
    // If FogDensity is high, blend in exponential fog for better depth perception
    if (uFogDensity > 0.01) {
        float expFog = 1.0 - exp(-distance * uFogDensity * heightDensity);
        combinedFogFactor = mix(combinedFogFactor, 1.0 - expFog, 0.3); // 30% exponential blend
    }
    
    return applyFog(color, uFogColor, combinedFogFactor);
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

    return applyFog(color, uFogColor, fogFactor);
}
