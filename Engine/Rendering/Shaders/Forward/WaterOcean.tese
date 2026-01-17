#version 420 core

// Tessellation Evaluation Shader for WaterOcean
// Recalculates wave displacement for tessellated vertices

#include "../Includes/Common.glsl"

layout(triangles, equal_spacing, cw) in;

// Inputs from tessellation control shader
in vec3 tcWorldPos[];
in vec3 tcNormal[];
in vec2 tcUV[];
in vec4 tcScreenPos[];
in vec4 tcReflectionPos[];
in float tcWaveHeight[];
in float tcWaveDx[];
in float tcWaveDz[];

// Outputs to fragment shader
out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUV;
out vec4 vScreenPos;
out vec4 vReflectionPos;
out float vWaveHeight;
out float vWaveDx;
out float vWaveDz;

// Time
uniform float u_Time;

// Wave parameters (same as vertex shader)
uniform int u_WaveIterations = 8;
uniform float u_WaveAmplitude = 1.0;
uniform float u_WaveFrequency = 1.0;
uniform float u_WaveSpeed = 2.0;
uniform float u_WaveSteepness = 0.5;
uniform float u_WaveDrag = 0.38;
uniform float u_WaveDepth = 1.0;
uniform vec2 u_WaveDirection = vec2(1.0, 0.3);

// FBM parameters
uniform int u_FbmEnabled = 1;
uniform int u_FbmOctaves = 4;
uniform float u_FbmAmplitude = 0.1;
uniform float u_FbmFrequency = 2.0;
uniform float u_FbmLacunarity = 2.0;
uniform float u_FbmPersistence = 0.5;

// Weather integration
uniform float u_WindStrength = 0.5;
uniform vec2 u_WindDirection = vec2(1.0, 0.0);
uniform float u_WindSpeed = 1.0;
uniform int u_WaveMode = 0;
uniform float u_WaveBlendFactor = 1.0;
uniform float u_WaveSpeed_Local = 2.0;
uniform float u_WaveAmplitude_Local = 1.0;
uniform vec2 u_WaveDirection_Local = vec2(1.0, 0.3);

// Reflection matrix
uniform mat4 u_ReflectionViewProj;

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

// Gerstner wave
vec2 wavedx(vec2 position, vec2 direction, float frequency, float timeshift) {
    float x = dot(direction, position) * frequency + timeshift;
    float wave = exp(sin(x) - 1.0);
    float dx = wave * cos(x);
    return vec2(wave, -dx);
}

// Multi-octave Gerstner waves
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
        float angle = iter * 0.5;
        vec2 dir = vec2(
            waveDir.x * cos(angle) - waveDir.y * sin(angle),
            waveDir.x * sin(angle) + waveDir.y * cos(angle)
        );
        dir = normalize(dir);
        
        vec2 res = wavedx(position, dir, frequency, time * timeMultiplier + wavePhaseShift);
        position += dir * res.y * weight * u_WaveDrag;
        
        sumOfValues += res.x * weight;
        sumOfDerivatives += dir * res.y * weight;
        sumOfWeights += weight;
        
        weight = mix(weight, 0.0, 0.2);
        frequency *= 1.18;
        timeMultiplier *= 1.07;
        iter += 1232.399963;
    }
    
    float height = (sumOfValues / sumOfWeights) * amplitude * u_WaveDepth;
    vec2 derivatives = (sumOfDerivatives / sumOfWeights) * amplitude;
    
    return vec3(height, derivatives.x, derivatives.y);
}

// FBM noise
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

// Interpolate attributes using barycentric coordinates
vec3 interpolate3(vec3 v0, vec3 v1, vec3 v2) {
    return gl_TessCoord.x * v0 + gl_TessCoord.y * v1 + gl_TessCoord.z * v2;
}

vec2 interpolate2(vec2 v0, vec2 v1, vec2 v2) {
    return gl_TessCoord.x * v0 + gl_TessCoord.y * v1 + gl_TessCoord.z * v2;
}

vec4 interpolate4(vec4 v0, vec4 v1, vec4 v2) {
    return gl_TessCoord.x * v0 + gl_TessCoord.y * v1 + gl_TessCoord.z * v2;
}

float interpolate1(float v0, float v1, float v2) {
    return gl_TessCoord.x * v0 + gl_TessCoord.y * v1 + gl_TessCoord.z * v2;
}

void main()
{
    // Interpolate base position (without wave displacement)
    vec3 basePos = interpolate3(tcWorldPos[0], tcWorldPos[1], tcWorldPos[2]);
    
    // Subtract interpolated wave height to get flat position
    float interpWaveHeight = interpolate1(tcWaveHeight[0], tcWaveHeight[1], tcWaveHeight[2]);
    basePos.y -= interpWaveHeight;
    
    // Recalculate waves at the new tessellated position
    vec3 waveData = getGerstnerWaves(basePos.xz, u_Time, u_WaveIterations);
    float waveHeight = waveData.x;
    float waveDx = waveData.y;
    float waveDz = waveData.z;
    
    // Add FBM detail if enabled
    if (u_FbmEnabled > 0) {
        float fbmValue = fbm(basePos.xz + u_Time * 0.1, u_FbmOctaves);
        waveHeight += fbmValue * getEffectiveWaveAmplitude() * 0.2;
    }
    
    // Apply displacement
    vec3 worldPos = basePos;
    worldPos.y += waveHeight;
    
    // Calculate normal from derivatives
    // cross(tangentZ, tangentX) gives up-pointing normal in right-handed coordinate system
    vec3 tangentX = vec3(1.0, waveDx, 0.0);
    vec3 tangentZ = vec3(0.0, waveDz, 1.0);
    vec3 normal = normalize(cross(tangentZ, tangentX));
    
    // Interpolate UV
    vUV = interpolate2(tcUV[0], tcUV[1], tcUV[2]);
    
    // Output
    vWorldPos = worldPos;
    vNormal = normal;
    vWaveHeight = waveHeight;
    vWaveDx = waveDx;
    vWaveDz = waveDz;
    
    // Calculate screen position
    vec4 clipPos = uViewProj * vec4(worldPos, 1.0);
    gl_Position = clipPos;
    vScreenPos = clipPos;
    
    // Calculate reflection position
    vReflectionPos = u_ReflectionViewProj * vec4(worldPos, 1.0);
}
