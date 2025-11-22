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

    // PROPER IRRADIANCE CONVOLUTION with heavily pre-filtered sampling
    // We MUST do a proper hemisphere convolution, not just sample the skybox directly,
    // because irradiance requires integrating over the hemisphere with cosine weighting.

    // LEGACY CODE BELOW (disabled for now - use simple max-LOD sampling above)
    if (u_SampleCount <= 0)
    {
        // Approximate irradiance by sampling a very high LOD of the environment to get blurred result
        float lod = max(0.0, u_PrefilterMaxLod - 0.5);
        FragColor = textureLod(u_EnvMap, N, lod).rgb;
        return;
    }

    uint sampleCount = uint(max(1, u_SampleCount));

    // CRITICAL: Sample at VERY HIGH LOD to eliminate sun/bright light spots
    // Use 95% of max LOD for ULTRA-smooth irradiance (e.g., LOD 10 → sample at LOD 9.5)
    // This samples at almost the smallest mip (2x2 or 1x1) for maximum blur
    float sampleLod = u_PrefilterMaxLod * 0.95;

    // Cosine-weighted hemisphere convolution with heavily pre-filtered sampling
    vec3 irradiance = vec3(0.0);
    for (uint i = 0u; i < sampleCount; ++i)
    {
        vec2 Xi = Hammersley(i, sampleCount);
        vec3 sampleDir = ImportanceSampleHemisphere(Xi, N);
        float NdotL = max(0.0, dot(sampleDir, N));

        // Sample pre-filtered environment at high LOD to eliminate sharp features
        vec3 envColor = textureLod(u_EnvMap, sampleDir, sampleLod).rgb;
        irradiance += envColor * NdotL;
    }

    irradiance = irradiance * PI / float(sampleCount);
    FragColor = irradiance;
}
