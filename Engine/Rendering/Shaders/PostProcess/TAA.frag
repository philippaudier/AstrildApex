#version 330 core

// ============================================================================
// Temporal Anti-Aliasing (TAA)
// High-quality anti-aliasing using temporal reprojection
// Based on "High Quality Temporal Supersampling" by Brian Karis (Epic Games)
// ============================================================================

in vec2 vUV;
out vec4 FragColor;

// Current frame
uniform sampler2D u_CurrentFrame;    // Current rendered frame (jittered)
uniform sampler2D u_Depth;           // Current depth buffer (regular sampler2D, not shadow)
uniform sampler2D u_Velocity;        // Motion vectors (if available, optional)

// Previous frame
uniform sampler2D u_HistoryFrame;    // Previous TAA result

// Matrices for reprojection
uniform mat4 u_InvViewProj;          // Inverse view-projection (current frame)
uniform mat4 u_PrevViewProj;         // Previous frame view-projection

// TAA parameters
uniform float u_FeedbackMin;         // Minimum history blend (0.0 = no history, 0.8 = default)
uniform float u_FeedbackMax;         // Maximum history blend (0.95 = default, 0.0 on first frame)
uniform int u_UseYCoCg;              // Use YCoCg color space for better quality (1 = yes)
uniform vec2 u_Jitter;               // Current frame jitter offset in pixels

// Screen size
uniform vec2 u_ScreenSize;

// ============================================================================
// Color Space Conversions
// ============================================================================

// RGB to YCoCg (better for temporal work - less ghosting)
vec3 RGBToYCoCg(vec3 rgb)
{
    float Y  = dot(rgb, vec3( 0.25,  0.5,   0.25));
    float Co = dot(rgb, vec3( 0.5,   0.0,  -0.5));
    float Cg = dot(rgb, vec3(-0.25,  0.5,  -0.25));
    return vec3(Y, Co, Cg);
}

vec3 YCoCgToRGB(vec3 ycocg)
{
    float Y  = ycocg.x;
    float Co = ycocg.y;
    float Cg = ycocg.z;

    float r = Y + Co - Cg;
    float g = Y + Cg;
    float b = Y - Co - Cg;

    return vec3(r, g, b);
}

// ============================================================================
// Neighborhood Clamping (reduce ghosting)
// ============================================================================

// Sample 3x3 neighborhood and compute min/max for clamping
void SampleNeighborhood(vec2 uv, out vec3 colorMin, out vec3 colorMax, out vec3 colorAvg)
{
    vec3 m1 = vec3(0.0);
    vec3 m2 = vec3(0.0);

    vec2 texelSize = 1.0 / u_ScreenSize;

    // 3x3 neighborhood sampling (9 samples)
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec3 color = texture(u_CurrentFrame, uv + offset).rgb;

            if (u_UseYCoCg == 1)
                color = RGBToYCoCg(color);

            m1 += color;
            m2 += color * color;
        }
    }

    // Compute moments
    const float numSamples = 9.0;
    m1 /= numSamples;
    m2 /= numSamples;

    // Variance-based clamping (more robust than min/max)
    vec3 sigma = sqrt(max(m2 - m1 * m1, 0.0));

    colorMin = m1 - sigma * 1.25; // 1.25 = tolerance (lower = less ghosting, more flicker)
    colorMax = m1 + sigma * 1.25;
    colorAvg = m1;
}

// Clamp color to AABB
vec3 ClipAABB(vec3 color, vec3 colorMin, vec3 colorMax)
{
    // Center of AABB
    vec3 center = 0.5 * (colorMax + colorMin);
    vec3 extents = 0.5 * (colorMax - colorMin);

    // Vector from center to color
    vec3 v = color - center;

    // Clip to box
    vec3 absV = abs(v);
    vec3 maxComp = max(absV - extents, 0.0);
    float maxCompScalar = max(maxComp.x, max(maxComp.y, maxComp.z));

    if (maxCompScalar > 0.0)
    {
        vec3 dir = v / absV;
        return center + dir * extents;
    }

    return color;
}

// ============================================================================
// Motion Vector / Reprojection
// ============================================================================

vec2 GetVelocity(vec2 uv)
{
    // Option 1: Use motion vector texture if available
    // return texture(u_Velocity, uv).xy;

    // Option 2: Compute from depth (fallback)
    float depth = texture(u_Depth, uv).r;

    // Reconstruct world position
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 worldPos = u_InvViewProj * ndc;
    worldPos /= worldPos.w;

    // Project to previous frame
    vec4 prevClip = u_PrevViewProj * worldPos;
    prevClip /= prevClip.w;
    vec2 prevUV = prevClip.xy * 0.5 + 0.5;

    // Velocity in screen space
    return prevUV - uv;
}

// ============================================================================
// Main TAA
// ============================================================================

void main()
{
    vec2 uv = vUV;

    // Sample current frame (jittered)
    vec3 currentColor = texture(u_CurrentFrame, uv).rgb;

    // DEBUG: Output current color directly to test if input is valid
    // Uncomment this to bypass TAA and see if input texture is valid
    // FragColor = vec4(currentColor, 1.0);
    // return;

    // First frame: no valid history yet, output current frame directly
    if (u_FeedbackMax <= 0.001)
    {
        FragColor = vec4(currentColor, 1.0);
        return;
    }

    // Read current depth and early-out for sky / far-plane pixels which often
    // produce invalid reprojection (skybox, clear color). This avoids ringing
    // and black pixels for background.
    float currDepth = texture(u_Depth, uv).r;
    if (currDepth <= 0.0001 || currDepth >= 0.9999)
    {
        FragColor = vec4(currentColor, 1.0);
        return;
    }

    // Get velocity and reproject to previous frame
    vec2 velocity = GetVelocity(uv);
    vec2 prevUV = uv + velocity;

    // Check if previous sample is valid (on screen)
    bool isValidHistory = all(greaterThanEqual(prevUV, vec2(0.0))) &&
                         all(lessThanEqual(prevUV, vec2(1.0)));

    if (!isValidHistory)
    {
        // No valid history - use current frame
        // DEBUG: Show in red when history is invalid
        // FragColor = vec4(1.0, 0.0, 0.0, 1.0);
        FragColor = vec4(currentColor, 1.0);
        return;
    }

    // Sample history
    vec3 historyColor = texture(u_HistoryFrame, prevUV).rgb;

    // Depth consistency test: sample depth at reprojected UV and ensure it is
    // similar to current depth. Large differences indicate disocclusion and we
    // should not use history for that pixel.
    float prevDepth = texture(u_Depth, prevUV).r;
    if (abs(prevDepth - currDepth) > 0.02)
    {
        FragColor = vec4(currentColor, 1.0);
        return;
    }

    // Convert to YCoCg if enabled (better temporal stability)
    if (u_UseYCoCg == 1)
    {
        currentColor = RGBToYCoCg(currentColor);
        historyColor = RGBToYCoCg(historyColor);
    }

    // Sample neighborhood for clamping
    vec3 colorMin, colorMax, colorAvg;
    SampleNeighborhood(uv, colorMin, colorMax, colorAvg);

    // Clamp history to neighborhood (reduces ghosting)
    historyColor = ClipAABB(historyColor, colorMin, colorMax);

    // Adaptive feedback based on similarity (use luminance for perceptual stability)
    // More similar = more history (less flicker); less similar = less history (less ghosting)
    float currLum = dot(currentColor, vec3(0.2126, 0.7152, 0.0722));
    float histLum = dot(historyColor, vec3(0.2126, 0.7152, 0.0722));
    float lumDiff = abs(histLum - currLum);

    // Use a softer threshold for blending to favor history when reasonably similar
    float feedback = mix(u_FeedbackMin, u_FeedbackMax,
                        smoothstep(0.0, 0.4, 1.0 - lumDiff));

    // Reduce feedback at screen edges (fixes edge artifacts)
    vec2 edgeDist = min(uv, 1.0 - uv);
    // Convert normalized distance to pixels using the smaller dimension
    float edgeDistPx = min(edgeDist.x, edgeDist.y) * min(u_ScreenSize.x, u_ScreenSize.y);
    // Use a small threshold (in pixels) to fade history near edges
    float edgeFadeThreshold = 20.0;
    float edgeFactor = clamp(edgeDistPx / edgeFadeThreshold, 0.0, 1.0);
    feedback *= edgeFactor;

    // Temporal blend
    vec3 result = mix(currentColor, historyColor, feedback);

    // Small temporal sharpening to preserve detail (helps reduce perceived blur
    // introduced by stronger history blending). This nudges the blended result
    // slightly towards the current frame luminance/details.
    float sharpenAmount = 0.12; // small value, tuned for stability
    result = mix(result, currentColor, sharpenAmount);

    // Convert back to RGB
    if (u_UseYCoCg == 1)
        result = YCoCgToRGB(result);

    // Ensure no negative colors (can happen with YCoCg conversion)
    result = max(result, vec3(0.0));

    FragColor = vec4(result, 1.0);
}
