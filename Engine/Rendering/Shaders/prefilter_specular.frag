#version 330 core
in vec3 vWorldPos;
layout(location=0) out vec3 FragColor;

uniform samplerCube u_EnvMap;
uniform float u_Roughness;
uniform int u_SampleCount;
const float PI = 3.14159265359;

// Hammersley and importance sample GGX
float RadicalInverse_VdC(uint bits)
{
    bits = (bits << 16u) | (bits >> 16u);
    bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
    bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
    bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
    bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
    return float(bits) * 2.3283064365386963e-10;
}

vec2 Hammersley(uint i, uint N)
{
    return vec2(float(i) / float(N), RadicalInverse_VdC(i));
}

// GGX importance sampling
vec3 ImportanceSampleGGX(vec2 Xi, vec3 N, float roughness)
{
    float a = roughness * roughness;

    float phi = 2.0 * PI * Xi.x;
    float cosTheta = sqrt((1.0 - Xi.y) / (1.0 + (a*a - 1.0) * Xi.y));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));

    vec3 H;
    H.x = cos(phi) * sinTheta;
    H.y = sin(phi) * sinTheta;
    H.z = cosTheta;

    // transform H to world space (TBN)
    vec3 up = abs(N.z) < 0.999 ? vec3(0.0, 0.0, 1.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, N));
    vec3 bitangent = cross(N, tangent);

    vec3 sampleVec = tangent * H.x + bitangent * H.y + N * H.z;
    return normalize(sampleVec);
}

void main()
{
    vec3 N = normalize(vWorldPos);
    vec3 R = N;
    vec3 prefilteredColor = vec3(0.0);
    float totalWeight = 0.0;
    uint sampleCount = uint(max(1, u_SampleCount));

    for (uint i = 0u; i < sampleCount; ++i)
    {
        vec2 Xi = Hammersley(i, sampleCount);
        vec3 H = ImportanceSampleGGX(Xi, N, u_Roughness);
        vec3 L = normalize(2.0 * dot(R, H) * H - R);

        float NdotL = max(0.0, dot(N, L));
        if (NdotL > 0.0)
        {
            vec3 sampleColor = texture(u_EnvMap, L).rgb;
            // Lob: simple weight by NdotL (importance sampling approximation)
            prefilteredColor += sampleColor * NdotL;
            totalWeight += NdotL;
        }
    }
    prefilteredColor = prefilteredColor / max(1.0, totalWeight);
    FragColor = prefilteredColor;
}

