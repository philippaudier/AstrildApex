#version 450 core

// Inputs from vertex shader
in vec2 vTexCoord;
in vec4 vColor;

// Output
out vec4 FragColor;

// Optional texture (for textured particles)
uniform sampler2D uTexture;
uniform int uHasTexture = 0;
uniform float uAlphaCutoff = 0.0;
uniform int uUseAlphaCutoff = 0;

void main()
{
    vec4 texColor = vec4(1.0);

    if (uHasTexture > 0)
    {
        texColor = texture(uTexture, vTexCoord);

        // Alpha cutoff for sprites (removes transparent background)
        if (uUseAlphaCutoff > 0 && texColor.a < uAlphaCutoff)
        {
            discard;
        }
    }
    else
    {
        // Default: circular gradient for soft particles
        vec2 center = vTexCoord - vec2(0.5);
        float dist = length(center);
        float alpha = 1.0 - smoothstep(0.3, 0.5, dist);
        texColor = vec4(1.0, 1.0, 1.0, alpha);
    }

    // Apply particle color and fade
    FragColor = vColor * texColor;

    // Discard fully transparent pixels for better performance
    if (FragColor.a < 0.01)
        discard;
}
