#version 330 core

// Renders selected object as pure white in R8 stencil texture
layout(location = 0) out vec4 FragColor;

void main()
{
    // Write to all channels to ensure compatibility
    // R8 texture will only use the red channel
    FragColor = vec4(1.0, 1.0, 1.0, 1.0); // White = selected object
}
