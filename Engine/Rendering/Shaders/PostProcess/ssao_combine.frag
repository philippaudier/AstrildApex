#version 330 core

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_ColorTexture;
uniform sampler2D u_SSAOTexture; // R channel for AO (half-res, needs upscaling)
uniform sampler2D u_DepthTexture; // For edge-aware bilateral upscale
uniform float u_Intensity;
uniform mat4 u_InvProjection;

// Bilateral upscale from half-res to full-res with edge preservation
float bilateralUpsample(sampler2D aoTex, sampler2D depthTex, vec2 uv, vec2 fullResTexelSize)
{
    vec2 aoTexSize = vec2(textureSize(aoTex, 0));
    vec2 aoTexelSize = 1.0 / aoTexSize;

    // Center depth (full resolution)
    float centerDepth = texture(depthTex, uv).r;

    // Skip skybox
    if (centerDepth >= 0.9999)
    {
        return 1.0;
    }

    // Reconstruct view-space Z for depth comparison
    vec4 clipPos = vec4(uv * 2.0 - 1.0, centerDepth * 2.0 - 1.0, 1.0);
    vec4 viewPos = u_InvProjection * clipPos;
    float centerZ = viewPos.z / viewPos.w;

    // Sample 2x2 bilinear from half-res AO with depth weighting
    vec2 aoUV = uv; // Half-res UV maps directly to full-res UV
    vec2 aoPixel = aoUV * aoTexSize - 0.5;
    vec2 aoFrac = fract(aoPixel);
    vec2 aoBase = (floor(aoPixel) + 0.5) * aoTexelSize;

    // Sample 2x2 neighborhood
    float ao00 = texture(aoTex, aoBase).r;
    float ao10 = texture(aoTex, aoBase + vec2(aoTexelSize.x, 0.0)).r;
    float ao01 = texture(aoTex, aoBase + vec2(0.0, aoTexelSize.y)).r;
    float ao11 = texture(aoTex, aoBase + aoTexelSize).r;

    // Depth-based weights (edge-aware)
    vec2 depthUV00 = aoBase * 2.0; // Map back to full-res depth
    vec2 depthUV10 = (aoBase + vec2(aoTexelSize.x, 0.0)) * 2.0;
    vec2 depthUV01 = (aoBase + vec2(0.0, aoTexelSize.y)) * 2.0;
    vec2 depthUV11 = (aoBase + aoTexelSize) * 2.0;

    float depth00 = texture(depthTex, clamp(depthUV00, vec2(0.0), vec2(1.0))).r;
    float depth10 = texture(depthTex, clamp(depthUV10, vec2(0.0), vec2(1.0))).r;
    float depth01 = texture(depthTex, clamp(depthUV01, vec2(0.0), vec2(1.0))).r;
    float depth11 = texture(depthTex, clamp(depthUV11, vec2(0.0), vec2(1.0))).r;

    // Convert to view-space Z
    vec4 vp00 = u_InvProjection * vec4(depthUV00 * 2.0 - 1.0, depth00 * 2.0 - 1.0, 1.0);
    vec4 vp10 = u_InvProjection * vec4(depthUV10 * 2.0 - 1.0, depth10 * 2.0 - 1.0, 1.0);
    vec4 vp01 = u_InvProjection * vec4(depthUV01 * 2.0 - 1.0, depth01 * 2.0 - 1.0, 1.0);
    vec4 vp11 = u_InvProjection * vec4(depthUV11 * 2.0 - 1.0, depth11 * 2.0 - 1.0, 1.0);

    float z00 = vp00.z / vp00.w;
    float z10 = vp10.z / vp10.w;
    float z01 = vp01.z / vp01.w;
    float z11 = vp11.z / vp11.w;

    // Edge-aware weights (exponential falloff on depth difference)
    const float depthSensitivity = 10.0;
    float w00 = exp(-abs(z00 - centerZ) * depthSensitivity);
    float w10 = exp(-abs(z10 - centerZ) * depthSensitivity);
    float w01 = exp(-abs(z01 - centerZ) * depthSensitivity);
    float w11 = exp(-abs(z11 - centerZ) * depthSensitivity);

    // Bilinear interpolation weights
    float bw00 = (1.0 - aoFrac.x) * (1.0 - aoFrac.y);
    float bw10 = aoFrac.x * (1.0 - aoFrac.y);
    float bw01 = (1.0 - aoFrac.x) * aoFrac.y;
    float bw11 = aoFrac.x * aoFrac.y;

    // Combine bilinear + depth weights
    w00 *= bw00;
    w10 *= bw10;
    w01 *= bw01;
    w11 *= bw11;

    float totalWeight = w00 + w10 + w01 + w11;

    // Avoid division by zero
    if (totalWeight < 0.0001)
    {
        // Fallback to simple bilinear
        return mix(mix(ao00, ao10, aoFrac.x), mix(ao01, ao11, aoFrac.x), aoFrac.y);
    }

    // Weighted average
    return (ao00 * w00 + ao10 * w10 + ao01 * w01 + ao11 * w11) / totalWeight;
}

void main()
{
    vec3 color = texture(u_ColorTexture, vTexCoord).rgb;
    vec2 texelSize = 1.0 / vec2(textureSize(u_ColorTexture, 0));

    // Bilateral upscale from half-res AO to full-res with edge preservation
    float occlusion = bilateralUpsample(u_SSAOTexture, u_DepthTexture, vTexCoord, texelSize);

    // Apply occlusion with intensity
    // occlusion = 1.0 means no occlusion, 0.0 means full occlusion
    float factor = mix(1.0, occlusion, u_Intensity);
    color *= factor;

    FragColor = vec4(color, 1.0);
}

