#version 330 core

out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform float u_Threshold = 1.0;
uniform float u_SoftKnee = 0.5;
uniform float u_Clamp = 65472.0;
uniform int u_FirstPass = 0;

in vec2 vTexCoord;

// Unity HDRP style soft knee function for smooth bloom threshold
vec3 QuadraticThreshold(vec3 color, float threshold, float softKnee)
{
    float brightness = max(color.r, max(color.g, color.b));

    // Soft knee calculation (Unity HDRP style)
    float knee = threshold * softKnee + 1e-5;
    float soft = brightness - threshold + knee;
    soft = clamp(soft, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-5);

    float contribution = max(soft, brightness - threshold);
    contribution /= max(brightness, 1e-5);

    return color * contribution;
}

// High-quality 13-tap downsampling filter (Karis average for anti-aliasing)
vec3 DownsampleBox13Tap(sampler2D tex, vec2 uv, vec2 texelSize)
{
    // Sample pattern:
    //   A   B   C
    // D   E   F   G
    // H   I   J   K
    //   L   M   N

    vec3 A = texture(tex, uv + texelSize * vec2(-1.0, -1.0)).rgb;
    vec3 B = texture(tex, uv + texelSize * vec2( 0.0, -1.0)).rgb;
    vec3 C = texture(tex, uv + texelSize * vec2( 1.0, -1.0)).rgb;
    vec3 D = texture(tex, uv + texelSize * vec2(-0.5, -0.5)).rgb;
    vec3 E = texture(tex, uv + texelSize * vec2( 0.5, -0.5)).rgb;
    vec3 F = texture(tex, uv + texelSize * vec2(-1.0,  0.0)).rgb;
    vec3 G = texture(tex, uv                               ).rgb;
    vec3 H = texture(tex, uv + texelSize * vec2( 1.0,  0.0)).rgb;
    vec3 I = texture(tex, uv + texelSize * vec2(-0.5,  0.5)).rgb;
    vec3 J = texture(tex, uv + texelSize * vec2( 0.5,  0.5)).rgb;
    vec3 K = texture(tex, uv + texelSize * vec2(-1.0,  1.0)).rgb;
    vec3 L = texture(tex, uv + texelSize * vec2( 0.0,  1.0)).rgb;
    vec3 M = texture(tex, uv + texelSize * vec2( 1.0,  1.0)).rgb;

    // Karis average (weights to avoid fireflies and temporal instability)
    vec3 groups[5];
    groups[0] = (A + B + D + E) * (0.125 / 4.0); // Top-left quad
    groups[1] = (B + C + E + F) * (0.125 / 4.0); // Top-right quad
    groups[2] = (F + G + I + J) * (0.5 / 4.0);   // Center (higher weight)
    groups[3] = (I + J + K + L) * (0.125 / 4.0); // Bottom-left quad
    groups[4] = (J + M + L + H) * (0.125 / 4.0); // Bottom-right quad

    return groups[0] + groups[1] + groups[2] + groups[3] + groups[4];
}

// Simple 4-tap downsampling for better performance on subsequent passes
vec3 DownsampleBox4Tap(sampler2D tex, vec2 uv, vec2 texelSize)
{
    vec4 offset = texelSize.xyxy * vec4(-0.5, -0.5, 0.5, 0.5);

    vec3 s0 = texture(tex, uv + offset.xy).rgb;
    vec3 s1 = texture(tex, uv + offset.zy).rgb;
    vec3 s2 = texture(tex, uv + offset.xw).rgb;
    vec3 s3 = texture(tex, uv + offset.zw).rgb;

    return (s0 + s1 + s2 + s3) * 0.25;
}

void main()
{
    vec2 texelSize = 1.0 / textureSize(u_SourceTexture, 0);
    vec3 color;

    if (u_FirstPass == 1)
    {
        // First pass: high-quality downsampling with anti-aliasing + threshold extraction
        color = DownsampleBox13Tap(u_SourceTexture, vTexCoord, texelSize);
        color = QuadraticThreshold(color, u_Threshold, u_SoftKnee);

        // Clamp to avoid infinite values
        color = min(color, vec3(u_Clamp));
    }
    else
    {
        // Subsequent passes: faster 4-tap downsampling
        color = DownsampleBox4Tap(u_SourceTexture, vTexCoord, texelSize);

        // Apply clamp to all passes to prevent blow-up
        color = min(color, vec3(u_Clamp));
    }

    FragColor = vec4(color, 1.0);
}