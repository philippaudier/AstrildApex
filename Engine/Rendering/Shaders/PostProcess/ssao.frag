#version 330 core

out float FragColor;

in vec2 vTexCoord;

// Textures
uniform sampler2D u_DepthTexture;
uniform sampler2D u_NoiseTexture;

// SSAO Parameters
uniform float u_Radius;
uniform float u_Bias;
uniform float u_Power;
uniform float u_MaxDistance;
uniform int u_SampleCount;
uniform vec2 u_NoiseScale;

// Sampling kernel
uniform vec3 u_Samples[64];

// Projection matrices
uniform mat4 u_Projection;
uniform mat4 u_InvProjection;

void main()
{
    vec2 texSize = vec2(textureSize(u_DepthTexture, 0));

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
    vec2 texelSize = 1.0 / texSize;

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

    // Sample noise vector for rotation
    vec3 randomVec = normalize(texture(u_NoiseTexture, vTexCoord * u_NoiseScale).xyz * 2.0 - 1.0);

    // Create TBN basis (Tangent, Bitangent, Normal)
    vec3 tangent = normalize(randomVec - normal * dot(randomVec, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

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

        // Depth test with range check
        float rangeCheck = smoothstep(0.0, 1.0, u_Radius / abs(position.z - samplePosition.z));

        // If sample is in front of geometry, there is occlusion
        float occluded = (samplePosition.z <= samplePos.z - u_Bias) ? 1.0 : 0.0;
        occlusion += occluded * rangeCheck;
    }

    // Normalize and invert (1.0 = no occlusion, 0.0 = full occlusion)
    occlusion = 1.0 - (occlusion / float(u_SampleCount));

    // Apply power to adjust contrast
    occlusion = pow(occlusion, u_Power);

    // Distance fade: gradually fade out SSAO based on distance from camera
    float distanceFade = 1.0 - smoothstep(u_MaxDistance * 0.7, u_MaxDistance, abs(position.z));
    occlusion = mix(1.0, occlusion, distanceFade);

    FragColor = occlusion;
}
