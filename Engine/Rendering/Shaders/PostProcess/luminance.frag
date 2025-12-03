#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;

// Calculate luminance from RGB
float Luminance(vec3 color)
{
    return dot(color, vec3(0.2126, 0.7152, 0.0722));
}

void main()
{
    vec3 color = texture(u_SourceTexture, vTexCoord).rgb;

    // Calculate luminance
    float lum = Luminance(color);

    // Output luminance to R channel
    // We use log luminance for better distribution
    // Add small epsilon to avoid log(0)
    float logLum = log(max(lum, 0.0001));

    FragColor = vec4(logLum, logLum, logLum, 1.0);
}
