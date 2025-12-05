#version 450 core

// Per-vertex attributes (billboard quad)
layout(location = 0) in vec2 aPosition;  // Quad corner position (-0.5 to 0.5)
layout(location = 1) in vec2 aTexCoord;  // UV coordinates

// Per-instance attributes
layout(location = 2) in vec3 aInstancePosition;  // Particle world position
layout(location = 3) in float aInstanceSize;     // Particle size
layout(location = 4) in vec4 aInstanceColor;     // Particle color
layout(location = 5) in float aInstanceRotation; // Particle rotation (degrees)

// Uniforms
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraPos;
uniform vec3 uCameraRight;
uniform vec3 uCameraUp;

// Outputs to fragment shader
out vec2 vTexCoord;
out vec4 vColor;

void main()
{
    vTexCoord = aTexCoord;
    vColor = aInstanceColor;

    // Billboard rotation
    float rotRad = radians(aInstanceRotation);
    float cosR = cos(rotRad);
    float sinR = sin(rotRad);
    
    // Rotate the quad corner
    vec2 rotatedPos = vec2(
        aPosition.x * cosR - aPosition.y * sinR,
        aPosition.x * sinR + aPosition.y * cosR
    );
    
    // Scale by particle size
    rotatedPos *= aInstanceSize;
    
    // Billboard towards camera using camera right/up vectors
    vec3 worldPos = aInstancePosition 
        + uCameraRight * rotatedPos.x 
        + uCameraUp * rotatedPos.y;
    
    // Transform to clip space
    gl_Position = uProjection * uView * vec4(worldPos, 1.0);
}
