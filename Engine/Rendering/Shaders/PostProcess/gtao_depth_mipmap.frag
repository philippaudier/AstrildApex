#version 330 core

// ============================================================================
// GTAO Depth Mipmap Generation
// Creates hierarchical depth pyramid for multi-scale ambient occlusion
// Uses min/max depth to preserve both occluders and receivers
// ============================================================================

out vec2 FragColor; // RG: min/max depth at this mip level

in vec2 vTexCoord;

uniform sampler2D u_SourceDepth;   // Previous mip level (or original depth for mip 0)
uniform int u_MipLevel;             // Current mip level being generated (0 = base)
uniform vec2 u_TexelSize;          // 1.0 / texture size at this mip level

// Sample 2x2 region from previous mip and compute min/max depth
// This preserves both thin occluders (min) and receivers (max)
vec2 computeMinMaxDepth(vec2 uv)
{
    // For mip 0: sample original depth as scalar, return as min/max pair
    // For mip 1+: sample previous mip's min/max depth
    if (u_MipLevel == 0)
    {
        // Base level: create min/max from single depth value
        float depth = texture(u_SourceDepth, uv).r;
        return vec2(depth, depth);
    }
    else
    {
        // Sample 2x2 region from previous mip (which stores min/max in RG)
        vec2 sample0 = texture(u_SourceDepth, uv + vec2(-0.25, -0.25) * u_TexelSize).rg;
        vec2 sample1 = texture(u_SourceDepth, uv + vec2( 0.25, -0.25) * u_TexelSize).rg;
        vec2 sample2 = texture(u_SourceDepth, uv + vec2(-0.25,  0.25) * u_TexelSize).rg;
        vec2 sample3 = texture(u_SourceDepth, uv + vec2( 0.25,  0.25) * u_TexelSize).rg;
        
        // Compute min of all min depths, max of all max depths
        float minDepth = min(min(sample0.r, sample1.r), min(sample2.r, sample3.r));
        float maxDepth = max(max(sample0.g, sample1.g), max(sample2.g, sample3.g));
        
        return vec2(minDepth, maxDepth);
    }
}

void main()
{
    vec2 minMaxDepth = computeMinMaxDepth(vTexCoord);
    
    // Skip skybox pixels (preserve them across mip chain)
    if (minMaxDepth.r >= 0.9999)
    {
        minMaxDepth = vec2(1.0, 1.0);
    }
    
    FragColor = minMaxDepth;
}
