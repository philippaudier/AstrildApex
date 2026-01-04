#version 450 core

// Per-vertex attributes (billboard quad)
layout(location = 0) in vec2 aPosition;  // Quad corner position (-0.5 to 0.5)
layout(location = 1) in vec2 aTexCoord;  // UV coordinates

// Per-instance attributes
layout(location = 2) in vec3 aInstancePosition;  // Particle world position
layout(location = 3) in float aInstanceSize;     // Particle size
layout(location = 4) in vec4 aInstanceColor;     // Particle color
layout(location = 5) in float aInstanceRotation; // Particle rotation (degrees)
layout(location = 6) in int aInstanceSpriteIndex; // Sprite sheet index
layout(location = 7) in float aInstanceFlipX;    // Flip horizontally (0.0 or 1.0)
layout(location = 8) in float aInstanceFlipY;    // Flip vertically (0.0 or 1.0)
layout(location = 9) in vec3 aInstanceRotation3D; // 3D rotation (X, Y, Z in degrees)

// Uniforms
uniform mat4 uView;
uniform mat4 uProjection;
uniform vec3 uCameraPos;
uniform vec3 uCameraRight;
uniform vec3 uCameraUp;
uniform int uSpriteRows;
uniform int uSpriteColumns;

// Outputs to fragment shader
out vec2 vTexCoord;
out vec4 vColor;

// Helper function to create rotation matrix around X axis
mat3 rotateX(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat3(
        1.0, 0.0, 0.0,
        0.0, c, -s,
        0.0, s, c
    );
}

// Helper function to create rotation matrix around Y axis
mat3 rotateY(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat3(
        c, 0.0, s,
        0.0, 1.0, 0.0,
        -s, 0.0, c
    );
}

// Helper function to create rotation matrix around Z axis
mat3 rotateZ(float angle)
{
    float c = cos(angle);
    float s = sin(angle);
    return mat3(
        c, -s, 0.0,
        s, c, 0.0,
        0.0, 0.0, 1.0
    );
}

void main()
{
    vColor = aInstanceColor;

    // Calculate sprite sheet UV coordinates
    int totalSprites = uSpriteRows * uSpriteColumns;
    int spriteIndex = aInstanceSpriteIndex % max(totalSprites, 1);

    // Calculate sprite position in the sheet (row, column)
    int row = spriteIndex / uSpriteColumns;
    int col = spriteIndex % uSpriteColumns;

    // Calculate UV offset and scale
    float uScale = 1.0 / float(uSpriteColumns);
    float vScale = 1.0 / float(uSpriteRows);
    float uOffset = float(col) * uScale;
    float vOffset = float(row) * vScale;

    // Apply flip
    vec2 flippedUV = aTexCoord;
    if (aInstanceFlipX > 0.5)
        flippedUV.x = 1.0 - flippedUV.x;
    if (aInstanceFlipY > 0.5)
        flippedUV.y = 1.0 - flippedUV.y;

    // Scale and offset UVs for sprite sheet
    vTexCoord = vec2(
        uOffset + flippedUV.x * uScale,
        vOffset + flippedUV.y * vScale
    );

    // Billboard rotation (2D)
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

    // Extract camera's right and up vectors from view matrix inverse
    // The view matrix transforms world to camera space, so its inverse
    // transforms camera space to world space. The first 3 columns of
    // the inverse are the camera's right, up, and forward axes in world space.
    
    // Get view matrix inverse (we only need rotation part, so we transpose 3x3)
    mat3 viewRot = mat3(uView);
    mat3 viewRotInv = transpose(viewRot);
    
    // Extract camera right and up in world space
    vec3 worldRight = viewRotInv[0];  // First column
    vec3 worldUp = viewRotInv[1];     // Second column
    
    // Create billboard in WORLD SPACE using world-space camera axes
    vec3 billboardOffset = worldRight * rotatedPos.x + worldUp * rotatedPos.y;

    // Apply 3D rotation if any rotation is set
    if (length(aInstanceRotation3D) > 0.01)
    {
        vec3 rotRad3D = radians(aInstanceRotation3D);
        mat3 rotation = rotateZ(rotRad3D.z) * rotateY(rotRad3D.y) * rotateX(rotRad3D.x);
        billboardOffset = rotation * billboardOffset;
    }

    // Final world position: particle position + billboard offset
    vec3 worldPos = aInstancePosition + billboardOffset;

    // Transform to clip space
    gl_Position = uProjection * uView * vec4(worldPos, 1.0);
}
