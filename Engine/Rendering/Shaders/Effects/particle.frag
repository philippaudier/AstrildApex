#version 450 core

// Inputs from vertex shader
in vec2 vTexCoord;
in vec4 vColor;

// Output
out vec4 FragColor;

// Optional texture (for textured particles)
uniform sampler2D uTexture;
uniform bool uHasTexture = false;

void main()
{
    vec4 texColor = vec4(1.0);
    
    if (uHasTexture)
    {
        texColor = texture(uTexture, vTexCoord);
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
