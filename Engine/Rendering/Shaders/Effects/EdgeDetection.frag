#version 330 core

in vec2 vTexCoord;
out vec4 outColor;

uniform sampler2D u_ColorTexture;
uniform sampler2D u_IdTexture;
uniform vec2 u_ScreenSize;
uniform vec4 u_OutlineColor;
uniform float u_Time;

void main()
{
    // Always start with original color
    vec4 originalColor = texture(u_ColorTexture, vTexCoord);
    
    vec2 texelSize = 1.0 / u_ScreenSize;
    
    // Sample current pixel ID  
    float centerID = texture(u_IdTexture, vTexCoord).r;
    
    // Skip if background (ID = 0)
    if (centerID < 0.001) {
        outColor = originalColor;
        return;
    }
    
    // Sample 4 neighbors (cross pattern)
    float leftID = texture(u_IdTexture, vTexCoord + vec2(-texelSize.x, 0.0)).r;
    float rightID = texture(u_IdTexture, vTexCoord + vec2(texelSize.x, 0.0)).r;
    float topID = texture(u_IdTexture, vTexCoord + vec2(0.0, texelSize.y)).r;
    float bottomID = texture(u_IdTexture, vTexCoord + vec2(0.0, -texelSize.y)).r;
    
    // Check if this is an edge (any neighbor has different ID or is background)
    bool isEdge = false;
    float threshold = 0.001;
    
    if (abs(leftID - centerID) > threshold || leftID < 0.001 ||
        abs(rightID - centerID) > threshold || rightID < 0.001 ||
        abs(topID - centerID) > threshold || topID < 0.001 ||
        abs(bottomID - centerID) > threshold || bottomID < 0.001) {
        isEdge = true;
    }
    
    if (isEdge) {
        // Animate outline color
        float pulse = 0.5 + 0.5 * sin(u_Time * 4.0);
        vec4 animatedOutline = vec4(u_OutlineColor.rgb, u_OutlineColor.a * pulse);
        
        // Blend with original color
        outColor = mix(originalColor, animatedOutline, animatedOutline.a);
    } else {
        outColor = originalColor;
    }
}