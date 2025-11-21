#version 330 core

// Post-process edge detection for selection outline
// Based on: https://bgolus.medium.com/the-quest-for-very-wide-outlines-ba82ed442cd9

out vec4 FragColor;

in vec2 vTexCoord;

uniform sampler2D u_ColorTexture;    // Scene color
uniform usampler2D u_IdTexture;      // Object ID texture (R32UI integer format)
uniform vec2 u_ScreenSize;           // Screen dimensions
uniform vec4 u_OutlineColor;         // Outline color
uniform float u_OutlineWidth;        // Outline thickness in pixels (default: 2.0)
uniform float u_SelectedId;          // ID of selected object (0 = none)

// Pulse effect parameters
uniform float u_Time;                // Time in seconds for animation
uniform bool u_EnablePulse;          // Enable/disable pulse effect
uniform float u_PulseSpeed;          // Pulse frequency in Hz (cycles per second)
uniform float u_PulseMinAlpha;       // Minimum alpha during pulse
uniform float u_PulseMaxAlpha;       // Maximum alpha during pulse

// Entity ID range (defined in EntityIdRange.cs)
const uint MIN_ENTITY_ID = 1000u;
const uint MAX_ENTITY_ID = 10000u;

// Check if an ID is a valid entity (not a gizmo, grid, or special object)
bool isEntityId(uint id)
{
    return id >= MIN_ENTITY_ID && id < MAX_ENTITY_ID;
}

void main()
{
    // Sample center pixel ID (stored as uint in red channel)
    uint centerId = texture(u_IdTexture, vTexCoord).r;
    uint selectedIdInt = uint(u_SelectedId);

    // Skip if selected object is not a valid entity (gizmo, grid, etc.)
    if (!isEntityId(selectedIdInt))
    {
        FragColor = texture(u_ColorTexture, vTexCoord);
        return;
    }

    // Check if this pixel is the selected object itself
    // If so, don't draw outline on it (only on surrounding pixels)
    if (centerId == selectedIdInt)
    {
        FragColor = texture(u_ColorTexture, vTexCoord);
        return;
    }

    // Check if any neighbor is the selected object
    bool isEdge = false;

    if (centerId != selectedIdInt)
    {
        // Calculate texel size
        vec2 texelSize = 1.0 / u_ScreenSize;

        // Sample surrounding pixels in a cross pattern
        // The outline width determines how far we sample
        float offset = u_OutlineWidth;

        // Sample 4 directions (cross pattern) - check bounds before sampling
        vec2 coord_right = vTexCoord + vec2(offset * texelSize.x, 0.0);
        vec2 coord_left  = vTexCoord - vec2(offset * texelSize.x, 0.0);
        vec2 coord_up    = vTexCoord + vec2(0.0, offset * texelSize.y);
        vec2 coord_down  = vTexCoord - vec2(0.0, offset * texelSize.y);

        // Only sample if within valid UV range (0,0) to (1,1)
        uint id_right  = (coord_right.x <= 1.0) ? texture(u_IdTexture, coord_right).r : 0u;
        uint id_left   = (coord_left.x >= 0.0) ? texture(u_IdTexture, coord_left).r : 0u;
        uint id_up     = (coord_up.y <= 1.0) ? texture(u_IdTexture, coord_up).r : 0u;
        uint id_down   = (coord_down.y >= 0.0) ? texture(u_IdTexture, coord_down).r : 0u;

        // Sample 4 diagonals for smoother outline - check bounds before sampling
        vec2 coord_ur = vTexCoord + vec2(offset * texelSize.x, offset * texelSize.y);
        vec2 coord_ul = vTexCoord + vec2(-offset * texelSize.x, offset * texelSize.y);
        vec2 coord_dr = vTexCoord + vec2(offset * texelSize.x, -offset * texelSize.y);
        vec2 coord_dl = vTexCoord + vec2(-offset * texelSize.x, -offset * texelSize.y);

        uint id_ur = (coord_ur.x <= 1.0 && coord_ur.y <= 1.0) ? texture(u_IdTexture, coord_ur).r : 0u;
        uint id_ul = (coord_ul.x >= 0.0 && coord_ul.y <= 1.0) ? texture(u_IdTexture, coord_ul).r : 0u;
        uint id_dr = (coord_dr.x <= 1.0 && coord_dr.y >= 0.0) ? texture(u_IdTexture, coord_dr).r : 0u;
        uint id_dl = (coord_dl.x >= 0.0 && coord_dl.y >= 0.0) ? texture(u_IdTexture, coord_dl).r : 0u;

        // If any neighbor is the selected object, this is an edge pixel
        // (we already verified selectedIdInt is not a gizmo above)
        if (id_right == selectedIdInt || id_left == selectedIdInt ||
            id_up == selectedIdInt || id_down == selectedIdInt ||
            id_ur == selectedIdInt || id_ul == selectedIdInt ||
            id_dr == selectedIdInt || id_dl == selectedIdInt)
        {
            isEdge = true;
        }
    }

    // Output: blend outline color over scene color at edges
    vec4 sceneColor = texture(u_ColorTexture, vTexCoord);

    // DEBUG: Force show outline on all selected pixels (DISABLED)
    // if (abs(centerId - u_SelectedId) < epsilon)
    // {
    //     FragColor = u_OutlineColor; // Should make the whole object orange
    //     return;
    // }

    if (isEdge)
    {
        // Draw outline with optional pulse effect
        vec4 outlineColor = u_OutlineColor;
        
        if (u_EnablePulse)
        {
            // Smooth sine wave pulse (0 to 1 range)
            float pulseValue = sin(u_Time * u_PulseSpeed * 6.28318530718) * 0.5 + 0.5;
            
            // Map pulse to alpha range
            float alpha = mix(u_PulseMinAlpha, u_PulseMaxAlpha, pulseValue);
            outlineColor.a *= alpha;
        }
        
        FragColor = outlineColor;
    }
    else
    {
        // Pass through scene color
        FragColor = sceneColor;
    }
}
