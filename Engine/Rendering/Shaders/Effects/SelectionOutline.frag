#version 330 core

out vec4 FragColor;

uniform vec4 u_OutlineColor;

void main()
{
    // Simple solid color output for outline
    FragColor = u_OutlineColor;
}