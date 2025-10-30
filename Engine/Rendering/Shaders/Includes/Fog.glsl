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

// Main fog processing function (using current fog settings from Global uniform)
vec3 processFog(vec3 color, vec3 worldPos) {
    if (uFogEnabled == 0) return color;

    float fogFactor = calculateLinearFogFactor(worldPos, uCameraPos, uFogStart, uFogEnd);
    return applyFog(color, uFogColor, fogFactor);
}

// Enhanced fog processing with multiple fog types (for future use)
vec3 processFogAdvanced(vec3 color, vec3 worldPos, int fogType, float fogDensity) {
    if (uFogEnabled == 0) return color;

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

    return applyFog(color, uFogColor, fogFactor);
}
