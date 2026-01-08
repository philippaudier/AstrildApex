#version 330 core

// Composites outline onto scene
// Extracts edges and applies color with optional pulse effect

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SceneTexture;      // Original scene
uniform sampler2D u_SilhouetteOriginal; // Original silhouette (not blurred)
uniform sampler2D u_SilhouetteBlurred;  // Blurred/dilated silhouette

uniform vec4 u_OutlineColor;
uniform float u_Time;
uniform bool u_EnablePulse;
uniform float u_PulseSpeed;
uniform float u_PulseMinAlpha;
uniform float u_PulseMaxAlpha;

void main()
{
    // DEBUG: Test if shader executes at all - write pure green
    FragColor = vec4(0.0, 1.0, 0.0, 1.0);
    return;

    // Sample textures
    vec4 sceneColor = texture(u_SceneTexture, vTexCoord);
    float original = texture(u_SilhouetteOriginal, vTexCoord).r;
    float blurred = texture(u_SilhouetteBlurred, vTexCoord).r;

    // Edge detection: blurred - original = outline only
    // This gives us just the outer ring/halo
    float edge = blurred - original;
    edge = clamp(edge, 0.0, 1.0);

    // Apply pulse effect if enabled
    vec4 outlineColor = u_OutlineColor;
    if (u_EnablePulse)
    {
        float pulseValue = sin(u_Time * u_PulseSpeed * 6.28318530718) * 0.5 + 0.5;
        float alpha = mix(u_PulseMinAlpha, u_PulseMaxAlpha, pulseValue);
        outlineColor.a *= alpha;
    }

    // Blend outline over scene
    // Use edge as alpha mask
    vec4 finalColor = mix(sceneColor, outlineColor, edge * outlineColor.a);

    FragColor = finalColor;
}
