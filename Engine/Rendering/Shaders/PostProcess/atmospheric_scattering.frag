#version 330 core

// ============================================================================
// ATMOSPHERIC SCATTERING - Ray Marching Implementation
// ============================================================================
// All parameters are actively used in the ray marching process
// ============================================================================

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform sampler2D u_DepthTexture;

uniform mat4 u_InvProjection;
uniform mat4 u_InvView;
uniform vec3 u_CameraPos;

uniform vec3 u_SunDirection;
uniform vec3 u_SunColor;
uniform float u_SunIntensity;

// All these parameters affect the result
uniform float u_AtmosphereRadius;    // Atmosphere thickness multiplier (10-200)
uniform float u_RayleighScaleHeight; // Rayleigh density falloff height (1-20)
uniform float u_MieScaleHeight;      // Mie density falloff height (0.1-5)

uniform vec3 u_RayleighCoeff;        // RGB scattering (wavelength-dependent)
uniform float u_MieCoeff;            // Mie scattering strength
uniform float u_MieG;                // Mie anisotropy (0-0.99)

uniform int u_NumSamples;            // View ray samples (8-64)
uniform int u_NumLightSamples;       // Light ray samples (2-16)
uniform float u_Exposure;
uniform float u_Intensity;

// Underwater detection - skip sky replacement when underwater
uniform bool u_IsUnderwater;
uniform float u_WaterLevel;

const float PI = 3.14159265359;

// ============================================================================
// UTILITIES
// ============================================================================

vec3 getViewDir(vec2 uv) {
    vec2 ndc = uv * 2.0 - 1.0;
    vec4 clip = vec4(ndc, 1.0, 1.0);
    vec4 view = u_InvProjection * clip;
    view.xyz /= view.w;
    return normalize((u_InvView * vec4(view.xyz, 0.0)).xyz);
}

// Interleaved gradient noise for dithering
float IGN(vec2 p) {
    return fract(52.9829189 * fract(dot(p, vec2(0.06711056, 0.00583715))));
}

// ============================================================================
// PHASE FUNCTIONS
// ============================================================================

float phaseRayleigh(float cosTheta) {
    return (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
}

float phaseMie(float cosTheta, float g) {
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 / (4.0 * PI)) * ((1.0 - g2) / pow(max(denom, 0.0001), 1.5));
}

// ============================================================================
// RAY MARCHING ATMOSPHERIC SCATTERING
// ============================================================================

vec3 computeAtmosphere(vec3 rayDir, vec3 sunDir, vec2 screenPos) {
    // Scale parameters to game world units
    float atmosphereHeight = u_AtmosphereRadius * 1000.0;  // 100 -> 100,000m
    float rayleighH = u_RayleighScaleHeight * 1000.0;      // 8 -> 8,000m
    float mieH = u_MieScaleHeight * 1000.0;                // 1.2 -> 1,200m

    // Scattering coefficients - scale from scientific notation
    // u_RayleighCoeff is ~(5.8, 13.5, 33.1) * 1e-6
    // Multiply by large factor to get visible scattering
    vec3 betaR = u_RayleighCoeff * 1000.0;  // Results in ~(0.0058, 0.0135, 0.0331)
    float betaM = u_MieCoeff * 1000.0;       // Results in ~0.021

    // Ray march setup
    int viewSamples = clamp(u_NumSamples, 4, 64);
    int lightSamples = clamp(u_NumLightSamples, 2, 16);

    // Limit maxDist to prevent huge step sizes that cause banding/triangle artifacts
    // Most scattering happens in the first ~50km anyway, no need to march further
    float maxDist = min(atmosphereHeight * 2.0, 100000.0);
    float stepSize = maxDist / float(viewSamples);

    // Dithering to reduce banding (varies with step count)
    float dither = IGN(screenPos) * 0.99;

    // Accumulators
    vec3 totalRayleigh = vec3(0.0);
    vec3 totalMie = vec3(0.0);
    float opticalDepthR = 0.0;
    float opticalDepthM = 0.0;

    // Ray march along view ray
    for (int i = 0; i < 64; i++) {
        if (i >= viewSamples) break;

        // Sample position with dithering
        float t = (float(i) + dither) * stepSize;
        vec3 pos = u_CameraPos + rayDir * t;
        float height = max(pos.y, 0.0);

        // Density at this height (exponential falloff)
        float densityR = exp(-height / rayleighH);
        float densityM = exp(-height / mieH);

        // Optical depth increment for view ray
        float stepR = densityR * stepSize;
        float stepM = densityM * stepSize;
        opticalDepthR += stepR;
        opticalDepthM += stepM;

        // Calculate optical depth toward sun (light ray)
        float lightOpticalR = 0.0;
        float lightOpticalM = 0.0;
        float lightStepSize = (rayleighH * 4.0) / float(lightSamples);

        for (int j = 0; j < 16; j++) {
            if (j >= lightSamples) break;

            float lt = float(j) * lightStepSize;
            vec3 lightPos = pos + sunDir * lt;
            float lightHeight = max(lightPos.y, 0.0);

            lightOpticalR += exp(-lightHeight / rayleighH) * lightStepSize;
            lightOpticalM += exp(-lightHeight / mieH) * lightStepSize;
        }

        // Total optical depth (view + light)
        float totalR = opticalDepthR + lightOpticalR;
        float totalM = opticalDepthM + lightOpticalM;

        // Transmittance (Beer-Lambert law)
        vec3 tau = betaR * totalR + vec3(betaM) * totalM;
        tau *= 0.000001; // Scale down to prevent overflow
        vec3 transmittance = exp(-tau);

        // Accumulate in-scattered light
        totalRayleigh += densityR * transmittance * stepSize;
        totalMie += densityM * transmittance * stepSize;
    }

    // Apply scattering coefficients
    totalRayleigh *= betaR * 0.000001;
    totalMie *= betaM * 0.000001;

    // Phase functions
    float cosTheta = dot(rayDir, sunDir);
    float phaseR = phaseRayleigh(cosTheta);
    float phaseM = phaseMie(cosTheta, u_MieG);

    // Final scattered light
    vec3 scattering = (totalRayleigh * phaseR + totalMie * phaseM);
    scattering *= u_SunColor * u_SunIntensity;

    // Ambient to prevent black sky
    float density = u_AtmosphereRadius / 100.0;
    scattering += vec3(0.02, 0.03, 0.06) * density;

    return scattering;
}

// Tone mapping
vec3 tonemap(vec3 c) {
    return 1.0 - exp(-c * u_Exposure);
}

// ============================================================================
// MAIN
// ============================================================================

void main() {
    vec3 sceneColor = texture(u_SourceTexture, vTexCoord).rgb;
    float depth = texture(u_DepthTexture, vTexCoord).r;

    vec3 rayDir = getViewDir(vTexCoord);
    vec3 sunDir = normalize(u_SunDirection);
    vec2 screenPos = gl_FragCoord.xy;

    // Compute atmosphere
    vec3 atmosphere = computeAtmosphere(rayDir, sunDir, screenPos);
    vec3 atmosphereToned = tonemap(atmosphere);

    vec3 result;

    // When underwater, don't replace sky - let water surface be visible
    if (u_IsUnderwater) {
        // Underwater: just pass through scene color, no atmospheric scattering on sky
        // Still apply very mild aerial perspective to scene objects
        if (depth >= 0.9999) {
            result = sceneColor; // Keep water surface / original sky visible
        } else {
            float linearDepth = 0.1 * 5000.0 / (5000.0 - depth * 4999.9);
            linearDepth = clamp(linearDepth, 0.0, 3000.0);
            float density = u_AtmosphereRadius / 100.0;
            float fog = 1.0 - exp(-linearDepth * density * 0.0001); // Reduced underwater
            fog = clamp(fog, 0.0, 0.2);
            result = mix(sceneColor, atmosphereToned * 0.5, fog);
        }
    } else {
        // Above water: normal atmospheric scattering
        if (depth >= 0.9999) {
            // Sky - use atmospheric scattering
            result = atmosphereToned;
        } else {
            // Scene object - aerial perspective
            float linearDepth = 0.1 * 5000.0 / (5000.0 - depth * 4999.9);
            linearDepth = clamp(linearDepth, 0.0, 3000.0);

            float density = u_AtmosphereRadius / 100.0;
            float fog = 1.0 - exp(-linearDepth * density * 0.0002);
            fog = clamp(fog, 0.0, 0.5);

            result = mix(sceneColor, atmosphereToned * 0.7, fog);
        }
    }

    // Intensity blend with original
    result = mix(sceneColor, result, u_Intensity);

    FragColor = vec4(result, 1.0);
}
