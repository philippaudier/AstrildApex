#version 330 core

// Marginallyclever approach: search neighbors for outline
in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_StencilTexture;  // R8 texture with selected object in white
uniform sampler2D u_SceneTexture;    // Original scene color
uniform vec4 u_OutlineColor;
uniform float u_OutlineSize;         // Outline thickness in pixels
uniform vec2 u_CanvasSize;           // Screen dimensions in pixels

// Pulse animation
uniform float u_Time;
uniform int u_EnablePulse;
uniform float u_PulseSpeed;
uniform float u_PulseMinAlpha;
uniform float u_PulseMaxAlpha;

void main()
{
    vec2 texelSize = 1.0 / u_CanvasSize;
    float stencilValue = texture(u_StencilTexture, vTexCoord).r;

    // DEBUG MODE: Show stencil texture directly (white = inside object, black = outside)
    // UNCOMMENT TO DEBUG STENCIL TEXTURE:
    // FragColor = vec4(stencilValue, stencilValue, stencilValue, 1.0);
    // return;

    // If we're inside the object (white in stencil), don't draw outline here
    // The scene is already in the FBO, so we discard to keep it
    if (stencilValue > 0.5)
    {
        discard;
    }

    // We're outside the object - search for edges
    // Expand search radius to ensure we find outline pixels
    int outInt = int(ceil(u_OutlineSize)) + 2;  // Add 2 pixels margin
    float o2 = (u_OutlineSize + 2.0) * (u_OutlineSize + 2.0);

    bool isOutline = false;
    for (int y = -outInt; y <= outInt; y++)
    {
        for (int x = -outInt; x <= outInt; x++)
        {
            // Circular kernel
            if (float(x*x + y*y) > o2) continue;

            vec2 offset = vec2(float(x), float(y)) * texelSize;
            float neighbor = texture(u_StencilTexture, vTexCoord + offset).r;

            // If any neighbor is white (inside object), we're on the outline
            if (neighbor > 0.5)
            {
                isOutline = true;
                break;
            }
        }
        if (isOutline) break;
    }

    // Only draw outline pixels, discard everything else
    if (isOutline)
    {
        vec4 outlineColor = u_OutlineColor;

        // Apply pulse effect
        if (u_EnablePulse > 0)
        {
            float pulseValue = sin(u_Time * u_PulseSpeed * 6.28318530718) * 0.5 + 0.5;
            float alpha = mix(u_PulseMinAlpha, u_PulseMaxAlpha, pulseValue);
            outlineColor.a *= alpha;
        }

        // Output outline color with alpha for blending
        FragColor = outlineColor;
    }
    else
    {
        // Not on outline, discard to keep the scene as-is
        discard;
    }
}
