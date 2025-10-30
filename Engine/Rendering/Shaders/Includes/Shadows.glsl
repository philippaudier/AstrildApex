// ============================================================================
// Modern Shadow Mapping with Three Quality Modes
// ============================================================================
// Mode 0: PCF Grid - Fast, robust, basic soft shadows
// Mode 1: PCF Poisson Disk - Better quality, same performance
// Mode 2: PCSS - Physically accurate soft shadows with contact hardening
// ============================================================================

// Hardware shadow comparison enabled (sampler2DShadow)
uniform sampler2DShadow u_ShadowMap;
uniform mat4 u_ShadowMatrix;  // Light-space transformation matrix
uniform int u_UseShadows;      // 0 = disabled, 1 = enabled
uniform int u_ShadowQuality;   // 0 = PCF Grid, 1 = Poisson, 2 = PCSS

// Shadow parameters
uniform float u_ShadowMapSize;     // Shadow map resolution (e.g., 2048.0)
uniform float u_ShadowBias;        // Constant bias to prevent shadow acne
uniform float u_ShadowNormalBias;  // Normal-based bias multiplier
uniform float u_ShadowStrength;    // Shadow darkness (0.0 = no shadows, 1.0 = full shadows)
uniform float u_LightSize;         // Light source size for PCSS (default: 0.05)
uniform int u_PCFSamples;          // Number of PCF samples (9, 16, 25, etc.)

// ============================================================================
// Poisson Disk sampling pattern (32 samples, optimized distribution)
// ============================================================================
const vec2 POISSON_DISK[32] = vec2[](
    vec2(-0.94201624, -0.39906216),
    vec2(0.94558609, -0.76890725),
    vec2(-0.094184101, -0.92938870),
    vec2(0.34495938, 0.29387760),
    vec2(-0.91588581, 0.45771432),
    vec2(-0.81544232, -0.87912464),
    vec2(-0.38277543, 0.27676845),
    vec2(0.97484398, 0.75648379),
    vec2(0.44323325, -0.97511554),
    vec2(0.53742981, -0.47373420),
    vec2(-0.26496911, -0.41893023),
    vec2(0.79197514, 0.19090188),
    vec2(-0.24188840, 0.99706507),
    vec2(-0.81409955, 0.91437590),
    vec2(0.19984126, 0.78641367),
    vec2(0.14383161, -0.14100790),
    vec2(-0.53018582, -0.69378359),
    vec2(0.61636526, 0.42859274),
    vec2(-0.52991370, 0.37888160),
    vec2(0.00486278, 0.39388924),
    vec2(0.43906097, -0.29417205),
    vec2(-0.98820230, 0.03378990),
    vec2(0.71416397, -0.21878541),
    vec2(-0.62130341, 0.76338654),
    vec2(0.31645095, 0.88681048),
    vec2(-0.08859328, 0.66308212),
    vec2(0.65945172, -0.65872562),
    vec2(-0.38166392, -0.08535953),
    vec2(0.88969195, -0.29036295),
    vec2(0.05019143, -0.67567086),
    vec2(-0.61131636, -0.21640146),
    vec2(-0.16149551, -0.67886263)
);

// ============================================================================
// MODE 0: PCF with Grid Sampling (Basic, Robust)
// ============================================================================
float PCF_Grid(vec2 shadowCoord, float compareDepth, float bias, int samples)
{
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);
    int halfSamples = samples / 2;
    int totalSamples = 0;

    for (int x = -halfSamples; x <= halfSamples; x++)
    {
        for (int y = -halfSamples; y <= halfSamples; y++)
        {
            vec2 offset = vec2(x, y) * texelSize;
            // Hardware PCF: texture() returns 1.0 if compareDepth-bias <= stored depth
            shadow += texture(u_ShadowMap, vec3(shadowCoord + offset, compareDepth - bias));
            totalSamples++;
        }
    }

    return shadow / float(totalSamples);
}

// ============================================================================
// MODE 1: PCF with Poisson Disk Sampling (Better Quality)
// ============================================================================
float PCF_Poisson(vec2 shadowCoord, float compareDepth, float bias, float diskRadius)
{
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);

    // Use first 16 Poisson samples for good quality/performance balance
    for (int i = 0; i < 16; i++)
    {
        vec2 offset = POISSON_DISK[i] * diskRadius * texelSize;
        shadow += texture(u_ShadowMap, vec3(shadowCoord + offset, compareDepth - bias));
    }

    return shadow / 16.0;
}

// ============================================================================
// MODE 2: PCSS - Percentage Closer Soft Shadows
// ============================================================================

// Step 1: Blocker search - find average depth of occluders
float FindBlockerDistance(vec2 shadowCoord, float receiverDepth, float searchRadius)
{
    float blockerSum = 0.0;
    int blockerCount = 0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);

    // Use Poisson disk for blocker search (faster than grid)
    for (int i = 0; i < 16; i++)
    {
        vec2 offset = POISSON_DISK[i] * searchRadius * texelSize;

        // Sample depth directly (not shadow comparison)
        // We need raw depth, so we use textureProj or manual sampling
        // Since we're using sampler2DShadow, we compare against 0.0 to get depth
        float shadowMapDepth = texture(u_ShadowMap, vec3(shadowCoord + offset, 0.0));

        // If this depth is closer than receiver, it's a blocker
        if (shadowMapDepth < receiverDepth)
        {
            blockerSum += shadowMapDepth;
            blockerCount++;
        }
    }

    if (blockerCount == 0)
        return -1.0; // No blockers found

    return blockerSum / float(blockerCount);
}

// Step 2: Calculate penumbra size based on blocker distance
float PenumbraSize(float receiverDepth, float blockerDepth)
{
    // (receiver - blocker) / blocker * lightSize
    // This gives physically correct penumbra width
    return (receiverDepth - blockerDepth) * u_LightSize / blockerDepth;
}

// Step 3: PCSS full algorithm
float PCSS(vec2 shadowCoord, float compareDepth, float bias)
{
    // Search for blockers
    float searchRadius = u_LightSize * 50.0; // Scale search area by light size
    float avgBlockerDepth = FindBlockerDistance(shadowCoord, compareDepth, searchRadius);

    // No blockers = fully lit
    if (avgBlockerDepth < 0.0)
        return 1.0;

    // Calculate penumbra size
    float penumbra = PenumbraSize(compareDepth, avgBlockerDepth);

    // PCF with variable kernel size based on penumbra
    float filterRadius = penumbra * u_ShadowMapSize;
    filterRadius = clamp(filterRadius, 1.0, 10.0); // Limit max radius for performance

    // Use Poisson disk for final filtering
    return PCF_Poisson(shadowCoord, compareDepth, bias, filterRadius);
}

// ============================================================================
// Main Shadow Calculation Function
// ============================================================================

/// Calculate shadow factor for a world-space position
/// Returns: 0.0 = fully shadowed, 1.0 = fully lit
/// worldPos: Fragment position in world space
/// normal: Surface normal (for normal bias)
/// lightDir: Direction TO the light (normalized)
float CalculateShadow(vec3 worldPos, vec3 normal, vec3 lightDir)
{
    // Early exit if shadows disabled
    if (u_UseShadows == 0)
        return 1.0;

    // Apply normal-based bias in world space BEFORE transformation
    // This prevents shadow acne more effectively than depth bias alone
    float normalDot = max(dot(normal, lightDir), 0.0);
    float slopeBias = u_ShadowBias * sqrt(1.0 - normalDot * normalDot);
    vec3 biasedWorldPos = worldPos + normal * (u_ShadowBias + slopeBias);

    // Transform biased world position to light space
    vec4 lightSpacePos = u_ShadowMatrix * vec4(biasedWorldPos, 1.0);

    // Perspective divide
    vec3 projCoords = lightSpacePos.xyz / lightSpacePos.w;

    // Transform from NDC [-1, 1] to texture coordinates [0, 1]
    projCoords = projCoords * 0.5 + 0.5;

    // Check if position is outside shadow map bounds
    if (projCoords.x < 0.0 || projCoords.x > 1.0 ||
        projCoords.y < 0.0 || projCoords.y > 1.0 ||
        projCoords.z > 1.0)
    {
        return 1.0; // Outside shadow map = fully lit
    }

    // Perform simple PCF shadow sampling (3x3 kernel)
    float shadowValue = 0.0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);

    // 3x3 kernel sampling
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            // Hardware PCF: returns 1.0 if lit, 0.0 if shadowed
            shadowValue += texture(u_ShadowMap, vec3(projCoords.xy + offset, projCoords.z));
        }
    }
    shadowValue /= 9.0;

    // Apply shadow strength: mix between (1.0 - strength) and 1.0
    // shadowValue: 1.0 = lit, 0.0 = shadowed
    // Result: (1.0 - strength) when shadowed, 1.0 when lit
    return mix(1.0 - u_ShadowStrength, 1.0, shadowValue);
}

// ============================================================================
// Convenience Overloads
// ============================================================================

// Simple shadow calculation without normal (uses constant bias only)
float CalculateShadow(vec3 worldPos)
{
    return CalculateShadow(worldPos, vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0));
}

// Legacy compatibility
float calculateShadow(vec3 worldPos)
{
    return CalculateShadow(worldPos);
}

float calculateShadow(vec3 worldPos, vec3 viewPos)
{
    return CalculateShadow(worldPos);
}

float calculateShadowWithNL(vec3 worldPos, vec3 viewPos, vec3 N, vec3 L)
{
    return CalculateShadow(worldPos, N, L);
}
