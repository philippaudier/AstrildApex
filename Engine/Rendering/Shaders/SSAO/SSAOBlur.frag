#version 330 core

// QUALITY IMPROVEMENT: Bilateral blur with edge preservation
// Preserves hard edges (geometry boundaries) while smoothing SSAO noise
// Modern AAA approach for clean SSAO without losing detail

layout(location=0) out float outBlurredSSAO;

in vec2 vTexCoord;

uniform sampler2D u_SSAOTex;      // Raw SSAO texture
uniform sampler2D u_DepthTex;     // Depth buffer for edge detection
uniform vec2 u_TexelSize;         // 1.0 / screen dimensions
uniform int u_BlurSize;           // Blur kernel radius in pixels

// Bilateral blur parameters
const float DEPTH_THRESHOLD = 0.01; // Depth difference threshold for edge detection
const float SIGMA_SPATIAL = 2.0;    // Spatial gaussian falloff
const float SIGMA_RANGE = 0.3;      // Range (intensity) gaussian falloff

void main()
{
    // If blur size is 0, just output the original SSAO value (no blur)
    if (u_BlurSize == 0)
    {
        outBlurredSSAO = texture(u_SSAOTex, vTexCoord).r;
        return;
    }

    vec2 texelSize = u_TexelSize;

    // Center pixel values
    float centerAO = texture(u_SSAOTex, vTexCoord).r;
    float centerDepth = texture(u_DepthTex, vTexCoord).r;

    float result = 0.0;
    float totalWeight = 0.0;

    // Bilateral blur: considers both spatial distance AND value similarity
    int radius = u_BlurSize;
    for (int x = -radius; x <= radius; ++x)
    {
        for (int y = -radius; y <= radius; ++y)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleCoord = vTexCoord + offset;

            // Sample SSAO and depth
            float sampleAO = texture(u_SSAOTex, sampleCoord).r;
            float sampleDepth = texture(u_DepthTex, sampleCoord).r;

            // === SPATIAL WEIGHT (Gaussian based on pixel distance) ===
            float spatialDist = length(vec2(x, y));
            float spatialWeight = exp(-(spatialDist * spatialDist) / (2.0 * SIGMA_SPATIAL * SIGMA_SPATIAL));

            // === RANGE WEIGHT (Gaussian based on AO value difference) ===
            float rangeDiff = abs(centerAO - sampleAO);
            float rangeWeight = exp(-(rangeDiff * rangeDiff) / (2.0 * SIGMA_RANGE * SIGMA_RANGE));

            // === DEPTH WEIGHT (Hard cutoff at edges) ===
            // If depth difference is too large, this is a geometry edge - don't blur across it
            float depthDiff = abs(centerDepth - sampleDepth);
            float depthWeight = (depthDiff < DEPTH_THRESHOLD) ? 1.0 : 0.0;

            // Combined weight: spatial × range × depth
            float weight = spatialWeight * rangeWeight * depthWeight;

            result += sampleAO * weight;
            totalWeight += weight;
        }
    }

    // Normalize result
    outBlurredSSAO = totalWeight > 0.0 ? result / totalWeight : centerAO;
}
