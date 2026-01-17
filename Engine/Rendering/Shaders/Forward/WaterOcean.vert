#version 420 core

#include "../Includes/Common.glsl"

// Vertex attributes
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

// Standard uniforms
uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

// Time
uniform float u_Time;

// === GERSTNER WAVE PARAMETERS ===
uniform int u_WaveIterations = 8;
uniform float u_WaveAmplitude = 1.0;
uniform float u_WaveFrequency = 1.0;
uniform float u_WaveSpeed = 2.0;
uniform float u_WaveSteepness = 0.5;
uniform float u_WaveDrag = 0.38;
uniform float u_WaveDepth = 1.0;
uniform vec2 u_WaveDirection = vec2(1.0, 0.3);

// === FBM DETAIL ===
uniform int u_FbmEnabled = 1;
uniform int u_FbmOctaves = 4;
uniform float u_FbmAmplitude = 0.1;
uniform float u_FbmFrequency = 2.0;
uniform float u_FbmLacunarity = 2.0;
uniform float u_FbmPersistence = 0.5;

// === GLOBAL WIND/WEATHER PARAMETERS (from WeatherComponent) ===
uniform float u_WindStrength = 0.5;
uniform vec2 u_WindDirection = vec2(1.0, 0.0);
uniform float u_WindSpeed = 1.0;
uniform float u_WindGustiness = 0.0;

// === GLOBAL/LOCAL/BLEND SYSTEM ===
uniform int u_WaveMode = 0;       // 0 = Global, 1 = Local, 2 = Blend
uniform float u_WaveBlendFactor = 1.0;

// === LOCAL PARAMETERS ===
uniform float u_WaveSpeed_Local = 2.0;
uniform float u_WaveAmplitude_Local = 1.0;
uniform vec2 u_WaveDirection_Local = vec2(1.0, 0.3);

// Planar reflection matrix
uniform mat4 u_ReflectionViewProj;

// Outputs to fragment shader (or tessellation)
out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUV;
out vec4 vScreenPos;
out vec4 vReflectionPos;
out float vWaveHeight;
out float vWaveDx;
out float vWaveDz;

// === HELPER FUNCTIONS ===

float getEffectiveWaveSpeed() {
    if (u_WaveMode == 0) return u_WaveSpeed * u_WindSpeed;  // Global
    if (u_WaveMode == 1) return u_WaveSpeed_Local;          // Local
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

// Hash function for pseudo-random values
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Gerstner wave calculation
// Based on GPU Gems Chapter 1 and Shadertoy by afl_ext
// Returns: (wave height, derivative x, derivative z)
vec2 wavedx(vec2 position, vec2 direction, float frequency, float timeshift) {
    float x = dot(direction, position) * frequency + timeshift;
    float wave = exp(sin(x) - 1.0);
    float dx = wave * cos(x);
    return vec2(wave, -dx);
}

// Multi-octave Gerstner wave summation
vec3 getGerstnerWaves(vec2 position, float time, int iterations) {
    float wavePhaseShift = length(position) * 0.1;
    float iter = 0.0;
    float frequency = u_WaveFrequency;
    float timeMultiplier = getEffectiveWaveSpeed();
    float weight = 1.0;
    float sumOfValues = 0.0;
    float sumOfWeights = 0.0;
    vec2 sumOfDerivatives = vec2(0.0);
    
    vec2 waveDir = getEffectiveWaveDirection();
    float amplitude = getEffectiveWaveAmplitude();
    
    for(int i = 0; i < iterations && i < 16; i++) {
        // Generate wave direction with variation
        float angle = iter * 0.5;
        vec2 dir = vec2(
            waveDir.x * cos(angle) - waveDir.y * sin(angle),
            waveDir.x * sin(angle) + waveDir.y * cos(angle)
        );
        dir = normalize(dir);
        
        // Calculate wave data
        vec2 res = wavedx(position, dir, frequency, time * timeMultiplier + wavePhaseShift);
        
        // Shift position by wave drag
        position += dir * res.y * weight * u_WaveDrag;
        
        // Accumulate
        sumOfValues += res.x * weight;
        sumOfDerivatives += dir * res.y * weight;
        sumOfWeights += weight;
        
        // Next octave
        weight = mix(weight, 0.0, 0.2);
        frequency *= 1.18;
        timeMultiplier *= 1.07;
        iter += 1232.399963;
    }
    
    float height = (sumOfValues / sumOfWeights) * amplitude * u_WaveDepth;
    vec2 derivatives = (sumOfDerivatives / sumOfWeights) * amplitude;
    
    return vec3(height, derivatives.x, derivatives.y);
}

// Simple FBM noise for additional detail
float noise2D(vec2 p) {
    vec2 ip = floor(p);
    vec2 fp = fract(p);
    fp = fp * fp * (3.0 - 2.0 * fp);
    
    float a = hash(ip);
    float b = hash(ip + vec2(1.0, 0.0));
    float c = hash(ip + vec2(0.0, 1.0));
    float d = hash(ip + vec2(1.0, 1.0));
    
    return mix(mix(a, b, fp.x), mix(c, d, fp.x), fp.y) * 2.0 - 1.0;
}

float fbm(vec2 p, int octaves) {
    float value = 0.0;
    float amplitude = u_FbmAmplitude;
    float frequency = u_FbmFrequency;
    
    for (int i = 0; i < octaves && i < 8; i++) {
        value += amplitude * noise2D(p * frequency);
        amplitude *= u_FbmPersistence;
        frequency *= u_FbmLacunarity;
    }
    
    return value;
}

void main()
{
    // Transform to world space
    vec4 worldPos = u_Model * vec4(aPos, 1.0);
    
    // Get Gerstner wave displacement
    vec3 waveData = getGerstnerWaves(worldPos.xz, u_Time, u_WaveIterations);
    float waveHeight = waveData.x;
    float waveDx = waveData.y;
    float waveDz = waveData.z;
    
    // Add FBM detail if enabled
    if (u_FbmEnabled > 0) {
        float fbmValue = fbm(worldPos.xz + u_Time * 0.1, u_FbmOctaves);
        waveHeight += fbmValue * getEffectiveWaveAmplitude() * 0.2;
    }
    
    // Apply wave displacement
    worldPos.y += waveHeight;
    
    // Calculate normal from wave derivatives
    // cross(tangentZ, tangentX) gives up-pointing normal in right-handed coordinate system
    vec3 tangentX = vec3(1.0, waveDx, 0.0);
    vec3 tangentZ = vec3(0.0, waveDz, 1.0);
    vec3 normal = normalize(cross(tangentZ, tangentX));
    
    // Output
    vWorldPos = worldPos.xyz;
    vNormal = normal;
    vUV = aUV * u_TextureTiling + u_TextureOffset;
    vWaveHeight = waveHeight;
    vWaveDx = waveDx;
    vWaveDz = waveDz;
    
    // Screen position for depth/refraction
    gl_Position = uViewProj * worldPos;
    vScreenPos = gl_Position;
    
    // Reflection position
    vReflectionPos = u_ReflectionViewProj * worldPos;
}
