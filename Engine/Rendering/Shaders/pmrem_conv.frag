#version 330 core

in vec3 vWorldPos;
layout(location=0) out vec3 FragColor;

uniform samplerCube u_EnvMap;
uniform int u_SampleCount; // e.g. 64; if <=0 use prefiltered LOD sampling
uniform float u_PrefilterMaxLod;

const float PI = 3.14159265359;

// Hammersley sequence and importance sampling helpers
float RadicalInverse_VdC(uint bits)
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10; // / 0x100000000
}

vec2 Hammersley(uint i, uint N)
{
    return vec2(float(i) / float(N), RadicalInverse_VdC(i));
}

vec3 ImportanceSampleHemisphere(vec2 Xi, vec3 N)
{
    float phi = 2.0 * PI * Xi.x;
    float cosTheta = sqrt(1.0 - Xi.y);
    float sinTheta = sqrt(1.0 - cosTheta * cosTheta);

    // spherical to cartesian
    vec3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    // create TBN for N
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);

    return tangent * H.x + bitangent * H.y + N * H.z;
}

void main()
{
    vec3 N = normalize(vWorldPos);
    vec3 irradiance = vec3(0.0);
    
    if (u_SampleCount <= 0)
    {
        // Approximate irradiance by sampling a very high LOD of the environment to get blurred result
        float lod = max(0.0, u_PrefilterMaxLod - 0.5);
        FragColor = textureLod(u_EnvMap, N, lod).rgb;
        return;
    }

    uint sampleCount = uint(max(1, u_SampleCount));

    // CRITICAL FIX: To eliminate white spots from sun/bright lights in HDR,
    // we need to sample a HEAVILY blurred version of the environment map.
    //
    // Query the cubemap size and calculate max LOD, then sample at a very high LOD
    // to get ultra-smooth irradiance (eliminates all high-frequency content).
    //
    // This is the industry-standard approach: Unreal/Unity both use heavily
    // pre-filtered environment maps for diffuse irradiance to avoid aliasing.
    ivec2 envSize = textureSize(u_EnvMap, 0);
    float maxLod = log2(float(max(envSize.x, envSize.y)));

    // Sample at MAXIMUM LOD minus 1 for ultra-diffuse irradiance
    // This gives us the most blurred representation possible
    float sampleLod = max(0.0, maxLod - 1.0);

    // Simple cosine-weighted hemisphere sampling
    for (uint i = 0u; i < sampleCount; ++i)
    {
        vec2 Xi = Hammersley(i, sampleCount);
        vec3 sampleDir = ImportanceSampleHemisphere(Xi, N);
        float cosTerm = max(0.0, dot(sampleDir, N));
        // Sample the heavily pre-filtered (ultra-blurred) environment map
        vec3 envColor = textureLod(u_EnvMap, sampleDir, sampleLod).rgb;
        irradiance += envColor * cosTerm;
    }

    irradiance = irradiance / float(sampleCount);
    FragColor = irradiance * PI; // scale by PI for diffuse convolution
}
