#version 420 core

#include "../Includes/Common.glsl"

layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUV;

void main(){
    vec4 wp = u_Model * vec4(aPos,1.0);
    vWorldPos = wp.xyz;
    vNormal   = normalize(u_NormalMat * aNormal);
    vUV = aUV * u_TextureTiling + u_TextureOffset;
    gl_Position = uViewProj * wp;
}
