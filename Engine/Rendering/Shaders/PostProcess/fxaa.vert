#version 330 core

// Fullscreen triangle vertex shader using gl_VertexID.
// Matches other post-process shaders that draw a fullscreen triangle with 3 verts.

out vec2 vUV;

void main()
{
    // Generate fullscreen triangle using vertex ID
    // v0 = (-1, -1), v1 = (3, -1), v2 = (-1, 3)
    float x = float((gl_VertexID & 1) << 2) - 1.0;
    float y = float((gl_VertexID & 2) << 1) - 1.0;

    vec2 pos = vec2(x, y);

    // Calculate texture coordinates
    vUV = pos * 0.5 + 0.5;

    gl_Position = vec4(pos, 0.0, 1.0);
}
