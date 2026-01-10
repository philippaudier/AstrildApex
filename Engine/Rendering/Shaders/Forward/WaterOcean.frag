#version 420 core

// WaterOcean Fragment Shader
// Based on GPU Gems techniques and Shadertoy implementations
// Features: Gerstner waves, SSS, Fresnel, reflections, caustics, foam

#include "../Includes/Common.glsl"
#include "../Includes/IBL.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"
#include "../Includes/Shadows.glsl"

layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

// Inputs from vertex/tessellation shader
in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;
in vec4 vScreenPos;
in vec4 vReflectionPos;
in float vWaveHeight;
in float vWaveDx;
in float vWaveDz;

// Object ID for picking
uniform uint u_ObjectId;

// Time
uniform float u_Time;

// === WATER COLORS ===
uniform vec4 u_ShallowColor = vec4(0.1, 0.4, 0.5, 1.0);
uniform vec4 u_DeepColor = vec4(0.02, 0.08, 0.15, 1.0);
uniform vec4 u_HorizonColor = vec4(0.4, 0.6, 0.7, 1.0);
uniform float u_ColorDepthFade = 10.0;

// === FRESNEL ===
uniform float u_FresnelPower = 5.0;
uniform float u_FresnelBias = 0.04;
uniform float u_FresnelScale = 0.96;

// === SUBSURFACE SCATTERING ===
uniform int u_SSSEnabled = 1;
uniform vec3 u_SSSColor = vec3(0.0293, 0.0698, 0.1717);
uniform float u_SSSIntensity = 0.3;
uniform float u_SSSDistortion = 0.5;
uniform float u_SSSPower = 2.0;

// === CREST FOAM ===
uniform int u_CrestFoamEnabled = 1;
uniform float u_CrestFoamThreshold = 0.7;
uniform float u_CrestFoamIntensity = 1.0;
uniform vec4 u_CrestFoamColor = vec4(1.0, 1.0, 1.0, 0.8);
uniform float u_CrestFoamScale = 5.0;

// === SPECULAR ===
uniform float u_SpecularIntensity = 2.0;
uniform float u_SpecularPower = 720.0;
uniform float u_Roughness = 0.1;

// === REFLECTIONS ===
uniform int u_ReflectionEnabled = 1;
uniform float u_ReflectionIntensity = 1.0;
uniform float u_ReflectionDistortion = 0.05;
uniform int u_UsePlanarReflection = 1;
uniform sampler2D u_PlanarReflectionTex;
uniform mat4 u_ReflectionViewProj;
uniform int u_FlipReflectionX = 0; // Same as WaterForward: 0 = no flip X
uniform int u_FlipReflectionY = 0; // 0 = no flip Y

// === REFRACTION ===
uniform int u_RefractionEnabled = 1;
uniform float u_RefractionStrength = 0.1;
uniform float u_RefractionChromatic = 0.02;
uniform sampler2D u_SceneColorTex;

// === DEPTH ===
uniform sampler2D u_DepthTex;

// === CAUSTICS ===
uniform int u_CausticsEnabled = 1;
uniform float u_CausticsIntensity = 0.5;
uniform float u_CausticsScale = 1.0;
uniform float u_CausticsSpeed = 1.0;

// === ABSORPTION ===
uniform vec3 u_AbsorptionColor = vec3(0.4, 0.1, 0.02);
uniform float u_AbsorptionStrength = 0.5;

// === NORMAL DETAIL ===
uniform float u_NormalStrength = 1.0;
uniform int u_NormalIterations = 36;
uniform float u_NormalEpsilon = 0.01;

// === WAVE PARAMETERS (for normal calculation) ===
uniform int u_WaveIterations = 8;
uniform float u_WaveAmplitude = 1.0;
uniform float u_WaveFrequency = 1.0;
uniform float u_WaveSpeed = 2.0;
uniform float u_WaveDrag = 0.38;
uniform float u_WaveDepth = 1.0;
uniform vec2 u_WaveDirection = vec2(1.0, 0.3);

// === WEATHER INTEGRATION ===
uniform float u_WindStrength = 0.5;
uniform vec2 u_WindDirection = vec2(1.0, 0.0);
uniform float u_WindSpeed = 1.0;
uniform int u_WaveMode = 0;
uniform float u_WaveBlendFactor = 1.0;
uniform float u_WaveSpeed_Local = 2.0;
uniform float u_WaveAmplitude_Local = 1.0;
uniform vec2 u_WaveDirection_Local = vec2(1.0, 0.3);

// === SCREEN SIZE ===
uniform vec2 u_ScreenSize = vec2(1920.0, 1080.0);

// === HELPER FUNCTIONS ===

float getEffectiveWaveSpeed() {
    if (u_WaveMode == 0) return u_WaveSpeed * u_WindSpeed;
    if (u_WaveMode == 1) return u_WaveSpeed_Local;
    return mix(u_WaveSpeed_Local, u_WaveSpeed * u_WindSpeed, u_WaveBlendFactor);
}

float getEffectiveWaveAmplitude() {
    if (u_WaveMode == 0) return u_WaveAmplitude * (0.5 + u_WindStrength);
    if (u_WaveMode == 1) return u_WaveAmplitude_Local;
    float globalAmp = u_WaveAmplitude * (0.5 + u_WindStrength);
    return mix(u_WaveAmplitude_Local, globalAmp, u_WaveBlendFactor);
}

vec2 getEffectiveWaveDirection() {
    if (u_WaveMode == 0) return normalize(u_WindDirection);
    if (u_WaveMode == 1) return normalize(u_WaveDirection_Local);
    return normalize(mix(u_WaveDirection_Local, u_WindDirection, u_WaveBlendFactor));
}

// Hash function
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Gerstner wave for high-quality normal calculation
vec2 wavedx(vec2 position, vec2 direction, float frequency, float timeshift) {
    float x = dot(direction, position) * frequency + timeshift;
    float wave = exp(sin(x) - 1.0);
    float dx = wave * cos(x);
    return vec2(wave, -dx);
}

// Get wave height for normal calculation
float getWaveHeight(vec2 position, int iterations) {
    float wavePhaseShift = length(position) * 0.1;
    float iter = 0.0;
    float frequency = u_WaveFrequency;
    float timeMultiplier = getEffectiveWaveSpeed();
    float weight = 1.0;
    float sumOfValues = 0.0;
    float sumOfWeights = 0.0;
    
    vec2 waveDir = getEffectiveWaveDirection();
    float amplitude = getEffectiveWaveAmplitude();
    
    for(int i = 0; i < iterations && i < 48; i++) {
        float angle = iter * 0.5;
        vec2 dir = vec2(
            waveDir.x * cos(angle) - waveDir.y * sin(angle),
            waveDir.x * sin(angle) + waveDir.y * cos(angle)
        );
        dir = normalize(dir);
        
        vec2 res = wavedx(position, dir, frequency, u_Time * timeMultiplier + wavePhaseShift);
        position += dir * res.y * weight * u_WaveDrag;
        
        sumOfValues += res.x * weight;
        sumOfWeights += weight;
        
        weight = mix(weight, 0.0, 0.2);
        frequency *= 1.18;
        timeMultiplier *= 1.07;
        iter += 1232.399963;
    }
    
    return (sumOfValues / sumOfWeights) * amplitude * u_WaveDepth;
}

// High-quality normal calculation using wave sampling
vec3 calculateDetailedNormal(vec2 pos, float epsilon, int iterations) {
    float H = getWaveHeight(pos, iterations);
    vec3 a = vec3(pos.x, H, pos.y);
    
    float Hx = getWaveHeight(pos - vec2(epsilon, 0.0), iterations);
    float Hz = getWaveHeight(pos + vec2(0.0, epsilon), iterations);
    
    return normalize(cross(
        a - vec3(pos.x - epsilon, Hx, pos.y),
        a - vec3(pos.x, Hz, pos.y + epsilon)
    ));
}

// Fresnel calculation (Schlick approximation)
float calculateFresnel(vec3 N, vec3 V) {
    float cosTheta = max(0.0, dot(N, V));
    float fresnel = u_FresnelBias + u_FresnelScale * pow(1.0 - cosTheta, u_FresnelPower);
    return clamp(fresnel, 0.0, 1.0);
}

// Subsurface scattering approximation
vec3 calculateSSS(vec3 N, vec3 V, vec3 L, float waveHeight) {
    if (u_SSSEnabled == 0) return vec3(0.0);
    
    // Distorted light direction for SSS
    vec3 H = normalize(L + N * u_SSSDistortion);
    float VdotH = pow(saturate(dot(V, -H)), u_SSSPower);
    
    // Height-based intensity (more scattering at wave peaks)
    float heightFactor = 0.2 + (waveHeight / max(0.001, u_WaveDepth * getEffectiveWaveAmplitude()) + 1.0) * 0.5;
    heightFactor = saturate(heightFactor);
    
    return u_SSSColor * VdotH * u_SSSIntensity * heightFactor;
}

// Foam at wave crests
vec3 calculateCrestFoam(float waveHeight, vec2 uv) {
    if (u_CrestFoamEnabled == 0) return vec3(0.0);
    
    // Normalize wave height
    float maxHeight = u_WaveDepth * getEffectiveWaveAmplitude();
    float normalizedHeight = waveHeight / max(0.001, maxHeight);
    
    // Foam appears at high points
    float foamFactor = smoothstep(u_CrestFoamThreshold, 1.0, normalizedHeight);
    
    // Add some noise to foam pattern
    vec2 foamUV = uv * u_CrestFoamScale + u_Time * 0.1;
    float foamNoise = hash(floor(foamUV * 20.0)) * 0.3 + 0.7;
    
    // Foam texture approximation using noise
    float foamPattern = 0.0;
    for (int i = 0; i < 3; i++) {
        float scale = float(i + 1) * 2.0;
        foamPattern += hash(floor(foamUV * scale * 10.0 + u_Time * 0.5)) / scale;
    }
    foamPattern = saturate(foamPattern * 2.0);
    
    return u_CrestFoamColor.rgb * foamFactor * u_CrestFoamIntensity * foamPattern * u_CrestFoamColor.a;
}

// Caustics pattern
vec3 calculateCaustics(vec2 worldPos, float depth) {
    if (u_CausticsEnabled == 0 || depth < 0.01) return vec3(0.0);
    
    vec2 causticsUV = worldPos * u_CausticsScale;
    float time = u_Time * u_CausticsSpeed;
    
    // Multiple overlapping caustic patterns with chromatic separation
    float causticR = 0.0, causticG = 0.0, causticB = 0.0;
    
    for (int i = 0; i < 3; i++) {
        float scale = 1.0 + float(i) * 0.5;
        float offset = float(i) * 0.1;
        
        vec2 uv1 = causticsUV * scale + vec2(time * 0.3, time * 0.2);
        vec2 uv2 = causticsUV * scale * 1.3 - vec2(time * 0.4, -time * 0.3);
        
        float c1 = sin(uv1.x * 10.0 + time) * sin(uv1.y * 10.0 - time * 0.7);
        float c2 = sin(uv2.x * 8.0 - time * 0.5) * sin(uv2.y * 8.0 + time * 0.8);
        
        float c = pow(saturate((c1 + c2) * 0.5 + 0.5), 3.0);
        
        if (i == 0) causticR = c;
        else if (i == 1) causticG = c;
        else causticB = c;
    }
    
    // Fade with depth
    float depthFade = exp(-depth * 0.2);
    
    return vec3(causticR, causticG, causticB) * u_CausticsIntensity * depthFade;
}

// Atmosphere approximation for reflection
vec3 cheapAtmosphere(vec3 raydir, vec3 sundir) {
    float special_trick = 1.0 / (raydir.y * 1.0 + 0.1);
    float special_trick2 = 1.0 / (sundir.y * 11.0 + 1.0);
    float raysundt = pow(abs(dot(sundir, raydir)), 2.0);
    float sundt = pow(max(0.0, dot(sundir, raydir)), 8.0);
    float mymie = sundt * special_trick * 0.2;
    vec3 suncolor = mix(vec3(1.0), max(vec3(0.0), vec3(1.0) - vec3(5.5, 13.0, 22.4) / 22.4), special_trick2);
    vec3 bluesky = vec3(5.5, 13.0, 22.4) / 22.4 * suncolor;
    vec3 bluesky2 = max(vec3(0.0), bluesky - vec3(5.5, 13.0, 22.4) * 0.002 * (special_trick + -6.0 * sundir.y * sundir.y));
    bluesky2 *= special_trick * (0.24 + raysundt * 0.24);
    return bluesky2 * (1.0 + 1.0 * pow(1.0 - raydir.y, 3.0));
}

// Sun reflection
float getSun(vec3 dir, vec3 sundir) {
    return pow(max(0.0, dot(dir, sundir)), u_SpecularPower) * u_SpecularIntensity;
}

// Linearize depth
float linearizeDepth(float depth, float near, float far) {
    return (2.0 * near * far) / (far + near - depth * (far - near));
}

void main()
{
    vec3 N = normalize(vNormal);
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 L = normalize(-uDirLightDirection);
    
    // High-quality normal with detail
    if (u_NormalIterations > 0) {
        vec3 detailedN = calculateDetailedNormal(vWorldPos.xz, u_NormalEpsilon, u_NormalIterations);
        N = normalize(mix(N, detailedN, u_NormalStrength));
    }
    
    // Smooth normal with distance to reduce high-frequency noise
    float distToCamera = length(uCameraPos - vWorldPos);
    N = mix(N, vec3(0.0, 1.0, 0.0), 0.8 * min(1.0, sqrt(distToCamera * 0.01) * 1.1));
    
    // Screen UV for depth/refraction
    vec2 screenUV = (vScreenPos.xy / vScreenPos.w) * 0.5 + 0.5;
    screenUV = clamp(screenUV, 0.001, 0.999);
    
    // === DEPTH CALCULATION ===
    float waterDepth = gl_FragCoord.z;
    float sceneDepth = texture(u_DepthTex, screenUV).r;
    
    float near = 0.1;
    float far = 1000.0;
    float linearSceneDepth = linearizeDepth(sceneDepth, near, far);
    float linearWaterDepth = linearizeDepth(waterDepth, near, far);
    float depthDiff = max(0.0, linearSceneDepth - linearWaterDepth);
    
    // === FRESNEL ===
    float fresnel = calculateFresnel(N, V);
    
    // === WATER COLOR ===
    float depthFactor = saturate(depthDiff / u_ColorDepthFade);
    vec3 waterColor = mix(u_ShallowColor.rgb, u_DeepColor.rgb, depthFactor);
    
    // Horizon tinting based on view angle
    float horizonFactor = pow(1.0 - saturate(dot(V, vec3(0.0, 1.0, 0.0))), 2.0);
    waterColor = mix(waterColor, u_HorizonColor.rgb, horizonFactor * 0.3);
    
    // Absorption
    float absorptionFactor = exp(-depthDiff * u_AbsorptionStrength);
    waterColor *= mix(u_AbsorptionColor, vec3(1.0), absorptionFactor);
    
    // === SUBSURFACE SCATTERING ===
    vec3 sss = calculateSSS(N, V, L, vWaveHeight);
    
    // === REFLECTION ===
    vec3 R = reflect(-V, N);
    R.y = abs(R.y); // Force reflection to look up
    
    vec3 reflectionColor = vec3(0.0);
    
    if (u_ReflectionEnabled > 0) {
        if (u_UsePlanarReflection > 0) {
            // Planar reflection
            vec3 reflectionNDC = vReflectionPos.xyz / vReflectionPos.w;
            vec2 reflectionUV = reflectionNDC.xy * 0.5 + 0.5;
            
            // Apply flips for proper mirror effect
            if (u_FlipReflectionX != 0)
                reflectionUV.x = 1.0 - reflectionUV.x;
            if (u_FlipReflectionY != 0)
                reflectionUV.y = 1.0 - reflectionUV.y;
            
            // Distort reflection UVs based on normal
            reflectionUV += N.xz * u_ReflectionDistortion;
            
            // Edge fade
            float fadeMargin = 0.3;
            vec2 fadeFactors = vec2(
                smoothstep(-fadeMargin, 0.0, reflectionUV.x) * smoothstep(1.0 + fadeMargin, 1.0, reflectionUV.x),
                smoothstep(-fadeMargin, 0.0, reflectionUV.y) * smoothstep(1.0 + fadeMargin, 1.0, reflectionUV.y)
            );
            float edgeFade = fadeFactors.x * fadeFactors.y;
            
            reflectionUV = clamp(reflectionUV, 0.0, 1.0);
            vec3 planarReflection = texture(u_PlanarReflectionTex, reflectionUV).rgb;
            
            // Fallback to IBL at edges
            vec3 iblReflection = samplePrefilteredEnv(R, u_Roughness);
            reflectionColor = mix(iblReflection, planarReflection, edgeFade);
        } else {
            // IBL reflection only
            reflectionColor = samplePrefilteredEnv(R, u_Roughness);
        }
        
        // Add atmosphere
        vec3 atmosphere = cheapAtmosphere(R, L) * 0.5;
        reflectionColor += atmosphere;
        
        reflectionColor *= u_ReflectionIntensity;
    }
    
    // === SUN SPECULAR ===
    float sunReflection = getSun(R, L);
    vec3 sunColor = uDirLightColor * sunReflection * uDirLightIntensity;
    
    // === REFRACTION ===
    vec3 refractionColor = waterColor;
    
    if (u_RefractionEnabled > 0 && depthDiff > 0.01) {
        vec2 refractionUV = screenUV + N.xz * u_RefractionStrength;
        refractionUV = clamp(refractionUV, 0.001, 0.999);
        
        // Chromatic aberration
        if (u_RefractionChromatic > 0.001) {
            vec2 chromaticOffset = N.xz * u_RefractionChromatic;
            float r = texture(u_SceneColorTex, refractionUV + chromaticOffset).r;
            float g = texture(u_SceneColorTex, refractionUV).g;
            float b = texture(u_SceneColorTex, refractionUV - chromaticOffset).b;
            refractionColor = vec3(r, g, b);
        } else {
            refractionColor = texture(u_SceneColorTex, refractionUV).rgb;
        }
        
        // Tint refraction with water color based on depth
        refractionColor = mix(refractionColor, waterColor, depthFactor * 0.5);
    }
    
    // === CAUSTICS ===
    vec3 caustics = calculateCaustics(vWorldPos.xz, depthDiff);
    
    // === CREST FOAM ===
    vec3 crestFoam = calculateCrestFoam(vWaveHeight, vUV);
    
    // === SHADOWS ===
    float shadow = 1.0;
    // Disable shadows for now to avoid potential issues
    // if (u_UseShadows > 0) {
    //     shadow = calculateShadow(vWorldPos, N);
    // }
    
    // === COMBINE ===
    // Base: mix refraction and water color
    vec3 finalColor = mix(refractionColor, waterColor, 0.3);
    
    // Add subsurface scattering
    finalColor += sss * shadow * uDirLightIntensity;
    
    // Add caustics
    finalColor += caustics * shadow;
    
    // Mix with reflection based on Fresnel
    finalColor = mix(finalColor, reflectionColor, fresnel);
    
    // Add sun specular
    finalColor += sunColor * shadow;
    
    // Add crest foam on top
    float foamMask = length(crestFoam);
    finalColor = mix(finalColor, finalColor + crestFoam, saturate(foamMask));
    
    // === FOG ===
    finalColor = processFog(finalColor, vWorldPos);
    
    // === OUTPUT ===
    float alpha = saturate(u_ShallowColor.a + fresnel * 0.5);
    
    // DEBUG: Uncomment to test basic rendering
    // outColor = vec4(0.2, 0.5, 0.8, 1.0); // Solid blue
    // outId = u_ObjectId;
    // return;
    
    outColor = vec4(finalColor, alpha);
    outId = u_ObjectId;
}
