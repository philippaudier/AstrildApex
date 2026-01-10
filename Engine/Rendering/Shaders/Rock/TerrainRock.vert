#version 420 core

// Terrain vertex attributes
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

#include "../Includes/Common.glsl"

uniform mat4 u_Model;
uniform mat3 u_NormalMat;

// Output to geometry shader
out VS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;
} vs_out;

void main()
{
    // Transform to world space
    vec4 worldPos = u_Model * vec4(aPosition, 1.0);
    vs_out.worldPos = worldPos.xyz;
    vs_out.normal = normalize(u_NormalMat * aNormal);
    vs_out.uv = aTexCoord;
    
    gl_Position = worldPos; // Geometry shader will transform to clip space
}
