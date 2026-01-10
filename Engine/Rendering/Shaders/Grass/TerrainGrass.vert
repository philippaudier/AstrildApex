#version 420 core

#include "../Includes/Common.glsl"

// Vertex attributes (from terrain mesh)
layout(location=0) in vec3 aPos;      // Vertex position
layout(location=1) in vec3 aNormal;   // Vertex normal
layout(location=2) in vec2 aUV;       // Vertex UV (unused for grass)

// Pass-through to geometry shader
out VS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;
} vs_out;

uniform mat4 u_Model;        // Terrain model matrix
uniform mat3 u_NormalMat;    // Normal matrix

void main()
{
    // Transform vertex to world space
    vec4 worldPos = u_Model * vec4(aPos, 1.0);
    vs_out.worldPos = worldPos.xyz;
    
    // Transform normal to world space
    vs_out.normal = normalize(u_NormalMat * aNormal);
    vs_out.uv = aUV;
    
    // Don't output gl_Position here - geometry shader will do it
    gl_Position = worldPos; // Pass world position for geometry shader
}
