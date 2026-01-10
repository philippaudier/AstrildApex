#version 420 core

#include "../Includes/Common.glsl"

// Input from geometry shader
in GS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;
    float heightFactor;
    vec3 color;
} fs_in;

// Output
layout(location = 0) out vec4 FragColor;

// Grass texture (optional)
uniform sampler2D u_AlbedoTex;
uniform bool u_HasAlbedoTex;

// Lighting
uniform vec3 u_AmbientColor;
uniform float u_AmbientIntensity;

void main()
{
    // Base color from geometry shader
    vec3 albedo = fs_in.color;
    
    // Apply texture if available
    if (u_HasAlbedoTex)
    {
        vec4 texColor = texture(u_AlbedoTex, fs_in.uv);
        albedo *= texColor.rgb;
        
        // Alpha clipping for grass texture
        if (texColor.a < 0.3)
            discard;
    }
    
    // Simple lighting (ambient + directional)
    vec3 normal = normalize(fs_in.normal);
    vec3 lightDir = normalize(uDirLightDirection);

    // Ambient
    vec3 ambient = u_AmbientColor * u_AmbientIntensity;

    // Diffuse (wrap lighting for softer grass)
    float NdotL = dot(normal, -lightDir);
    float wrap = 0.5; // Wrap factor for subsurface scattering approximation
    float diffuse = max(0.0, (NdotL + wrap) / (1.0 + wrap));

    vec3 directional = uDirLightColor * uDirLightIntensity * diffuse;

    // Combine lighting
    vec3 lighting = ambient + directional;
    vec3 finalColor = albedo * lighting;

    // Apply simple fog if enabled
    if (uFogEnabled > 0) {
        float dist = length(fs_in.worldPos - uCameraPos);
        float fogFactor = smoothstep(uFogStart, uFogEnd, dist) * uFogOpacity;
        finalColor = mix(finalColor, uFogColor, fogFactor);
    }
    
    // Alpha for soft edges at blade tips
    float alpha = 1.0;
    if (fs_in.heightFactor > 0.8)
    {
        alpha = 1.0 - smoothstep(0.8, 1.0, fs_in.heightFactor);
    }
    
    FragColor = vec4(finalColor, alpha);
}
