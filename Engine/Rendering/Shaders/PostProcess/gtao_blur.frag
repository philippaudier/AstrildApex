#version 330 core

out float FragColor;

in vec2 vTexCoord;

// Textures
uniform sampler2D u_GTAOTexture;
uniform sampler2D u_DepthTexture;

// Parameters
uniform int u_BlurRadius;
uniform mat4 u_InvProjection;

// Reconstructs view space position from depth
vec3 reconstructViewPosition(vec2 uv, float depth)
{
    vec4 clipSpacePos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewSpacePos = u_InvProjection * clipSpacePos;
    return viewSpacePos.xyz / viewSpacePos.w;
}

void main()
{
    vec2 texSize = vec2(textureSize(u_GTAOTexture, 0));
    vec2 texelSize = 1.0 / texSize;
    
    float centerDepth = texture(u_DepthTexture, vTexCoord).r;
    vec3 centerPos = reconstructViewPosition(vTexCoord, centerDepth);
    
    // Skip skybox
    if (centerDepth >= 0.9999)
    {
        FragColor = 1.0;
        return;
    }
    
    float centerAO = texture(u_GTAOTexture, vTexCoord).a; // AO is in alpha channel

    // Edge-aware bilateral blur
    float totalWeight = 1.0;
    float totalAO = centerAO;

    // Blur kernel with gaussian weights
    int radius = u_BlurRadius;
    
    for (int y = -radius; y <= radius; y++)
    {
        for (int x = -radius; x <= radius; x++)
        {
            if (x == 0 && y == 0) continue;
            
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleUV = vTexCoord + offset;

            // Skip if offscreen
            if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
                continue;

            float sampleDepth = texture(u_DepthTexture, sampleUV).r;
            float sampleAO = texture(u_GTAOTexture, sampleUV).a; // AO is in alpha channel

            vec3 samplePos = reconstructViewPosition(sampleUV, sampleDepth);

            // Spatial weight (gaussian)
            float distanceSq = float(x * x + y * y);
            float spatialWeight = exp(-distanceSq / (2.0 * float(radius * radius)));

            // Depth weight (edge-aware)
            float depthDiff = abs(centerPos.z - samplePos.z);
            float depthWeight = exp(-depthDiff * depthDiff * 10.0);

            // Combined weight
            float weight = spatialWeight * depthWeight;
            
            totalAO += sampleAO * weight;
            totalWeight += weight;
        }
    }
    
    FragColor = totalAO / totalWeight;
}
