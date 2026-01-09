#version 330 core

in vec3 vColor;

uniform float uTime;
uniform float uTwinkle;
uniform float uNightFade; // 0.0 = day (invisible), 1.0 = night (fully visible)

out vec4 FragColor;

void main() {
    // Calculate twinkle effect
    float fade = 1.0;
    if (uTwinkle > 0.0) {
        // Use a combination of time and fragment position for varied twinkling
        float t = fract(uTime * 0.1 + gl_FragCoord.x * 0.001 + gl_FragCoord.y * 0.001);
        float noise = abs(sin(t * 6.2831853));
        fade = mix(1.0 - uTwinkle, 1.0, noise);
    }

    // Apply night fade (stars should only be visible at night)
    fade *= uNightFade;

    // Soften the edges of the point to make it look more like a star
    vec2 coord = gl_PointCoord - vec2(0.5);
    float dist = length(coord);
    float alpha = 1.0 - smoothstep(0.3, 0.5, dist);

    // Output color with fade and alpha
    FragColor = vec4(vColor * fade, alpha * fade);
}
