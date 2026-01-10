#version 420 core

// Tessellation Control Shader for WaterOcean
// Adaptive tessellation based on distance to camera

#include "../Includes/Common.glsl"

layout(vertices = 3) out;

// Inputs from vertex shader
in vec3 vWorldPos[];
in vec3 vNormal[];
in vec2 vUV[];
in vec4 vScreenPos[];
in vec4 vReflectionPos[];
in float vWaveHeight[];
in float vWaveDx[];
in float vWaveDz[];

// Outputs to tessellation evaluation shader
out vec3 tcWorldPos[];
out vec3 tcNormal[];
out vec2 tcUV[];
out vec4 tcScreenPos[];
out vec4 tcReflectionPos[];
out float tcWaveHeight[];
out float tcWaveDx[];
out float tcWaveDz[];

// Tessellation parameters
uniform float u_TessellationFactor = 8.0;
uniform float u_TessellationMinDistance = 10.0;
uniform float u_TessellationMaxDistance = 200.0;
uniform float u_TessellationMinLevel = 1.0;
uniform float u_TessellationMaxLevel = 64.0;

// LOD parameters
uniform int u_LodEnabled = 1;
uniform float u_LodDistance1 = 50.0;
uniform float u_LodDistance2 = 150.0;
uniform float u_LodDistance3 = 300.0;

// Calculate tessellation level based on distance
float getTessLevel(float distance) {
    if (u_LodEnabled == 0) {
        return u_TessellationFactor;
    }
    
    // Clamp distance to range
    float t = clamp(
        (distance - u_TessellationMinDistance) / 
        (u_TessellationMaxDistance - u_TessellationMinDistance),
        0.0, 1.0
    );
    
    // Interpolate between max and min tessellation levels
    float level = mix(u_TessellationMaxLevel, u_TessellationMinLevel, t);
    
    // Apply base factor
    level *= u_TessellationFactor / 8.0;
    
    return clamp(level, 1.0, 64.0);
}

// Calculate edge tessellation based on edge midpoint distance
float getEdgeTessLevel(vec3 p0, vec3 p1) {
    vec3 midpoint = (p0 + p1) * 0.5;
    float distance = length(uCameraPos - midpoint);
    return getTessLevel(distance);
}

void main()
{
    // Pass through vertex data
    tcWorldPos[gl_InvocationID] = vWorldPos[gl_InvocationID];
    tcNormal[gl_InvocationID] = vNormal[gl_InvocationID];
    tcUV[gl_InvocationID] = vUV[gl_InvocationID];
    tcScreenPos[gl_InvocationID] = vScreenPos[gl_InvocationID];
    tcReflectionPos[gl_InvocationID] = vReflectionPos[gl_InvocationID];
    tcWaveHeight[gl_InvocationID] = vWaveHeight[gl_InvocationID];
    tcWaveDx[gl_InvocationID] = vWaveDx[gl_InvocationID];
    tcWaveDz[gl_InvocationID] = vWaveDz[gl_InvocationID];
    
    // Only first invocation sets tessellation levels
    if (gl_InvocationID == 0)
    {
        // Calculate edge tessellation levels based on edge midpoint distances
        float e0 = getEdgeTessLevel(vWorldPos[1], vWorldPos[2]); // Edge opposite to vertex 0
        float e1 = getEdgeTessLevel(vWorldPos[2], vWorldPos[0]); // Edge opposite to vertex 1
        float e2 = getEdgeTessLevel(vWorldPos[0], vWorldPos[1]); // Edge opposite to vertex 2
        
        // Calculate inner tessellation as average
        float inner = (e0 + e1 + e2) / 3.0;
        
        // Set outer tessellation levels (for triangle edges)
        gl_TessLevelOuter[0] = e0;
        gl_TessLevelOuter[1] = e1;
        gl_TessLevelOuter[2] = e2;
        
        // Set inner tessellation level
        gl_TessLevelInner[0] = inner;
    }
}
