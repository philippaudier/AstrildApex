#version 420 core

// Modern Image Sharpening Post-Process
// Based on AMD FidelityFX Contrast Adaptive Sharpening (CAS)
// Performant, edge-aware sharpening that prevents over-sharpening and halos

layout(location=0) out vec4 outColor;

in vec2 vUV;

uniform sampler2D u_InputTexture;
uniform float u_Sharpness;      // 0.0 = no sharpening, 1.0 = maximum sharpening
uniform vec2 u_TexelSize;       // 1.0 / resolution (for pixel offset calculations)

// Converts RGB to luma (perceived brightness)
float rgb2luma(vec3 rgb) {
    return dot(rgb, vec3(0.299, 0.587, 0.114));
}

// AMD FidelityFX CAS algorithm
// Contrast Adaptive Sharpening with edge detection
vec3 contrastAdaptiveSharpen(vec2 uv) {
    // Sample center pixel
    vec3 center = texture(u_InputTexture, uv).rgb;

    // Sample 4-connected neighbors (cross pattern)
    vec3 top    = texture(u_InputTexture, uv + vec2(0.0, u_TexelSize.y)).rgb;
    vec3 bottom = texture(u_InputTexture, uv - vec2(0.0, u_TexelSize.y)).rgb;
    vec3 left   = texture(u_InputTexture, uv - vec2(u_TexelSize.x, 0.0)).rgb;
    vec3 right  = texture(u_InputTexture, uv + vec2(u_TexelSize.x, 0.0)).rgb;

    // Convert to luma for edge detection
    float lumaCenter = rgb2luma(center);
    float lumaTop    = rgb2luma(top);
    float lumaBottom = rgb2luma(bottom);
    float lumaLeft   = rgb2luma(left);
    float lumaRight  = rgb2luma(right);

    // Find minimum and maximum luma in neighborhood
    float lumaMin = min(lumaCenter, min(min(lumaTop, lumaBottom), min(lumaLeft, lumaRight)));
    float lumaMax = max(lumaCenter, max(max(lumaTop, lumaBottom), max(lumaLeft, lumaRight)));

    // Calculate local contrast (edge strength)
    float contrast = lumaMax - lumaMin;

    // Adaptive sharpening strength based on contrast
    // High contrast areas (edges) get less sharpening to prevent halos
    // Low contrast areas get more sharpening
    float edgeWeight = 1.0 - smoothstep(0.0, 0.3, contrast);
    float adaptiveSharpness = u_Sharpness * edgeWeight;

    // Calculate sharpening kernel
    // Formula: center + (center - neighbors_avg) * sharpness
    vec3 neighborsAvg = (top + bottom + left + right) * 0.25;
    vec3 sharpened = center + (center - neighborsAvg) * adaptiveSharpness;

    // Prevent over-brightening and over-darkening
    // Clamp result to local min/max range to prevent halos
    vec3 minRGB = min(center, min(min(top, bottom), min(left, right)));
    vec3 maxRGB = max(center, max(max(top, bottom), max(left, right)));
    sharpened = clamp(sharpened, minRGB, maxRGB);

    return sharpened;
}

void main() {
    // Apply contrast adaptive sharpening
    vec3 sharpenedColor = contrastAdaptiveSharpen(vUV);

    // Preserve alpha channel from original
    float alpha = texture(u_InputTexture, vUV).a;

    outColor = vec4(sharpenedColor, alpha);
}
