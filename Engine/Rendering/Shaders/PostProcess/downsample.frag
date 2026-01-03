#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform vec2 u_TexelSize; // 1.0 / texture dimensions

// Downsample by averaging 4 samples in a 2x2 box
void main()
{
    // Sample 4 texels in a 2x2 pattern
    vec4 s0 = texture(u_SourceTexture, vTexCoord + vec2(-0.5, -0.5) * u_TexelSize);
    vec4 s1 = texture(u_SourceTexture, vTexCoord + vec2(0.5, -0.5) * u_TexelSize);
    vec4 s2 = texture(u_SourceTexture, vTexCoord + vec2(-0.5, 0.5) * u_TexelSize);
    vec4 s3 = texture(u_SourceTexture, vTexCoord + vec2(0.5, 0.5) * u_TexelSize);

    // Average the 4 samples
    FragColor = (s0 + s1 + s2 + s3) * 0.25;
}
