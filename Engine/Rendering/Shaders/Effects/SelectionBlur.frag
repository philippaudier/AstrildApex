#version 330 core

// Separable Gaussian blur for outline dilation
// This shader dilates the silhouette mask to create outline thickness

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_Texture;
uniform vec2 u_Direction;  // (1,0) for horizontal, (0,1) for vertical
uniform vec2 u_TexelSize;  // 1.0 / textureSize
uniform float u_BlurRadius; // Blur radius in pixels (controls outline thickness)

void main()
{
    // Gaussian blur with variable kernel size based on blur radius
    // For outline, we want a box blur or dilate effect rather than gaussian
    // This creates a more uniform outline thickness

    float result = 0.0;
    float samples = 0.0;

    // Sample along the direction (horizontal or vertical)
    int radius = int(u_BlurRadius);

    for (int i = -radius; i <= radius; i++)
    {
        vec2 offset = vec2(float(i)) * u_Direction * u_TexelSize;
        float sample = texture(u_Texture, vTexCoord + offset).r;

        // Use max instead of sum for dilation effect
        result = max(result, sample);
        samples += 1.0;
    }

    // For blur: result /= samples;
    // For dilation: use max (already done above)

    FragColor = vec4(result, result, result, 1.0);
}
