#version 330 core

in vec2 vTexCoord;

uniform sampler2D u_AlbedoTex;
uniform int u_AlphaClippingEnabled;
uniform float u_AlphaClipThreshold;

void main()
{
    // Alpha clipping for vegetation with transparent leaves
    if (u_AlphaClippingEnabled == 1) {
        float alpha = texture(u_AlbedoTex, vTexCoord).a;
        if (alpha < u_AlphaClipThreshold) {
            discard;
        }
    }

    // Depth is automatically written to gl_FragDepth
    // No color output needed for shadow depth pass
}
