#version 330 core

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aColor;

uniform mat4 uView;
uniform mat4 uProj;
uniform float uPointSize;
uniform float uStarAngle; // degrees

out vec3 vColor;

void main() {
    // Remove translation from view matrix (keep only rotation) like skybox does
    mat4 viewRotation = mat4(mat3(uView));

    // Apply optional star rotation in world space around Y axis
    float ang = radians(uStarAngle);
    float c = cos(ang);
    float s = sin(ang);
    mat3 rotY = mat3(
        vec3(c, 0.0, s),
        vec3(0.0, 1.0, 0.0),
        vec3(-s, 0.0, c)
    );

    vec3 dir = aPos;
    if (uStarAngle != 0.0) dir = rotY * aPos;

    vec4 pos = uProj * viewRotation * vec4(dir, 1.0);

    // Use skybox trick: set depth to far plane (1.0 in NDC)
    gl_Position = pos.xyww;

    // Pass color to fragment shader
    vColor = aColor;

    // Set point size
    gl_PointSize = uPointSize;
}
