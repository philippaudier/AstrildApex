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

// Constants
const float PI = 3.14159265359;
const float HALF_PI = 1.57079632679;

// Spatial noise for stable temporal variation
float spatialNoise(vec2 coord)
{
    return fract(sin(dot(coord, vec2(12.9898, 78.233))) * 43758.5453);
}

// Fast approximation of atan2
float fast_atan2(float y, float x)
{
    float ax = abs(x);
    float ay = abs(y);
    float a = min(ax, ay) / max(ax, ay);
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
    vec2 texSize = vec2(textureSize(u_DepthTexture, 0));
    vec2 texelSize = 1.0 / texSize;

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
    vec2 texSize = vec2(textureSize(u_DepthTexture, 0));

    // Slice direction in tangent plane
    vec2 sliceDir = vec2(cos(sliceAngle), sin(sliceAngle));

    float horizonAngle1 = -HALF_PI;
    float horizonAngle2 = -HALF_PI;

    // Radius in pixels (scaled by mip level)
    float effectiveRadius = u_Radius * radiusScale;
    float radiusPixels = effectiveRadius * u_Projection[0][0] / -viewPos.z * texSize.x * 0.5;
    float stepSize = radiusPixels / float(u_SampleCount);

    // Sampling in both directions
    for (int side = 0; side < 2; side++)
    {
        float direction = side == 0 ? 1.0 : -1.0;
        float horizonAngle = side == 0 ? horizonAngle1 : horizonAngle2;
        
        for (int step = 1; step <= u_SampleCount; step++)
        {
            vec2 offset = sliceDir * direction * stepSize * float(step) / texSize;
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
        FragColor = vec4(0.0, 0.0, 1.0, 1.0); // Normal pointing up, no occlusion
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
        FragColor = vec4(viewNormal, 1.0);
        return;
    }

    // Integrate occlusion over multiple slices
    float totalOcclusion = 0.0;
    vec3 bentNormal = vec3(0.0);
    float angleStep = PI / float(u_SliceCount);

    // Spatial variation to reduce banding (stable, doesn't animate)
    vec2 noiseCoord = vTexCoord * vec2(textureSize(u_DepthTexture, 0));
    float spatialOffset = spatialNoise(noiseCoord) * angleStep;

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
            float sliceAngle = float(i) * angleStep + spatialOffset;
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
    
    // Normalize bent normal (average unoccluded direction)
    if (length(bentNormal) > 0.001)
    {
        bentNormal = normalize(bentNormal);
    }
    else
    {
        // Fully occluded, use surface normal
        bentNormal = viewNormal;
    }
    
    // Output: RGB = Bent Normal (view space), A = Occlusion
    FragColor = vec4(bentNormal * 0.5 + 0.5, totalOcclusion); // Encode normal in [0,1] range
}
