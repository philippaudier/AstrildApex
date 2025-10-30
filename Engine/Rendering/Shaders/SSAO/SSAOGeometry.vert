#version 330 core

layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform mat4 u_ViewMatrix;
uniform mat4 u_ProjMatrix;
uniform mat4 u_ViewProjMatrix;

out vec3 vViewPos;
out vec3 vViewNormal;
out vec2 vUV;

void main(){
    vec4 worldPos = u_Model * vec4(aPos, 1.0);
    vec4 viewPos = u_ViewMatrix * worldPos;

    // Store VIEW SPACE position and normal (correct for SSAO)
    vViewPos = viewPos.xyz;
    vViewNormal = normalize(mat3(u_ViewMatrix) * u_NormalMat * aNormal);
    vUV = aUV;

    gl_Position = u_ProjMatrix * viewPos;
}