#version 330 core

// Fullscreen triangle vertex shader for underwater volume effect

out vec2 vTexCoord;

void main()
{
    // Generate fullscreen triangle using vertex ID
    float x = float((gl_VertexID & 1) << 2) - 1.0;
    float y = float((gl_VertexID & 2) << 1) - 1.0;

    vec2 pos = vec2(x, y);
    vTexCoord = pos * 0.5 + 0.5;

    gl_Position = vec4(pos, 0.0, 1.0);
}
