#version 330 core

// Renders selected object as pure white in R8 stencil texture
out float FragColor;

void main()
{
    FragColor = 1.0; // White = selected object
}
