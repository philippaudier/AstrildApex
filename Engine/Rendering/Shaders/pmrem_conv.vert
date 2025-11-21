#version 330 core
layout (location = 0) in vec3 aPosition;

out vec3 vWorldPos;

uniform mat4 uView;
uniform mat4 uProjection;

void main()
{
    vec4 pos = uProjection * uView * vec4(aPosition, 1.0);
    gl_Position = pos.xyww;
    // Pass direction (world position) as the normalized position
    vWorldPos = aPosition;
}
