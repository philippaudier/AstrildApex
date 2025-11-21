#version 450 core

in vec3 v_WorldNormal;

layout (location = 0) out vec4 o_Normal;

void main()
{
    // Store world-space normals (normalized)
    // Encode to [0,1] range for storage in RGB texture
    vec3 normal = normalize(v_WorldNormal);
    o_Normal = vec4(normal * 0.5 + 0.5, 1.0);
}
