#version 330 core

// Empty fragment shader for depth-only pass
// Depth is automatically written to gl_FragDepth

void main()
{
    // No color output needed for shadow depth pass
    // OpenGL automatically writes depth to the depth buffer
}
