#version 410 core

in vec3 v_WorldPos;
in vec3 v_Normal;
in vec2 v_TexCoord;
in float v_Height;

out vec4 FragColor;

uniform float u_MinHeight;
uniform float u_MaxHeight;

void main()
{
    // Color by height: blue (low) -> green (mid) -> yellow (high)
    float t = clamp((v_Height - u_MinHeight) / (u_MaxHeight - u_MinHeight), 0.0, 1.0);
    
    vec3 color;
    if (t < 0.5) {
        // Blue to green
        float s = t * 2.0;
        color = mix(vec3(0.0, 0.3, 0.8), vec3(0.2, 0.8, 0.2), s);
    } else {
        // Green to yellow
        float s = (t - 0.5) * 2.0;
        color = mix(vec3(0.2, 0.8, 0.2), vec3(0.9, 0.9, 0.1), s);
    }
    
    // Add simple normal-based shading for depth perception
    vec3 lightDir = normalize(vec3(0.3, 0.7, 0.4));
    float ndotl = max(dot(normalize(v_Normal), lightDir), 0.0);
    color *= (0.5 + 0.5 * ndotl);
    
    FragColor = vec4(color, 1.0);
}
