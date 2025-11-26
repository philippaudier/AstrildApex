#version 330 core

// High-quality per-object motion blur using velocity buffer
// Reconstructs motion vectors from camera and object movement

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_ColorTexture;
uniform sampler2D u_DepthTexture;
uniform sampler2D u_VelocityTexture;  // Optional: per-object velocity

// Motion blur settings
uniform int u_SampleCount;          // Number of samples along motion vector (4-32)
uniform float u_Intensity;          // Motion blur strength multiplier
uniform float u_MaxBlurRadius;      // Maximum blur radius in pixels

// Camera matrices for motion reconstruction
uniform mat4 u_InvViewProj;         // Current frame inverse view-projection
uniform mat4 u_PrevViewProj;        // Previous frame view-projection

uniform vec2 u_ScreenSize;
uniform float u_NearPlane;
uniform float u_FarPlane;

// Linearize depth
float LinearizeDepth(float depth)
{
    float z = depth * 2.0 - 1.0;
    return (2.0 * u_NearPlane * u_FarPlane) / (u_FarPlane + u_NearPlane - z * (u_FarPlane - u_NearPlane));
}

// Reconstruct world position from depth
vec3 ReconstructWorldPos(vec2 uv, float depth)
{
    vec4 clipPos = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 worldPos = u_InvViewProj * clipPos;
    return worldPos.xyz / worldPos.w;
}

// Calculate velocity vector for a pixel
vec2 CalculateVelocity(vec2 uv, float depth)
{
    // Check if we have a velocity texture
    vec2 velocityFromBuffer = texture(u_VelocityTexture, uv).rg;

    // If velocity buffer has valid data, use it (faster and more accurate for moving objects)
    if (length(velocityFromBuffer) > 0.001)
    {
        return velocityFromBuffer * u_Intensity;
    }

    // Fallback: Reconstruct velocity from depth (camera motion only)
    vec3 worldPos = ReconstructWorldPos(uv, depth);

    // Project to previous frame
    vec4 prevClipPos = u_PrevViewProj * vec4(worldPos, 1.0);
    vec2 prevUV = (prevClipPos.xy / prevClipPos.w) * 0.5 + 0.5;

    // Velocity in screen space
    vec2 velocity = (uv - prevUV) * u_Intensity;

    return velocity;
}

void main()
{
    vec2 uv = vTexCoord;
    float depth = texture(u_DepthTexture, uv).r;

    // Skip skybox (depth = 1.0)
    if (depth >= 0.9999)
    {
        FragColor = texture(u_ColorTexture, uv);
        return;
    }

    // Calculate motion vector
    vec2 velocity = CalculateVelocity(uv, depth);

    // Early exit if no motion
    float velocityMag = length(velocity);
    if (velocityMag < 0.0001)
    {
        FragColor = texture(u_ColorTexture, uv);
        return;
    }

    // Clamp velocity to max blur radius
    float maxVelocity = u_MaxBlurRadius / max(u_ScreenSize.x, u_ScreenSize.y);
    if (velocityMag > maxVelocity)
    {
        velocity = normalize(velocity) * maxVelocity;
        velocityMag = maxVelocity;
    }

    // Adaptive sample count based on velocity magnitude
    int samples = max(4, int(float(u_SampleCount) * velocityMag * 10.0));
    samples = min(samples, u_SampleCount);

    // Accumulate samples along motion vector
    vec3 color = vec3(0.0);
    float totalWeight = 0.0;

    // Sample along the motion vector with stratified sampling
    for (int i = 0; i < samples; i++)
    {
        // Random jitter for better distribution (using interleaved gradient noise)
        float noise = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));
        float t = (float(i) + noise) / float(samples);

        // Sample offset from center
        vec2 offset = velocity * (t - 0.5);
        vec2 sampleUV = uv + offset;

        // Skip out-of-bounds samples
        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
            continue;

        // Depth-aware weighting (avoid bleeding from foreground to background)
        float sampleDepth = texture(u_DepthTexture, sampleUV).r;
        float depthDiff = abs(LinearizeDepth(depth) - LinearizeDepth(sampleDepth));
        float depthWeight = exp(-depthDiff * 0.5); // Soft falloff

        // Sample color
        vec3 sampleColor = texture(u_ColorTexture, sampleUV).rgb;

        // Weight by depth similarity
        float weight = depthWeight;

        color += sampleColor * weight;
        totalWeight += weight;
    }

    // Normalize
    if (totalWeight > 0.0)
    {
        color /= totalWeight;
    }
    else
    {
        color = texture(u_ColorTexture, uv).rgb;
    }

    FragColor = vec4(color, 1.0);
}
