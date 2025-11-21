#version 330 core

// Fullscreen triangle vertex shader using gl_VertexID.
// Matches renderers that call: GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

out vec2 vTexCoord;

void main()
{
    // Generate fullscreen triangle using vertex ID
    // v0 = (-1, -1), v1 = (3, -1), v2 = (-1, 3)
    float x = float((gl_VertexID & 1) << 2) - 1.0;
    float y = float((gl_VertexID & 2) << 1) - 1.0;

    vec2 pos = vec2(x, y);

    // Calculate texture coordinates with exact precision
    // This avoids interpolation issues at viewport edges
    vTexCoord = pos * 0.5 + 0.5;

    gl_Position = vec4(pos, 0.0, 1.0);
}