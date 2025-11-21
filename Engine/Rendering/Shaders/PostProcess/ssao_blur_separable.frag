#version 330 core

out float FragColor;

in vec2 vTexCoord;

uniform sampler2D u_SSAOTexture;
uniform sampler2D u_DepthTexture;
uniform int u_BlurSize;
uniform vec2 u_Direction; // (1,0) for horizontal, (0,1) for vertical
uniform mat4 u_InvProjection;

// Reconstruct view-space position from depth
vec3 reconstructViewPosition(vec2 uv, float depth)
{
    vec4 clipSpacePos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewSpacePos = u_InvProjection * clipSpacePos;
    return viewSpacePos.xyz / viewSpacePos.w;
}

void main()
{
    vec2 texSize = vec2(textureSize(u_SSAOTexture, 0));
    vec2 texelSize = 1.0 / texSize;

    float centerDepth = texture(u_DepthTexture, vTexCoord).r;

    // Skip skybox
    if (centerDepth >= 0.9999)
    {
        FragColor = 1.0;
        return;
    }

    vec3 centerPos = reconstructViewPosition(vTexCoord, centerDepth);
    float centerAO = texture(u_SSAOTexture, vTexCoord).r;

    // Bilateral blur: depth-aware + gaussian weights
    float totalWeight = 1.0;
    float totalAO = centerAO;

    int radius = u_BlurSize;

    for (int i = -radius; i <= radius; i++)
    {
        if (i == 0) continue;

        vec2 offset = u_Direction * float(i) * texelSize;
        vec2 sampleUV = vTexCoord + offset;

        // Skip if offscreen
        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
            continue;

        float sampleDepth = texture(u_DepthTexture, sampleUV).r;
        float sampleAO = texture(u_SSAOTexture, sampleUV).r;

        vec3 samplePos = reconstructViewPosition(sampleUV, sampleDepth);

        // Gaussian spatial weight (separable)
        float distSq = float(i * i);
        float sigma = float(radius) * 0.5;
        float spatialWeight = exp(-distSq / (2.0 * sigma * sigma));

        // Depth weight (edge-aware)
        float depthDiff = abs(centerPos.z - samplePos.z);
        float depthWeight = exp(-depthDiff * depthDiff * 10.0);

        // Combined weight
        float weight = spatialWeight * depthWeight;

        totalAO += sampleAO * weight;
        totalWeight += weight;
    }

    FragColor = totalAO / totalWeight;
}
