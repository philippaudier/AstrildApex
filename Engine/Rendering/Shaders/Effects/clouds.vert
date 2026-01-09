#version 420 core

// ============================================================================
// CLOUD VERTEX SHADER
// Projects cloud dome/sphere vertices into screen space
// ============================================================================

#include "../Includes/Common.glsl"

layout(location = 0) in vec3 aPosition;

// Outputs to fragment shader
out vec3 vWorldDir;   // Direction from camera to sky (for UV mapping)
out vec2 vScreenPos;  // Screen-space position for dithering
out float vVertexY;   // Vertex Y for horizon fade

// Model-View-Projection matrix (dome centered on camera)
uniform mat4 uMVP;

void main() {
    // Transform vertex to clip space using MVP matrix
    vec4 clipPos = uMVP * vec4(aPosition, 1.0);

    // Output clip position
    // Use depth trick: set z = w so depth is always 1.0 (far plane)
    // This ensures clouds render behind all geometry
    gl_Position = clipPos.xyww;

    // Calculate world direction for sphere UV mapping
    // Since dome is centered on camera, direction is simply the vertex position normalized
    vWorldDir = normalize(aPosition);

    // Screen-space position for dithering texture sampling
    // Remap from [-1,1] to [0,1] range
    vScreenPos = clipPos.xy / clipPos.w * 0.5 + 0.5;

    // Pass vertex Y for horizon fade calculations
    vVertexY = aPosition.y;
}
