#version 330 core

layout(location = 0) in vec3 aPos;

uniform mat4 u_LightSpaceMatrix;  // Combined light view-projection matrix
uniform mat4 u_Model;              // Model matrix for this object

void main()
{
    // Transform vertex to light space
    gl_Position = u_LightSpaceMatrix * u_Model * vec4(aPos, 1.0);
}
