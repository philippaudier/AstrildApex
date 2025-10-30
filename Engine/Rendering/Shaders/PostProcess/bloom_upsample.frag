#version 330 core

out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform float u_Radius = 1.0;
uniform float u_Scatter = 0.7;

in vec2 vTexCoord;

// Unity HDRP style 9-tap tent filter for smooth upsampling
vec3 UpsampleTent9Tap(sampler2D tex, vec2 uv, vec2 texelSize, float radius)
{
    // Scale radius to make it more responsive (0.5 to 2.0 range)
    float effectiveRadius = 0.5 + radius * 1.5;
    vec4 d = texelSize.xyxy * vec4(1.0, 1.0, -1.0, 0.0) * effectiveRadius;

    vec3 s;
    s  = texture(tex, uv - d.xy).rgb;  // Top-left
    s += texture(tex, uv - d.wy).rgb * 2.0;  // Top-center (weight 2)
    s += texture(tex, uv - d.zy).rgb;  // Top-right

    s += texture(tex, uv + d.zw).rgb * 2.0;  // Middle-left (weight 2)
    s += texture(tex, uv       ).rgb * 4.0;  // Center (weight 4)
    s += texture(tex, uv + d.xw).rgb * 2.0;  // Middle-right (weight 2)

    s += texture(tex, uv + d.zy).rgb;  // Bottom-left
    s += texture(tex, uv + d.wy).rgb * 2.0;  // Bottom-center (weight 2)
    s += texture(tex, uv + d.xy).rgb;  // Bottom-right

    return s * (1.0 / 16.0); // Normalize by total weight (1+2+1+2+4+2+1+2+1 = 16)
}

// Alternative box filter for comparison/fallback
vec3 UpsampleBox4Tap(sampler2D tex, vec2 uv, vec2 texelSize, float radius)
{
    vec4 d = texelSize.xyxy * vec4(-radius, -radius, radius, radius);

    vec3 s;
    s  = texture(tex, uv + d.xy).rgb;  // Top-left
    s += texture(tex, uv + d.zy).rgb;  // Top-right
    s += texture(tex, uv + d.xw).rgb;  // Bottom-left
    s += texture(tex, uv + d.zw).rgb;  // Bottom-right

    return s * 0.25;
}

void main()
{
    vec2 texelSize = 1.0 / textureSize(u_SourceTexture, 0);

    // Use tent filter for high-quality upsampling
    vec3 color = UpsampleTent9Tap(u_SourceTexture, vTexCoord, texelSize, u_Radius);

    // Apply scattering for progressive bloom diffusion
    color *= u_Scatter;

    FragColor = vec4(color, 1.0);
}