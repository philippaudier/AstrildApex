#version 330 core
#include "../Includes/Common.glsl"

// Vertex attributes
layout (location = 0) in vec3 a_Position;
layout (location = 1) in vec3 a_Normal;
layout (location = 2) in vec2 a_UV;
layout (location = 3) in vec4 a_SplatWeights;    // RGBA weights for up to 4 terrain layers
layout (location = 4) in uint a_SplatIndices;    // Packed layer indices

// Transform matrices (terrain-specific, compatible with forward pipeline)
uniform mat4 u_Model;
uniform mat3 u_NormalMat;

// Output to fragment shader
out vec3 v_WorldPos;
out vec3 v_ViewPos;
out vec3 v_Normal;
out vec2 v_UV;
out vec4 v_SplatWeights;
flat out uvec4 v_SplatIndices; // Unpacked layer indices
out float v_Height; // World height for height-based effects

void main()
{
    vec4 worldPos = u_Model * vec4(a_Position, 1.0);
    v_WorldPos = worldPos.xyz;
    v_ViewPos = (uView * worldPos).xyz;
    v_Height = worldPos.y; // Store world height for layer blending

    v_Normal = normalize(u_NormalMat * a_Normal);
    v_UV = a_UV;
    v_SplatWeights = a_SplatWeights;

    // Unpack layer indices from uint (support up to 8 layers)
    v_SplatIndices.x = (a_SplatIndices) & 0xFFu;
    v_SplatIndices.y = (a_SplatIndices >> 8u) & 0xFFu;
    v_SplatIndices.z = (a_SplatIndices >> 16u) & 0xFFu;
    v_SplatIndices.w = (a_SplatIndices >> 24u) & 0xFFu;

    gl_Position = uViewProj * worldPos;
}