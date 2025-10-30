#version 330 core

#include "../Includes/Common.glsl"

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;

layout(location=0) out vec4 FragColor;

void main()
{
    // Simple blue water color
    FragColor = vec4(0.1, 0.3, 0.8, 1.0);
}
