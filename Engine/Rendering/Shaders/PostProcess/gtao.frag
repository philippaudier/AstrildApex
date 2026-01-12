#version 330 core

out vec4 FragColor; // RGB: Bent Normal (view space), A: Occlusion

in vec2 vTexCoord;

// Textures
uniform sampler2D u_DepthTexture;       // Base depth (full resolution)
uniform sampler2D u_DepthMipmap;        // Hierarchical depth pyramid (RG: min/max depth per mip)

// GTAO Parameters
uniform float u_Radius;          // Sampling radius in view units
uniform float u_Thickness;       // Surface thickness
uniform float u_FalloffRange;    // Falloff range
uniform float u_MaxDistance;     // Max distance for fade out
uniform int u_SampleCount;       // Number of samples per slice (2-6)
uniform int u_SliceCount;        // Number of slices/directions (1-4)
uniform int u_FrameCounter;      // Frame counter for temporal variation

// Multi-Scale Parameters
uniform int u_MipLevels;         // Number of mip levels to sample (1-4, default 1 = no multi-scale)
uniform float u_MipWeights[4];   // Weight for each mip level (should sum to 1.0)
uniform float u_MipRadii[4];     // Radius multiplier for each mip level

// Projection matrices
uniform mat4 u_Projection;
uniform mat4 u_InvProjection;

// PERFORMANCE: Screen size passed from CPU (avoids expensive textureSize() calls)
uniform vec2 u_ScreenSize;

// Constants
const float PI = 3.14159265359;
const float HALF_PI = 1.57079632679;

// =============================================================================
// MODERN NOISE FUNCTIONS - Based on XeGTAO (Intel's optimized GTAO)
// Reference: https://github.com/GameTechDev/XeGTAO
// =============================================================================

// Interleaved gradient noise (better than hash-based noise)
// From: http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare
float interleavedGradientNoise(vec2 uv)
{
    vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(magic.z * fract(dot(uv, magic.xy)));
}

// R2 sequence (quasi-random, low-discrepancy)
// Better distribution than pseudo-random, reduces banding
vec2 R2sequence(int n)
{
    const float g = 1.61803398875; // Golden ratio
    const float a1 = 1.0 / g;
    const float a2 = 1.0 / (g * g);
    return fract(vec2(n * a1, n * a2));
}

// Robust spatiotemporal noise - compatible with all GLSL versions
// Combines improved spatial hash with temporal variation
vec2 spatialTemporalNoise(vec2 pixelPos, int frameCounter)
{
    // Use 64x64 tiling for repeating pattern
    vec2 tilePos = mod(pixelPos, 64.0);

    // Create spatial index using improved 2D hash (better than simple linear)
    // This spreads neighboring pixels across the sequence space
    float spatialHash = interleavedGradientNoise(tilePos);
    int spatialIdx = int(spatialHash * 4096.0); // Map to integer range

    // Add temporal offset (288 is XeGTAO standard for good distribution)
    int temporalOffset = 288 * (frameCounter % 256); // Prevent overflow
    int spatioTemporalIdx = spatialIdx + temporalOffset;

    // R2 low-discrepancy sequence for final sample positions
    return R2sequence(spatioTemporalIdx);
}

// Blue noise dithering - superior to triangle dither for smooth gradients
// Breaks both spatial AND temporal banding
float blueNoiseDither(vec2 pixelPos, int frameCounter)
{
    // Temporal variation using golden ratio offset (prevents temporal patterns)
    const float goldenRatio = 0.61803398875; // 1/φ
    float temporalPhase = fract(float(frameCounter) * goldenRatio);

    // Combine spatial IGN with temporal offset
    // Use small offset to avoid repeating patterns
    vec2 temporalOffset = vec2(temporalPhase * 17.0, temporalPhase * 23.0);
    float spatial = interleavedGradientNoise(pixelPos + temporalOffset);

    // Triangle-shaped PDF (reduces quantization artifacts better than uniform)
    float triangular = spatial + interleavedGradientNoise(pixelPos + vec2(0.5)) - 1.0;

    return triangular * 0.5; // Center around 0
}

// Fast approximation of atan2 (with safety checks)
float fast_atan2(float y, float x)
{
    // Handle edge case: both zero
    if (abs(x) < 0.0001 && abs(y) < 0.0001)
        return 0.0;

    float ax = abs(x);
    float ay = abs(y);
    float maxVal = max(ax, ay);

    // Avoid division by zero
    if (maxVal < 0.0001)
        return 0.0;

    float a = min(ax, ay) / maxVal;
    float s = a * a;
    float r = ((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a + a;
    if (ay > ax) r = HALF_PI - r;
    if (x < 0.0) r = PI - r;
    if (y < 0.0) r = -r;
    return r;
}

// Reconstructs view space position from depth
vec3 reconstructViewPosition(vec2 uv, float depth)
{
    vec4 clipSpacePos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewSpacePos = u_InvProjection * clipSpacePos;
    return viewSpacePos.xyz / viewSpacePos.w;
}

// Computes approximate normal from depth gradients
vec3 computeNormal(vec2 uv, vec3 position)
{
    // PERFORMANCE: Use uniform instead of textureSize() (~20 GPU cycles saved)
    vec2 texelSize = 1.0 / u_ScreenSize;

    // Sample neighbors
    vec2 coordRight = uv + vec2(texelSize.x, 0.0);
    vec2 coordTop = uv + vec2(0.0, texelSize.y);
    
    float depthRight = texture(u_DepthTexture, coordRight).r;
    float depthTop = texture(u_DepthTexture, coordTop).r;
    
    vec3 posRight = reconstructViewPosition(coordRight, depthRight);
    vec3 posTop = reconstructViewPosition(coordTop, depthTop);
    
    vec3 tangentX = posRight - position;
    vec3 tangentY = posTop - position;
    
    return normalize(cross(tangentX, tangentY));
}

// Integrate occlusion for a given direction and accumulate bent normal
// mipLevel: which mip level of depth pyramid to sample (0 = full res, 1+ = downsampled)
// radiusScale: multiplier for sampling radius at this mip level
void integrateArc(vec3 viewPos, vec3 viewDir, vec3 viewNormal, vec2 uv, float sliceAngle,
                  int mipLevel, float radiusScale, inout float totalOcclusion, inout vec3 bentNormal)
{
    // PERFORMANCE: Use uniform instead of textureSize()

    // Slice direction in tangent plane
    vec2 sliceDir = vec2(cos(sliceAngle), sin(sliceAngle));

    float horizonAngle1 = -HALF_PI;
    float horizonAngle2 = -HALF_PI;

    // Radius in pixels (scaled by mip level)
    float effectiveRadius = u_Radius * radiusScale;
    float radiusPixels = effectiveRadius * u_Projection[0][0] / -viewPos.z * u_ScreenSize.x * 0.5;
    float stepSize = radiusPixels / float(u_SampleCount);

    // Sampling in both directions
    for (int side = 0; side < 2; side++)
    {
        float direction = side == 0 ? 1.0 : -1.0;
        float horizonAngle = side == 0 ? horizonAngle1 : horizonAngle2;
        
        for (int step = 1; step <= u_SampleCount; step++)
        {
            vec2 offset = sliceDir * direction * stepSize * float(step) / u_ScreenSize;
            vec2 sampleUV = uv + offset;

            // Skip if offscreen
            if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
                continue;

            // Sample depth from appropriate source
            float sampleDepth;
            if (mipLevel == 0)
            {
                // Full resolution depth
                sampleDepth = texture(u_DepthTexture, sampleUV).r;
            }
            else
            {
                // Sample min/max depth from mipmap (average for better quality)
                vec2 minMaxDepth = textureLod(u_DepthMipmap, sampleUV, float(mipLevel - 1)).rg;
                sampleDepth = (minMaxDepth.r + minMaxDepth.g) * 0.5;
            }
            
            vec3 samplePos = reconstructViewPosition(sampleUV, sampleDepth);

            vec3 horizonDir = samplePos - viewPos;
            float horizonDist = length(horizonDir);
            horizonDir = normalize(horizonDir);

            // Calculate horizon angle
            float angle = fast_atan2(-horizonDir.z, length(horizonDir.xy));

            // Thickness heuristic: reduce occlusion for samples that are too close (thin surfaces)
            float thicknessFactor = smoothstep(0.0, u_Thickness, horizonDist);

            // Distance-based falloff (adjusted for mip scale)
            float weight = 1.0 - smoothstep(effectiveRadius * (1.0 - u_FalloffRange), effectiveRadius, horizonDist);
            weight *= thicknessFactor; // Apply thickness attenuation

            // Update horizon angle with weight
            angle = mix(horizonAngle, angle, weight);
            horizonAngle = max(horizonAngle, angle);
        }
        
        if (side == 0)
            horizonAngle1 = horizonAngle;
        else
            horizonAngle2 = horizonAngle;
    }

    // Calculate normal angle projected in the slice plane
    vec3 planeNormal = vec3(sliceDir.x, sliceDir.y, 0.0);
    vec3 tangent = normalize(cross(vec3(0.0, 0.0, 1.0), planeNormal));
    vec3 projNormal = viewNormal - dot(viewNormal, tangent) * tangent;
    projNormal = normalize(projNormal);

    float normalAngle = fast_atan2(-projNormal.z, length(projNormal.xy));

    // Calculate occlusion with arc integration
    float h1 = normalAngle + max(-horizonAngle1 - normalAngle, -HALF_PI);
    float h2 = normalAngle + min(horizonAngle2 - normalAngle, HALF_PI);
    
    float sinNormal = sin(normalAngle);
    float occlusion = -cos(2.0 * h1 - normalAngle) + cos(normalAngle);
    occlusion += -cos(2.0 * h2 - normalAngle) + cos(normalAngle);
    occlusion *= 0.25;
    
    totalOcclusion += clamp(occlusion, 0.0, 1.0);
    
    // Calculate bent normal (average unoccluded direction)
    // Use the bisector of the unoccluded cone
    float avgHorizon = (horizonAngle1 + horizonAngle2) * 0.5;
    float bentAngle = (normalAngle + avgHorizon) * 0.5;
    
    // Convert bent angle to 3D direction in slice plane
    vec3 sliceDir3D = vec3(sliceDir.x, sliceDir.y, 0.0);
    vec3 bentDir = cos(bentAngle) * normalize(sliceDir3D) + sin(bentAngle) * vec3(0.0, 0.0, -1.0);
    
    // Weight by visibility (less occlusion = more contribution)
    float visibility = 1.0 - clamp(occlusion, 0.0, 1.0);
    bentNormal += bentDir * visibility;
}

void main()
{
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // Skip skybox
    if (depth >= 0.9999)
    {
        FragColor = vec4(0.5, 0.5, 0.5, 1.0); // Encode default normal + no occlusion
        return;
    }

    // Reconstruct position and normal
    vec3 viewPos = reconstructViewPosition(vTexCoord, depth);
    vec3 viewNormal = computeNormal(vTexCoord, viewPos);
    vec3 viewDir = normalize(viewPos);

    // Fade out with distance
    float distanceFade = 1.0 - smoothstep(u_MaxDistance * 0.8, u_MaxDistance, -viewPos.z);
    if (distanceFade <= 0.01)
    {
        FragColor = vec4(viewNormal * 0.5 + 0.5, 1.0); // Encode normal in [0,1] range
        return;
    }

    // Integrate occlusion over multiple slices
    float totalOcclusion = 0.0;
    vec3 bentNormal = vec3(0.0);
    float angleStep = PI / float(u_SliceCount);

    // CRITICAL FIX: Modern XeGTAO-style spatial-temporal noise
    // Uses Hilbert curve + R2 for superior low-discrepancy sampling
    // PERFORMANCE: Use uniform instead of textureSize()
    vec2 pixelPos = vTexCoord * u_ScreenSize;

    // Get spatio-temporal noise from Hilbert curve + R2 sequence
    vec2 noise = spatialTemporalNoise(pixelPos, u_FrameCounter);

    // Apply noise to slice rotation
    float sliceAngleOffset = noise.x * angleStep;

    // Optional: Add slight per-sample jitter (reduces pattern artifacts)
    float sampleJitter = noise.y * 0.5; // Used for sample step variation

    // Multi-scale GTAO: sample multiple mip levels with different radii
    for (int mipLevel = 0; mipLevel < u_MipLevels; mipLevel++)
    {
        float mipWeight = u_MipWeights[mipLevel];
        float radiusScale = u_MipRadii[mipLevel];
        
        if (mipWeight <= 0.001) continue; // Skip disabled mip levels
        
        float mipOcclusion = 0.0;
        vec3 mipBentNormal = vec3(0.0);
        
        for (int i = 0; i < u_SliceCount; i++)
        {
            float sliceAngle = float(i) * angleStep + sliceAngleOffset;
            integrateArc(viewPos, viewDir, viewNormal, vTexCoord, sliceAngle,
                        mipLevel, radiusScale, mipOcclusion, mipBentNormal);
        }
        
        // Average and weight this mip level's contribution
        mipOcclusion /= float(u_SliceCount);
        
        totalOcclusion += mipOcclusion * mipWeight;
        bentNormal += mipBentNormal * mipWeight;
    }

    // Convert occlusion (higher = more occluded) to accessibility (higher = less occluded)
    totalOcclusion = 1.0 - totalOcclusion;

    // Apply distance fade to occlusion
    totalOcclusion = mix(1.0, totalOcclusion, distanceFade);

    // CRITICAL: Apply blue noise dithering to break quantization banding
    // This is ESSENTIAL for smooth gradients in 8/16-bit buffers
    // XeGTAO recommendation: stronger dither for R16F textures
    float dither = blueNoiseDither(pixelPos, u_FrameCounter);

    // Scale dithering based on buffer format
    // R16F has ~10 bits effective precision, so we need ~1/1024 dither strength
    float ditherScale = 1.0 / 512.0; // Optimized for R16F (half precision float)
    totalOcclusion = clamp(totalOcclusion + dither * ditherScale, 0.0, 1.0);
    
    // Normalize bent normal (average unoccluded direction)
    float bentNormalLength = length(bentNormal);
    if (bentNormalLength > 0.001)
    {
        bentNormal = bentNormal / bentNormalLength;
    }
    else
    {
        // Fully occluded or invalid, use surface normal
        bentNormal = viewNormal;
    }

    // Safety: Clamp occlusion and check for invalid values
    totalOcclusion = clamp(totalOcclusion, 0.0, 1.0);

    // Final safety check for NaN/Inf (shouldn't happen but protects against GPU errors)
    if (isnan(totalOcclusion) || isinf(totalOcclusion))
        totalOcclusion = 1.0; // No occlusion if invalid

    if (any(isnan(bentNormal)) || any(isinf(bentNormal)))
        bentNormal = vec3(0.0, 0.0, 1.0); // Default up vector if invalid

    // Output: RGB = Bent Normal (view space), A = Occlusion
    FragColor = vec4(bentNormal * 0.5 + 0.5, totalOcclusion); // Encode normal in [0,1] range
}
