#version 450 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

uniform mat4 u_MVP;
uniform mat4 u_Model;

out vec3 v_WorldNormal;

void main()
{
    gl_Position = u_MVP * vec4(aPos, 1.0);

    // Transform normal to world-space
    // Use transpose(inverse()) to handle non-uniform scaling correctly
    mat3 normalMatrix = transpose(inverse(mat3(u_Model)));
    v_WorldNormal = normalize(normalMatrix * aNormal);
}
