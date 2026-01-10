#version 420 core

#include "../Includes/Common.glsl"

// Input from geometry shader
in GS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec3 localPos;
    float aoFactor;
    vec3 rockColor;
    float mossBlend;
} fs_in;

// Uniforms
uniform vec4 u_BaseColor;
uniform vec4 u_DarkColor;
uniform vec4 u_HighlightColor;
uniform vec4 u_MossColor;
uniform float u_Roughness;
uniform float u_Metallic;

// Lighting
uniform vec3 u_AmbientColor;
uniform float u_AmbientIntensity;
uniform vec3 u_SunDirection;
uniform vec3 u_SunColor;
uniform float u_SunIntensity;

// Output
out vec4 FragColor;

// Simple noise for surface detail
float hash(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    
    float n = i.x + i.y * 157.0 + 113.0 * i.z;
    return mix(
        mix(mix(hash(vec3(n, 0.0, 0.0)), hash(vec3(n + 1.0, 0.0, 0.0)), f.x),
            mix(hash(vec3(n + 157.0, 0.0, 0.0)), hash(vec3(n + 158.0, 0.0, 0.0)), f.x), f.y),
        mix(mix(hash(vec3(n + 113.0, 0.0, 0.0)), hash(vec3(n + 114.0, 0.0, 0.0)), f.x),
            mix(hash(vec3(n + 270.0, 0.0, 0.0)), hash(vec3(n + 271.0, 0.0, 0.0)), f.x), f.y),
        f.z);
}

void main()
{
    vec3 N = normalize(fs_in.normal);
    vec3 V = normalize(uCameraPos - fs_in.worldPos);
    vec3 L = normalize(-u_SunDirection);
    vec3 H = normalize(L + V);
    
    // === BASE COLOR ===
    
    // Local position noise for surface variation
    float surfaceNoise = noise3D(fs_in.localPos * 8.0) * 0.2;
    
    // Mix between dark (crevices) and highlight (exposed edges)
    float heightFactor = (fs_in.localPos.y + 1.0) * 0.5; // Normalize Y to 0-1
    vec3 baseColor = mix(u_DarkColor.rgb, fs_in.rockColor, fs_in.aoFactor);
    baseColor = mix(baseColor, u_HighlightColor.rgb, pow(max(0.0, fs_in.localPos.y), 2.0) * 0.3);
    
    // Add surface noise variation
    baseColor += vec3(surfaceNoise - 0.1);
    
    // === MOSS BLENDING ===
    vec3 finalColor = mix(baseColor, u_MossColor.rgb, fs_in.mossBlend);
    float finalRoughness = mix(u_Roughness, 0.9, fs_in.mossBlend); // Moss is rougher
    
    // === LIGHTING ===
    
    // Ambient
    vec3 ambient = u_AmbientColor * u_AmbientIntensity * fs_in.aoFactor;
    
    // Diffuse (Lambert)
    float NdotL = max(dot(N, L), 0.0);
    vec3 diffuse = u_SunColor * u_SunIntensity * NdotL;
    
    // Specular (simplified GGX)
    float NdotH = max(dot(N, H), 0.0);
    float roughSq = finalRoughness * finalRoughness;
    float spec = pow(NdotH, mix(128.0, 4.0, roughSq)) * (1.0 - finalRoughness) * 0.3;
    vec3 specular = u_SunColor * spec * (1.0 - u_Metallic * 0.5);
    
    // Fresnel rim lighting for rocks
    float fresnel = pow(1.0 - max(dot(N, V), 0.0), 3.0) * 0.15;
    
    // === FINAL COLOR ===
    vec3 litColor = finalColor * (ambient + diffuse) + specular + fresnel * u_HighlightColor.rgb;
    
    // Simple tone mapping
    litColor = litColor / (litColor + vec3(1.0));
    
    FragColor = vec4(litColor, 1.0);
}
