#version 330 core

// High-quality circular bokeh blur shader
// Uses adaptive sample count based on CoC radius

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_SourceTexture;
uniform vec2 u_TexelSize;

uniform int u_SampleCount;          // Number of ring samples (quality)
uniform float u_BokehRadius;        // Maximum bokeh radius multiplier
uniform float u_BokehRotation;      // Rotation for temporal variation

// Golden angle for circular sampling (optimal distribution)
const float GOLDEN_ANGLE = 2.39996323;

void main()
{
    vec4 centerSample = texture(u_SourceTexture, vTexCoord);
    float centerCoC = centerSample.a;

    // If CoC is very small, skip expensive blur
    if (centerCoC < 0.001)
    {
        FragColor = centerSample;
        return;
    }

    vec3 color = centerSample.rgb;
    float totalWeight = 1.0;

    // Adaptive sample count based on CoC
    int samples = int(float(u_SampleCount) * centerCoC * 0.5 + 8.0);
    samples = clamp(samples, 8, u_SampleCount);

    float radius = centerCoC * u_BokehRadius;

    // Circular bokeh sampling using golden angle spiral
    for (int i = 1; i < samples; i++)
    {
        float angle = float(i) * GOLDEN_ANGLE + u_BokehRotation;
        float distance = sqrt(float(i) / float(samples)); // Uniform disc distribution

        vec2 offset = vec2(cos(angle), sin(angle)) * distance * radius * u_TexelSize;
        vec4 sample = texture(u_SourceTexture, vTexCoord + offset);

        // Weight by sample CoC (larger CoC = more contribution)
        float weight = smoothstep(0.0, radius, sample.a);
        weight = max(weight, 0.1); // Minimum weight to avoid artifacts

        color += sample.rgb * weight;
        totalWeight += weight;
    }

    color /= totalWeight;

    FragColor = vec4(color, centerCoC);
}
