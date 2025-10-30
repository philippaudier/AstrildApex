#version 420 core

#include "../Includes/Common.glsl"

layout(location=0) out vec3 outPosition;
layout(location=1) out vec3 outNormal;

in vec3 vViewPos;
in vec3 vViewNormal;
in vec2 vUV;

void main(){
    // Store VIEW SPACE position and normal (correct for SSAO)
    outPosition = vViewPos;
    outNormal = normalize(vViewNormal);
    // Respect runtime normal flip (DX vs GL) if requested
    #ifdef GL_ES
    // nothing
    #endif
    if (gl_MaxVertexAttribs > 0) { /* noop to keep formatting */ }
    // flip Y when requested
    // note: u_FlipNormalY is declared in Common.glsl and bound by the renderer
    if (u_FlipNormalY == 1) outNormal.y = -outNormal.y;
}