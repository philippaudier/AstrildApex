#version 330 core

// ============================================================================
// SSAO Implementation - View-space approach (correct and stable)
// ============================================================================
// This implementation calculates ambient occlusion in view-space which provides
// consistent results that don't change based on camera orientation.
//
// Key principles:
// - All calculations in view-space (camera-relative but stable)
// - Hemisphere sampling (z > 0) oriented along view-space surface normal
// - Static noise texture with tiling to reduce banding
// - Range checks to prevent distant geometry from contributing
// - Proper depth comparison using view-space Z values
// ============================================================================

layout(location=0) out float outSSAO;

in vec2 vTexCoord;

// G-buffer inputs (view-space)
uniform sampler2D u_PositionTex;  // View-space positions (xyz)
uniform sampler2D u_NormalTex;    // View-space normals (xyz)
uniform sampler2D u_NoiseTex;     // 4x4 random rotation vectors (xy = rotation, z = 0)

// SSAO parameters
uniform float u_SSAORadius;       // Sampling radius in view-space units (default: 0.5)
uniform float u_SSAOBias;         // Depth bias to prevent acne (default: 0.025)
uniform float u_SSAOIntensity;    // Power exponent for contrast (default: 1.0)
uniform int u_SSAOSamples;        // Number of samples to use (max 128)
uniform vec2 u_ScreenSize;        // Screen dimensions for noise tiling

// Projection matrix for screen-space transformation
uniform mat4 u_ProjMatrix;

// Pre-generated hemisphere sample kernel (oriented along +Z axis)
// QUALITY: Increased from 64 to 128 for ultra quality option
uniform vec3 u_Samples[128];

const float EPSILON = 0.0001;

void main()
{
    // ========================================================================
    // Step 1: Sample G-buffer
    // ========================================================================

    vec3 fragPos = texture(u_PositionTex, vTexCoord).xyz;
    vec3 normal = normalize(texture(u_NormalTex, vTexCoord).xyz);

    // Early exit for sky/background (depth = far plane)
    if (fragPos.z >= 0.0 || length(fragPos) < EPSILON) {
        outSSAO = 1.0; // No occlusion for background
        return;
    }

    // ========================================================================
    // Step 2: Create TBN matrix to orient sample hemisphere
    // ========================================================================

    // Sample noise texture (4x4 tiled across screen)
    // Use gl_FragCoord for stable noise pattern that doesn't follow camera
    // This ensures the noise pattern is screen-space stable
    vec2 noiseCoord = gl_FragCoord.xy / 4.0;
    vec3 randomVec = normalize(texture(u_NoiseTex, noiseCoord).xyz);

    // Create tangent vector perpendicular to normal using Gram-Schmidt
    // randomVec provides rotation in the tangent plane
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);

    // TBN transforms from tangent-space (kernel space) to view-space
    mat3 TBN = mat3(tangent, bitangent, normal);

    // ========================================================================
    // Step 3: Sample kernel and accumulate occlusion
    // ========================================================================

    float occlusion = 0.0;

    for (int i = 0; i < u_SSAOSamples; ++i)
    {
        // Transform sample from tangent-space to view-space
        vec3 sampleVec = TBN * u_Samples[i];
        vec3 samplePos = fragPos + sampleVec * u_SSAORadius;

        // ====================================================================
        // Step 3a: Project sample to screen-space
        // ====================================================================

        vec4 offset = vec4(samplePos, 1.0);
        offset = u_ProjMatrix * offset;       // View-space to clip-space
        offset.xyz /= offset.w;                // Perspective divide (NDC)
        offset.xy = offset.xy * 0.5 + 0.5;    // NDC [-1,1] to texture coords [0,1]

        // Check if sample is outside screen bounds
        if (offset.x < 0.0 || offset.x > 1.0 || offset.y < 0.0 || offset.y > 1.0) {
            continue; // Skip out-of-bounds samples
        }

        // ====================================================================
        // Step 3b: Get actual depth at sample position
        // ====================================================================

        vec3 samplePosActual = texture(u_PositionTex, offset.xy).xyz;
        float sampleDepth = samplePosActual.z;

        // ====================================================================
        // Step 3c: Occlusion test with range check
        // ====================================================================

        // In OpenGL view-space, -Z is forward (into screen)
        // So more negative Z = farther from camera

        float depthDifference = samplePos.z - sampleDepth;

        // Range check: only count occlusion if sample is within radius
        float rangeCheck = smoothstep(0.0, 1.0, u_SSAORadius / (abs(depthDifference) + 0.001));

        // Occlusion test:
        // - sampleDepth < samplePos.z: geometry is closer than sample (occluding)
        // - depthDifference > u_SSAOBias: difference exceeds bias threshold
        // - depthDifference < u_SSAORadius: sample is within sampling radius

        float isOccluded = 0.0;
        if (sampleDepth < samplePos.z - u_SSAOBias && depthDifference < u_SSAORadius) {
            isOccluded = 1.0;
        }

        occlusion += isOccluded * rangeCheck;
    }

    // ========================================================================
    // Step 4: Normalize and output
    // ========================================================================

    // Normalize by sample count
    occlusion = occlusion / float(u_SSAOSamples);

    // Invert: 1.0 = no occlusion (bright), 0.0 = full occlusion (dark)
    occlusion = 1.0 - occlusion;

    // Apply intensity/power curve for artistic control
    // Higher values = more contrast, darker shadows
    outSSAO = pow(occlusion, u_SSAOIntensity);
}
