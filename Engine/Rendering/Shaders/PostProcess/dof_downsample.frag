#version 330 core

// Downsample shader for DOF
// Downsamples to half resolution while preserving CoC in alpha channel

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_SourceTexture;
uniform vec2 u_TexelSize;

void main()
{
    // 4-tap box filter for downsampling
    vec2 offset = u_TexelSize * 0.5;

    vec4 s0 = texture(u_SourceTexture, vTexCoord + vec2(-offset.x, -offset.y));
    vec4 s1 = texture(u_SourceTexture, vTexCoord + vec2( offset.x, -offset.y));
    vec4 s2 = texture(u_SourceTexture, vTexCoord + vec2(-offset.x,  offset.y));
    vec4 s3 = texture(u_SourceTexture, vTexCoord + vec2( offset.x,  offset.y));

    // Average color
    vec3 color = (s0.rgb + s1.rgb + s2.rgb + s3.rgb) * 0.25;

    // Maximum CoC (we want to preserve the largest blur)
    float coc = max(max(s0.a, s1.a), max(s2.a, s3.a));

    FragColor = vec4(color, coc);
}
