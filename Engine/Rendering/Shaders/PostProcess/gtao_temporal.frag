#version 330 core

out vec4 FragColor; // RGB: Bent Normal, A: Occlusion

in vec2 vTexCoord;

// Textures (RGBA: bent normal + AO)
uniform sampler2D u_CurrentGTAO;     // Current frame GTAO + bent normals
uniform sampler2D u_HistoryGTAO;     // Previous frame GTAO + bent normals
uniform sampler2D u_CurrentDepth;    // Current frame depth
uniform sampler2D u_HistoryDepth;    // Previous frame depth

// Temporal parameters
uniform float u_BlendFactor;         // History blend weight (0.8-0.95)
uniform float u_VarianceThreshold;   // Rejection threshold (0.05-0.3)

// Matrices for reprojection
uniform mat4 u_CurrentInvProjection;
uniform mat4 u_CurrentInvView;
uniform mat4 u_PrevViewProjection;

// Reconstruct world position from depth
vec3 reconstructWorldPosition(vec2 uv, float depth, mat4 invProj, mat4 invView)
{
    vec4 clipSpacePos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewSpacePos = invProj * clipSpacePos;
    viewSpacePos.xyz /= viewSpacePos.w;
    
    vec4 worldSpacePos = invView * viewSpacePos;
    return worldSpacePos.xyz;
}

// Project world position to screen space
vec2 projectToScreen(vec3 worldPos, mat4 viewProj)
{
    vec4 clipPos = viewProj * vec4(worldPos, 1.0);
    clipPos.xyz /= clipPos.w;
    return clipPos.xy * 0.5 + 0.5;
}

// Sample neighborhood with catmull-rom filter for better quality
vec4 sampleCatmullRom(sampler2D tex, vec2 uv, vec2 texSize)
{
    vec2 position = uv * texSize;
    vec2 centerPosition = floor(position - 0.5) + 0.5;
    vec2 f = position - centerPosition;
    vec2 f2 = f * f;
    vec2 f3 = f2 * f;
    
    vec2 w0 = f2 - 0.5 * (f3 + f);
    vec2 w1 = 1.5 * f3 - 2.5 * f2 + 1.0;
    vec2 w3 = 0.5 * (f3 - f2);
    vec2 w2 = 1.0 - w0 - w1 - w3;
    
    vec2 s0 = w0 + w1;
    vec2 s1 = w2 + w3;
    vec2 f0 = w1 / (w0 + w1);
    vec2 f1 = w3 / (w2 + w3);
    
    vec2 t0 = centerPosition - 1.0 + f0;
    vec2 t1 = centerPosition + 1.0 + f1;
    
    vec2 texelSize = 1.0 / texSize;
    
    vec4 result = 
        texture(tex, t0 * texelSize) * s0.x * s0.y +
        texture(tex, vec2(t1.x, t0.y) * texelSize) * s1.x * s0.y +
        texture(tex, vec2(t0.x, t1.y) * texelSize) * s0.x * s1.y +
        texture(tex, t1 * texelSize) * s1.x * s1.y;
    
    return result;
}

// Compute variance in 3x3 neighborhood (for AO channel only)
float computeVariance(sampler2D tex, vec2 uv, vec2 texelSize, out float minVal, out float maxVal)
{
    float sum = 0.0;
    float sumSq = 0.0;
    minVal = 1.0;
    maxVal = 0.0;
    
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            float sample = texture(tex, uv + offset).a; // AO is in alpha channel
            
            sum += sample;
            sumSq += sample * sample;
            minVal = min(minVal, sample);
            maxVal = max(maxVal, sample);
        }
    }
    
    float mean = sum / 9.0;
    float variance = (sumSq / 9.0) - (mean * mean);
    return sqrt(max(variance, 0.0));
}

void main()
{
    vec2 texSize = vec2(textureSize(u_CurrentGTAO, 0));
    vec2 texelSize = 1.0 / texSize;
    
    float currentDepth = texture(u_CurrentDepth, vTexCoord).r;
    
    // Skip skybox
    if (currentDepth >= 0.9999)
    {
        FragColor = vec4(0.0, 0.0, 1.0, 1.0); // Normal pointing up, no occlusion
        return;
    }
    
    vec4 currentData = texture(u_CurrentGTAO, vTexCoord); // RGBA: bent normal + AO
    vec3 currentBentNormal = currentData.rgb * 2.0 - 1.0; // Decode from [0,1] to [-1,1]
    float currentAO = currentData.a;
    
    // Reconstruct current world position
    vec3 worldPos = reconstructWorldPosition(vTexCoord, currentDepth, 
                                             u_CurrentInvProjection, u_CurrentInvView);
    
    // Check if world position is valid
    if (any(isnan(worldPos)) || any(isinf(worldPos)))
    {
        // Invalid matrices - fallback to simple temporal without reprojection
        vec4 historyData = texture(u_HistoryGTAO, vTexCoord); // No motion, sample same position
        float blendFactor = u_BlendFactor * 0.9; // Still blend for noise reduction
        
        float resultAO = mix(currentAO, historyData.a, blendFactor);
        vec3 historyBent = historyData.rgb * 2.0 - 1.0;
        vec3 resultBent = normalize(mix(currentBentNormal, historyBent, blendFactor * 0.8));
        
        FragColor = vec4(resultBent * 0.5 + 0.5, resultAO);
        return;
    }
    
    // Reproject to previous frame
    vec2 prevUV = projectToScreen(worldPos, u_PrevViewProjection);
    
    // Check if reprojection is valid (on screen)
    if (prevUV.x < 0.0 || prevUV.x > 1.0 || prevUV.y < 0.0 || prevUV.y > 1.0 ||
        any(isnan(prevUV)) || any(isinf(prevUV)))
    {
        // Out of bounds or invalid - use current frame only
        FragColor = currentData;
        return;
    }
    
    // Sample history with high-quality filter
    vec4 historyData = sampleCatmullRom(u_HistoryGTAO, prevUV, texSize);
    vec3 historyBentNormal = historyData.rgb * 2.0 - 1.0; // Decode
    float historyAO = historyData.a;
    float historyDepth = texture(u_HistoryDepth, prevUV).r;
    
    // Reconstruct history world position for depth comparison
    vec3 historyWorldPos = reconstructWorldPosition(prevUV, historyDepth,
                                                    u_CurrentInvProjection, u_CurrentInvView);
    
    // Depth-based rejection
    float depthDiff = abs(length(worldPos) - length(historyWorldPos));
    float depthWeight = exp(-depthDiff * depthDiff * 50.0);
    
    // Compute variance for clamping (AO channel only)
    float minAO, maxAO;
    float variance = computeVariance(u_CurrentGTAO, vTexCoord, texelSize, minAO, maxAO);
    
    // Variance-based rejection
    float varianceWeight = variance < u_VarianceThreshold ? 1.0 : 0.3;
    
    // Clamp history AO to neighborhood min-max (reduces ghosting)
    float margin = 0.1 * (maxAO - minAO);
    historyAO = clamp(historyAO, minAO - margin, maxAO + margin);
    
    // Compute final blend factor
    float finalBlendFactor = u_BlendFactor * depthWeight * varianceWeight;
    
    // Temporal accumulation for AO
    float resultAO = mix(currentAO, historyAO, finalBlendFactor);
    
    // Temporal accumulation for bent normal (blend directions)
    vec3 resultBentNormal = mix(currentBentNormal, historyBentNormal, finalBlendFactor * 0.8); // Slightly less blending for normals
    resultBentNormal = normalize(resultBentNormal);
    
    // Encode bent normal back to [0,1] range
    FragColor = vec4(resultBentNormal * 0.5 + 0.5, resultAO);
}
