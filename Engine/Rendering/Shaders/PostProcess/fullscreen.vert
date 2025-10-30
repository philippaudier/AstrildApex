#version 330 core

// Fullscreen triangle vertex shader using gl_VertexID.
// Matches renderers that call: GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

out vec2 vTexCoord;

void main()
{
    // Emit a triangle that covers the whole screen without a vertex buffer.
    // Positions chosen so the triangle covers the full viewport.
    vec2 pos = vec2((gl_VertexID == 1) ? 3.0 : -1.0,
                    (gl_VertexID == 2) ? 3.0 : -1.0);

    vTexCoord = pos * 0.5 + 0.5;
    gl_Position = vec4(pos, 0.0, 1.0);
}