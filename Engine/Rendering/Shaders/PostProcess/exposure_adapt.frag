#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_CurrentLuminance; // 1x1 texture with current average log-luminance
uniform sampler2D u_PreviousExposure; // 1x1 texture with previous exposure value
uniform float u_TargetBrightness;
uniform float u_MinExposure;
uniform float u_MaxExposure;
uniform float u_AdaptationSpeed;
uniform float u_DeltaTime;
uniform bool u_FirstFrame;

void main()
{
    // Read current average log-luminance from 1x1 texture
    float avgLogLum = texture(u_CurrentLuminance, vec2(0.5)).r;

    // Convert from log-luminance to linear luminance
    float avgLum = exp(avgLogLum);

    // Avoid division by zero
    avgLum = max(avgLum, 0.0001);

    // Calculate target exposure
    float targetExposure = u_TargetBrightness / avgLum;

    // Clamp to min/max range
    targetExposure = clamp(targetExposure, u_MinExposure, u_MaxExposure);

    // Read previous exposure
    float previousExposure = texture(u_PreviousExposure, vec2(0.5)).r;

    float finalExposure;
    if (u_FirstFrame)
    {
        // First frame: use target directly
        finalExposure = targetExposure;
    }
    else
    {
        // Temporal smoothing: exponential interpolation
        // lerpFactor = 1 - exp(-dt / tau) where tau = AdaptationSpeed
        float adaptationTime = max(0.001, u_AdaptationSpeed);
        float lerpFactor = 1.0 - exp(-u_DeltaTime / adaptationTime);
        finalExposure = previousExposure + lerpFactor * (targetExposure - previousExposure);
    }

    // Output the computed exposure value (single float in R channel)
    FragColor = vec4(finalExposure, finalExposure, finalExposure, 1.0);
}
