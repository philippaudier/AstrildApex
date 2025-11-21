#version 330 core

in vec2 vUV;
out vec4 FragColor;

uniform sampler2D u_Texture;
uniform vec2 u_ScreenSize; // in pixels
uniform float u_Quality; // 0.0..1.0 (0 low, 1 high)
uniform float u_Intensity; // effect intensity (0..1)

// FXAA (approximate/compact implementation tuned for GL 3.3)
// Based on public domain implementations (compact variant).

vec3 fetch(vec2 uv) {
    return texture(u_Texture, uv).rgb;
}

void main()
{
    vec2 px = 1.0 / u_ScreenSize;

    vec3 rgbM = fetch(vUV);

    // Sample neighbourhood
    float lumaM = dot(rgbM, vec3(0.299, 0.587, 0.114));
    vec3 rgbN = fetch(vUV + vec2(0.0, -px.y));
    vec3 rgbS = fetch(vUV + vec2(0.0, px.y));
    vec3 rgbW = fetch(vUV + vec2(-px.x, 0.0));
    vec3 rgbE = fetch(vUV + vec2(px.x, 0.0));

    float lumaN = dot(rgbN, vec3(0.299, 0.587, 0.114));
    float lumaS = dot(rgbS, vec3(0.299, 0.587, 0.114));
    float lumaW = dot(rgbW, vec3(0.299, 0.587, 0.114));
    float lumaE = dot(rgbE, vec3(0.299, 0.587, 0.114));

    float lumaMin = min(lumaM, min(min(lumaN, lumaS), min(lumaW, lumaE)));
    float lumaMax = max(lumaM, max(max(lumaN, lumaS), max(lumaW, lumaE)));

    float range = lumaMax - lumaMin;

    // Early exit: not an edge
    float threshold = mix(0.0312, 0.063, u_Quality); // tuned
    float edgeMask = smoothstep(threshold * 0.5, threshold * 2.0, range);
    if (edgeMask < 0.001) {
        FragColor = vec4(rgbM, 1.0);
        return;
    }

    // Compute gradient
    vec2 dir;
    dir.x = -((lumaN + lumaS) - 2.0 * lumaM);
    dir.y =  ((lumaW + lumaE) - 2.0 * lumaM);

    // Normalize and reduce based on quality
    float maxAbs = max(abs(dir.x), abs(dir.y));
    if (maxAbs < 1e-6) { dir = vec2(0.0); }
    else dir /= maxAbs;

    float rcpScreen = 1.0 / sqrt((dir.x * dir.x + dir.y * dir.y) + 1.0);
    dir *= rcpScreen * mix(0.5, 1.5, u_Quality) * px;

    // Sample along edge and blend
    vec3 rgbA = 0.5 * (fetch(vUV + dir * -1.0) + fetch(vUV + dir * 1.0));
    vec3 rgbB = 0.25 * (fetch(vUV + dir * -2.0) + fetch(vUV + dir * 2.0)) + 0.5 * rgbA;

    // Choose depending on local contrast
    float lumaA = dot(rgbA, vec3(0.299, 0.587, 0.114));
    vec3 fxaaColor = (lumaA < lumaMin || lumaA > lumaMax) ? rgbB : rgbA;

    // Blend according to quality * intensity
    float blend = clamp(u_Quality * u_Intensity, 0.0, 1.0);
    vec3 finalColor = mix(rgbM, fxaaColor, blend);

    FragColor = vec4(finalColor, 1.0);
}
