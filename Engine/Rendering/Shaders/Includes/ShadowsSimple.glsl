// ============================================================================
// Simple Shadow Mapping - Robust and straightforward implementation
// ============================================================================
// This provides basic shadow mapping with PCF (Percentage Closer Filtering)
// for soft shadows. Focus on correctness and visual quality over complexity.
// ============================================================================

// Shadow uniforms
uniform sampler2DShadow u_ShadowMap;  // Hardware shadow comparison
uniform mat4 u_ShadowMatrix;          // World-space to light-space transformation
uniform int u_UseShadows;             // 0 = disabled, 1 = enabled

// Shadow parameters
uniform float u_ShadowMapSize;        // Shadow map resolution (e.g., 2048.0)
uniform float u_ShadowBias;           // Depth bias to prevent shadow acne
uniform float u_ShadowStrength;       // Shadow darkness (0.0 = no shadows, 1.0 = full shadows)
uniform float u_ShadowDistance;       // Maximum shadow distance from light

// ============================================================================
// Simple PCF with 3x3 kernel (9 samples)
// ============================================================================
float PCF_Simple(vec2 shadowCoord, float compareDepth)
{
    float shadow = 0.0;
    vec2 texelSize = 1.0 / vec2(u_ShadowMapSize);

    // 3x3 kernel sampling
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            // Hardware PCF: returns 1.0 if lit, 0.0 if shadowed
            shadow += texture(u_ShadowMap, vec3(shadowCoord + offset, compareDepth));
        }
    }

    return shadow / 9.0;
}

// ============================================================================
// Main Shadow Calculation Function
// ============================================================================
/// Calculate shadow factor for a world-space position
/// Returns: 0.0 = fully shadowed, 1.0 = fully lit
/// worldPos: Fragment position in world space
/// normal: Surface normal (for bias calculation)
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
        projCoords.z < 0.0 || projCoords.z > 1.0)
    {
        return 1.0; // Outside shadow map = fully lit
    }

    // Check distance-based shadow fadeout
    float distFromCenter = length(projCoords.xy - vec2(0.5));
    if (distFromCenter > 0.5)
    {
        return 1.0; // Outside shadow coverage
    }

    // Perform PCF shadow sampling
    float shadowValue = PCF_Simple(projCoords.xy, projCoords.z);

    // Apply shadow strength
    // shadowValue: 1.0 = lit, 0.0 = shadowed
    // We want: (1.0 - strength) when shadowed, 1.0 when lit
    return mix(1.0 - u_ShadowStrength, 1.0, shadowValue);
}

// ============================================================================
// Convenience Overloads
// ============================================================================

// Simple shadow calculation with default normal pointing up
float CalculateShadow(vec3 worldPos)
{
    return CalculateShadow(worldPos, vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0));
}
