#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 u_Model;
uniform mat4 u_View;
uniform mat4 u_Projection;
uniform float u_OutlineWidth;

void main()
{
    // Extrude vertices along their normals in world space
    // This creates a thicker "shell" around the object
    // Based on the marginallyclever.com tutorial approach
    
    // Transform normal to world space
    vec3 worldNormal = normalize(mat3(u_Model) * aNormal);
    
    // Extrude position along normal
    vec3 extrudedPosition = aPosition + worldNormal * u_OutlineWidth;
    
    // Transform to clip space
    gl_Position = u_Projection * u_View * u_Model * vec4(extrudedPosition, 1.0);
}