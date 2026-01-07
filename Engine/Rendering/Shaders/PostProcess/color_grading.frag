#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;

// Color grading parameters
uniform float u_Saturation;      // 0 = grayscale, 1 = normal, 2 = vibrant
uniform float u_Contrast;        // 0 = flat, 1 = normal, 2 = high contrast
uniform float u_Brightness;      // -1 = darker, 0 = normal, +1 = brighter
uniform vec3 u_ColorFilter;      // RGB multiplier (tint)
uniform float u_Temperature;     // -1 = cool (blue), 0 = neutral, +1 = warm (orange)
uniform float u_Tint;            // -1 = green, 0 = neutral, +1 = magenta
uniform float u_HueShift;        // 0-360 degrees hue rotation
uniform float u_Vibrance;        // Selective saturation boost for dull colors

// Convert RGB to HSV
vec3 rgb2hsv(vec3 c) {
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

// Convert HSV to RGB
vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

// Calculate luminance
float luminance(vec3 color) {
    return dot(color, vec3(0.2126, 0.7152, 0.0722));
}

void main()
{
    vec3 color = texture(u_SourceTexture, vTexCoord).rgb;

    // === BRIGHTNESS ===
    color += u_Brightness;

    // === CONTRAST ===
    // Pivot around 0.5 (middle gray)
    color = (color - 0.5) * u_Contrast + 0.5;

    // === TEMPERATURE & TINT ===
    // Temperature: cool (blue) <-> warm (orange/yellow)
    if (u_Temperature != 0.0) {
        vec3 warmColor = vec3(1.05, 1.0, 0.95);  // Slight orange tint
        vec3 coolColor = vec3(0.95, 1.0, 1.05);  // Slight blue tint

        if (u_Temperature > 0.0) {
            color *= mix(vec3(1.0), warmColor, u_Temperature);
        } else {
            color *= mix(vec3(1.0), coolColor, -u_Temperature);
        }
    }

    // Tint: green <-> magenta
    if (u_Tint != 0.0) {
        vec3 greenTint = vec3(0.95, 1.05, 0.95);
        vec3 magentaTint = vec3(1.05, 0.95, 1.05);

        if (u_Tint > 0.0) {
            color *= mix(vec3(1.0), magentaTint, u_Tint);
        } else {
            color *= mix(vec3(1.0), greenTint, -u_Tint);
        }
    }

    // === COLOR FILTER ===
    color *= u_ColorFilter;

    // === HUE SHIFT ===
    if (abs(u_HueShift) > 0.01) {
        vec3 hsv = rgb2hsv(color);
        hsv.x += u_HueShift / 360.0;
        hsv.x = fract(hsv.x); // Wrap around
        color = hsv2rgb(hsv);
    }

    // === VIBRANCE ===
    // Vibrance increases saturation of dull colors while protecting already saturated colors
    if (abs(u_Vibrance) > 0.01) {
        float luma = luminance(color);
        float maxChannel = max(max(color.r, color.g), color.b);
        float minChannel = min(min(color.r, color.g), color.b);
        float saturation = (maxChannel - minChannel) / (maxChannel + 1e-5);

        // Apply vibrance more to less saturated colors
        float vibranceBoost = (1.0 - saturation) * u_Vibrance;
        vec3 hsv = rgb2hsv(color);
        hsv.y = clamp(hsv.y + vibranceBoost, 0.0, 1.0);
        color = hsv2rgb(hsv);
    }

    // === SATURATION ===
    // Apply after vibrance for more control
    if (abs(u_Saturation - 1.0) > 0.01) {
        float luma = luminance(color);
        color = mix(vec3(luma), color, u_Saturation);
    }

    // Clamp to valid range
    color = clamp(color, 0.0, 1.0);

    FragColor = vec4(color, 1.0);
}
