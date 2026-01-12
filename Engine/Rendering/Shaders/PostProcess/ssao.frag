#version 330 core

out float FragColor;

in vec2 vTexCoord;

// Textures
uniform sampler2D u_DepthTexture;

// SSAO Parameters
uniform float u_Radius;
uniform float u_Bias;
uniform float u_Power;
uniform float u_MaxDistance;
uniform int u_SampleCount;

// Sampling kernel
uniform vec3 u_Samples[64];

// Projection matrices
uniform mat4 u_Projection;
uniform mat4 u_InvProjection;

// PERFORMANCE: Screen size passed from CPU (avoids expensive textureSize() calls)
uniform vec2 u_ScreenSize;

// =============================================================================
// NOISE FUNCTION - Interleaved Gradient Noise (IGN)
// =============================================================================

// Interleaved gradient noise - procedural noise function
float interleavedGradientNoise(vec2 uv)
{
    vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(magic.z * fract(dot(uv, magic.xy)));
}

void main()
{
    // PERFORMANCE: Use uniform instead of textureSize() (~20 GPU cycles saved)

    // Sample depth
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // Skip skybox
    if (depth >= 0.9999)
    {
        FragColor = 1.0;
        return;
    }

    // Reconstruct position in view space
    vec4 clipSpacePos = vec4(vTexCoord * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewSpacePos = u_InvProjection * clipSpacePos;
    vec3 position = viewSpacePos.xyz / viewSpacePos.w;

    // Calculate approximate normal from depth gradients
    vec2 texelSize = 1.0 / u_ScreenSize;

    // Sample neighbors for normal calculation
    vec2 coordRight = clamp(vTexCoord + vec2(texelSize.x, 0.0), vec2(0.0), vec2(1.0));
    vec2 coordTop = clamp(vTexCoord + vec2(0.0, texelSize.y), vec2(0.0), vec2(1.0));

    float depthRight = texture(u_DepthTexture, coordRight).r;
    float depthTop = texture(u_DepthTexture, coordTop).r;

    vec4 posRightClip = vec4(coordRight * 2.0 - 1.0, depthRight * 2.0 - 1.0, 1.0);
    vec4 posRightView = u_InvProjection * posRightClip;
    vec3 posRight = posRightView.xyz / posRightView.w;

    vec4 posTopClip = vec4(coordTop * 2.0 - 1.0, depthTop * 2.0 - 1.0, 1.0);
    vec4 posTopView = u_InvProjection * posTopClip;
    vec3 posTop = posTopView.xyz / posTopView.w;

    vec3 tangentX = posRight - position;
    vec3 tangentY = posTop - position;
    vec3 normal = normalize(cross(tangentX, tangentY));

    // Get rotation using procedural noise
    vec2 pixelCoord = vTexCoord * u_ScreenSize;
    float noise = interleavedGradientNoise(pixelCoord);

    // Convert noise [0,1] to rotation angle [0, 2π]
    float rotationAngle = noise * 6.28318530718; // 2 * PI

    // Create rotation in tangent plane (more efficient than full 3D rotation)
    float cosAngle = cos(rotationAngle);
    float sinAngle = sin(rotationAngle);

    // Create TBN basis (Tangent, Bitangent, Normal) with rotation
    vec3 tangent = vec3(1.0, 0.0, 0.0);
    vec3 bitangent = vec3(0.0, 1.0, 0.0);

    // Gram-Schmidt to make tangent perpendicular to normal
    tangent = normalize(tangent - normal * dot(tangent, normal));
    bitangent = cross(normal, tangent);

    // Apply rotation in tangent space
    vec3 rotatedTangent = tangent * cosAngle + bitangent * sinAngle;
    vec3 rotatedBitangent = -tangent * sinAngle + bitangent * cosAngle;

    mat3 TBN = mat3(rotatedTangent, rotatedBitangent, normal);

    // Accumulate occlusion
    float occlusion = 0.0;

    for (int i = 0; i < u_SampleCount; ++i)
    {
        // Transform sample to tangent space
        vec3 samplePos = TBN * u_Samples[i];
        samplePos = position + samplePos * u_Radius;

        // Project sample to screen space
        vec4 offset = u_Projection * vec4(samplePos, 1.0);
        offset.xyz = offset.xyz / offset.w;
        offset.xyz = offset.xyz * 0.5 + 0.5;

        // Clamp to valid texture coordinates to avoid edge artifacts
        if (offset.x < 0.0 || offset.x > 1.0 || offset.y < 0.0 || offset.y > 1.0)
        {
            continue; // Skip samples outside screen bounds
        }

        // Sample depth at this position
        float sampleDepth = texture(u_DepthTexture, offset.xy).r;

        vec4 samplePosClip = vec4(offset.xy * 2.0 - 1.0, sampleDepth * 2.0 - 1.0, 1.0);
        vec4 samplePosView = u_InvProjection * samplePosClip;
        vec3 samplePosition = samplePosView.xyz / samplePosView.w;

        // IMPROVED: Depth test with better range check (avoid over-darkening at edges)
        float depthDiff = samplePosition.z - samplePos.z;

        // Range check: fade out contribution for samples far from surface
        float rangeCheck = smoothstep(0.0, 1.0, u_Radius / (abs(depthDiff) + 0.001));

        // Occlusion test: sample is occluder if it's in front (closer to camera)
        // Use smooth falloff instead of hard threshold for better quality
        float occluded = step(u_Bias, -depthDiff);

        // Alchemy AO improvement: weight by distance (closer samples = more influence)
        float distanceWeight = 1.0 - smoothstep(u_Radius * 0.5, u_Radius, length(samplePos - position));

        occlusion += occluded * rangeCheck * distanceWeight;
    }

    // Normalize and invert (1.0 = no occlusion, 0.0 = full occlusion)
    occlusion = 1.0 - (occlusion / float(u_SampleCount));

    // Apply power to adjust contrast (non-linear response)
    occlusion = pow(clamp(occlusion, 0.0, 1.0), u_Power);

    // Distance fade: gradually fade out SSAO based on distance from camera
    float distanceFade = 1.0 - smoothstep(u_MaxDistance * 0.7, u_MaxDistance, abs(position.z));
    occlusion = mix(1.0, occlusion, distanceFade);

    // Add dithering to break quantization banding
    float dither = (noise - 0.5) / 255.0; // Scale for 8-bit output
    occlusion = clamp(occlusion + dither, 0.0, 1.0);

    FragColor = occlusion;
}
