#version 330 core

in vec2 vUV;
out vec4 FragColor;

uniform sampler2D u_Depth;
uniform mat4 u_InvViewProj; // inverse of (view * proj) for current frame (jittered)
uniform mat4 u_PrevViewProj; // previous frame view-proj (jittered)

void main()
{
    vec2 uv = vUV;
    float depth = texture(u_Depth, uv).r;

    // Reconstruct world position (same math as TAA shader fallback)
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 worldPos = u_InvViewProj * ndc;
    worldPos /= worldPos.w;

    // Project into previous clip space and convert to UV
    vec4 prevClip = u_PrevViewProj * worldPos;
    prevClip /= prevClip.w;
    vec2 prevUV = prevClip.xy * 0.5 + 0.5;

    // Velocity in UV space (prev - curr)
    vec2 vel = prevUV - uv;

    // Store into RG channels (x,y). Alpha is 1.0 to keep texture well-formed.
    FragColor = vec4(vel, 0.0, 1.0);
}
