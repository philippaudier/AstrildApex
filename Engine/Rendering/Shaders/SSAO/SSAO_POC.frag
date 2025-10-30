```plaintext
#version 330 core

in vec2 vTexCoord;

out float outSSAO;

uniform sampler2D u_PositionTex;
uniform sampler2D u_NormalTex;

uniform float u_SSAORadius;
uniform float u_SSAOBias;
uniform int u_SSAOSamples; // expected small (e.g., 8)
uniform mat4 u_ProjMatrix;

const float EPS = 0.0001;

void main(){
    vec3 fragPos = texture(u_PositionTex, vTexCoord).xyz;
    vec3 normal = normalize(texture(u_NormalTex, vTexCoord).xyz);

    if (length(fragPos) < EPS) { outSSAO = 1.0; return; }

    float occlusion = 0.0;
    // Very simple orientation: sample points uniformly in a fixed set around normal
    // We'll use a tiny fixed kernel in tangent space for POC
    vec3 sampleKernel[8];
    sampleKernel[0] = vec3( 0.0, 0.0, 1.0);
    sampleKernel[1] = vec3( 0.5, 0.5, 0.5);
    sampleKernel[2] = vec3(-0.5, 0.5, 0.5);
    sampleKernel[3] = vec3( 0.5,-0.5, 0.5);
    sampleKernel[4] = vec3(-0.5,-0.5, 0.5);
    sampleKernel[5] = vec3( 0.2, 0.2, 0.8);
    sampleKernel[6] = vec3(-0.2, 0.2, 0.8);
    sampleKernel[7] = vec3( 0.0, 0.6, 0.6);

    // Build simple TBN: choose arbitrary tangent
    vec3 up = abs(normal.z) < 0.999 ? vec3(0.0,0.0,1.0) : vec3(0.0,1.0,0.0);
    vec3 tangent = normalize(cross(up, normal));
    vec3 bitangent = cross(normal, tangent);
    mat3 TBN = mat3(tangent, bitangent, normal);

    for (int i = 0; i < u_SSAOSamples && i < 8; ++i)
    {
        vec3 sampleVec = normalize(TBN * sampleKernel[i]);
        vec3 samplePos = fragPos + sampleVec * u_SSAORadius;

        vec4 offset = u_ProjMatrix * vec4(samplePos, 1.0);
        offset.xyz /= offset.w;
        vec2 sampleUV = offset.xy * 0.5 + 0.5;

        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0) continue;

        vec3 sampleActual = texture(u_PositionTex, sampleUV).xyz;
        float rangeCheck = smoothstep(0.0, 1.0, u_SSAORadius / (abs(samplePos.z - sampleActual.z) + 0.001));

        if (sampleActual.z < samplePos.z - u_SSAOBias) occlusion += rangeCheck;
    }

    occlusion = occlusion / float(u_SSAOSamples);
    outSSAO = 1.0 - occlusion; // invert: 1 = no occlusion, 0 = full
}
```
