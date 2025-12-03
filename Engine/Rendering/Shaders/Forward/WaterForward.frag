#version 420 core

#include "../Includes/Common.glsl"
#include "../Includes/IBL.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"

in vec3 v_WorldPos;
in vec3 v_Normal;
in vec2 v_TexCoord;

layout(location=0) out vec4 FragColor;

// Standard material samplers (bound by MaterialRuntime)
uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;
uniform vec4 u_AlbedoColor;
uniform float u_Metallic;
uniform float u_Smoothness;
uniform float u_NormalStrength;

// Water-specific uniforms (MaterialRuntime binds many of these)
uniform vec4 u_WaterColor; // rgba tint
uniform float u_Opacity; // 0..1
uniform float u_WaveAmplitude;
uniform float u_WaveFrequency;
uniform float u_WaveSpeed;
uniform vec2 u_WaveDirection;
uniform float u_Wave2Amplitude;
uniform float u_Wave2Frequency;
uniform float u_Wave2Speed;
uniform vec2 u_Wave2Direction;
uniform float u_WaterFresnelPower;
uniform float u_WaterReflectionStrength;
uniform vec2 u_NormalTiling;
uniform vec2 u_NormalScrollSpeed;

// Simple helper to compute two-layered Gerstner-like waves (cheap)
float waveAt(vec2 pos, vec2 dir, float freq, float speed, float amp, float time)
{
    float phase = dot(pos, normalize(dir)) * freq + time * speed;
    return sin(phase) * amp;
}

void main()
{
    // Compute animated height offset for this fragment (used for subtle normal perturb)
    float t = u_Time;
    vec2 pos = v_WorldPos.xz;
    float h1 = waveAt(pos, u_WaveDirection, u_WaveFrequency, u_WaveSpeed, u_WaveAmplitude, t);
    float h2 = waveAt(pos, u_Wave2Direction, u_Wave2Frequency, u_Wave2Speed, u_Wave2Amplitude, t);
    float heightOffset = h1 + h2;

    // Perturb UVs for normal scrolling
    vec2 nUv = v_TexCoord * u_NormalTiling + u_NormalScrollSpeed * t;
    vec3 nMap = texture(u_NormalTex, nUv).rgb * 2.0 - 1.0;
    // Respect runtime normal flip
    if (u_FlipNormalY == 1) nMap.y = -nMap.y;
    vec3 sampledNormal = normalize(vec3(nMap.x, nMap.y, nMap.z));

    // Blend sampled normal with geometry normal for smoother waves
    vec3 baseN = normalize(v_Normal);
    vec3 N = normalize(mix(baseN, sampledNormal, clamp(u_NormalStrength, 0.0, 1.0)));

    // Small perturbation from height offset: approximate derivatives to nudge normal
    float eps = 0.001;
    float hx = waveAt(pos + vec2(eps,0.0), u_WaveDirection, u_WaveFrequency, u_WaveSpeed, u_WaveAmplitude, t) +
               waveAt(pos + vec2(eps,0.0), u_Wave2Direction, u_Wave2Frequency, u_Wave2Speed, u_Wave2Amplitude, t);
    float hz = waveAt(pos + vec2(0.0,eps), u_WaveDirection, u_WaveFrequency, u_WaveSpeed, u_WaveAmplitude, t) +
               waveAt(pos + vec2(0.0,eps), u_Wave2Direction, u_Wave2Frequency, u_Wave2Speed, u_Wave2Amplitude, t);
    vec3 d = normalize(vec3(hx - heightOffset, eps, hz - heightOffset));
    N = normalize(mix(N, d, 0.2));

    // Prepare material properties; use PBR helpers
    MaterialProperties material = setupMaterialProperties(u_AlbedoTex, u_NormalTex, v_TexCoord, u_AlbedoColor, u_NormalStrength, u_Metallic, u_Smoothness, N);

    // Override F0 for water-like dielectric (depends on refractive index). Use small constant (approx 0.02)
    material.F0 = vec3(0.02);

    // View vector
    vec3 V = normalize(uCameraPos - v_WorldPos);

    // Direct lighting
    vec3 Lo = vec3(0.0);
    Lo += calculateDirectionalLight(N, V, material);
    Lo += calculatePointLights(v_WorldPos, N, V, material);
    Lo += calculateSpotLights(v_WorldPos, N, V, material);

    // Ambient (IBL-aware)
    vec3 ambient = calculateAmbientLighting(material, v_WorldPos);

    // Extra reflection/specular boost for water using prefiltered env
    vec3 specularBoost = vec3(0.0);
    if (u_HasIBL != 0)
    {
        vec3 R = reflect(-V, N);
        float NdotV = clamp(dot(N, V), 0.0, 1.0);
        vec3 prefiltered = samplePrefilteredEnv(R, material.roughness);
        vec2 brdf = integrateBRDF(NdotV, material.roughness);
        vec3 spec = prefiltered * (material.F0 * brdf.x + brdf.y);
        float fresnel = clamp(dot(N, V), 0.0, 1.0);
        fresnel = pow(1.0 - fresnel, u_WaterFresnelPower);
        specularBoost = spec * u_WaterReflectionStrength * fresnel;
    }

    vec3 shaded = ambient + Lo + specularBoost;

    // Tint with water color and apply opacity
    vec3 outColor = mix(shaded, u_WaterColor.rgb * shaded, 0.5);

    FragColor = vec4(outColor, clamp(u_Opacity, 0.0, 1.0));

    // Apply fog to alpha-blended color
    FragColor.rgb = processFog(FragColor.rgb, v_WorldPos);
}
