#version 420 core

#include "../Includes/Common.glsl"

layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;

out vec3 v_WorldPos;
out vec3 v_Normal;
out vec2 v_TexCoord;

void main()
{
    vec4 worldPos = u_Model * vec4(aPos, 1.0);
    v_WorldPos = worldPos.xyz;
    v_Normal = normalize(u_NormalMat * aNormal);
    v_TexCoord = aUV;

    gl_Position = uViewProj * worldPos;
}
