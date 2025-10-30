#version 330 core

out vec4 FragColor;

uniform sampler2D u_OriginalTexture;
uniform sampler2D u_BloomTexture;
uniform float u_BloomIntensity = 1.0;

in vec2 vTexCoord;

void main()
{
    vec3 original = texture(u_OriginalTexture, vTexCoord).rgb;
    vec3 bloom = texture(u_BloomTexture, vTexCoord).rgb;

    // Combinaison additive du bloom avec l'image originale
    vec3 result = original + bloom * u_BloomIntensity;

    FragColor = vec4(result, 1.0);
}