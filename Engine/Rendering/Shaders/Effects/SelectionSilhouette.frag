#version 330 core

// Simple fragment shader for rendering silhouette mask
// Outputs white (1.0) for the selected object, will be used for outline generation

out vec4 FragColor;

void main()
{
    // Render selected object as pure white
    FragColor = vec4(1.0, 1.0, 1.0, 1.0);
}
