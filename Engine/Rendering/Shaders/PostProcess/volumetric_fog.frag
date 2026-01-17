#version 330 core

// ============================================================================
// PHYSICALLY BASED VOLUMETRIC FOG
// ============================================================================
// Based on:
// - Beer-Lambert law for light extinction (absorption)
// - Henyey-Greenstein phase function for anisotropic scattering
// - Single + Multi-scattering approximation
// - Physically correct light transport
//
// Like Microsoft Flight Simulator clouds - fog ABSORBS light and only
// scatters it where the sun is visible, creating natural light shafts.
// ============================================================================

in vec2 vTexCoord;
out vec4 FragColor;

// Textures
uniform sampler2D u_SourceTexture;
uniform sampler2D u_DepthTexture;

// Camera
uniform mat4 u_InvProjection;
uniform mat4 u_InvView;
uniform vec3 u_CameraPos;
uniform float u_Time;
uniform vec2 u_ScreenSize;

// === FOG PARAMETERS ===
uniform vec3 u_FogColor;              // Fog albedo (how much light it scatters)
uniform float u_Density;              // Base fog density
uniform float u_DepthStart;           // Distance where fog starts
uniform float u_DepthEnd;             // Maximum fog distance

// === RAY MARCHING ===
uniform int u_RayMarchSteps;          // Number of ray march steps (16-64)
uniform float u_MaxRayDistance;       // Maximum ray march distance

// === HEIGHT FOG ===
uniform bool u_UseHeightFog;
uniform float u_HeightFalloff;        // Exponential height falloff
uniform float u_BaseHeight;           // Height where fog is densest
uniform float u_MaxHeight;            // Height where fog disappears

// === PHYSICALLY BASED SCATTERING ===
uniform bool u_UseSunScattering;
uniform vec3 u_SunDirection;          // Direction TO the sun (normalized)
uniform vec3 u_SunScatteringColor;    // Sun light color
uniform float u_ScatteringIntensity;  // Sun intensity multiplier
uniform float u_MieG;                 // Henyey-Greenstein anisotropy (0.7-0.95 for forward scatter)
uniform float u_ExtinctionFactor;     // Extinction coefficient (absorption + out-scatter)
uniform float u_AmbientIntensity;     // Ambient light in fog (sky contribution)

// === GOD RAYS (Radial Blur with Occlusion) ===
uniform vec2 u_SunScreenPos;          // Sun position in screen space [0,1]
uniform float u_GodRaysIntensity;     // God rays strength
uniform float u_GodRaysDensity;       // Radial blur density
uniform float u_GodRaysDecay;         // Decay per sample

// === NOISE ===
uniform bool u_UseNoise;
uniform float u_NoiseScale;
uniform float u_NoiseSpeed;
uniform float u_NoiseStrength;
uniform int u_NoiseOctaves;

// ============================================================================
// CONSTANTS
// ============================================================================

const float PI = 3.14159265359;

// ============================================================================
// NOISE FUNCTIONS - 3D Simplex noise
// ============================================================================

vec3 mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec4 mod289(vec4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec4 permute(vec4 x) { return mod289(((x * 34.0) + 1.0) * x); }
vec4 taylorInvSqrt(vec4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

float snoise(vec3 v) {
    const vec2 C = vec2(1.0 / 6.0, 1.0 / 3.0);
    const vec4 D = vec4(0.0, 0.5, 1.0, 2.0);

    vec3 i = floor(v + dot(v, C.yyy));
    vec3 x0 = v - i + dot(i, C.xxx);

    vec3 g = step(x0.yzx, x0.xyz);
    vec3 l = 1.0 - g;
    vec3 i1 = min(g.xyz, l.zxy);
    vec3 i2 = max(g.xyz, l.zxy);

    vec3 x1 = x0 - i1 + C.xxx;
    vec3 x2 = x0 - i2 + C.yyy;
    vec3 x3 = x0 - D.yyy;

    i = mod289(i);
    vec4 p = permute(permute(permute(
        i.z + vec4(0.0, i1.z, i2.z, 1.0))
        + i.y + vec4(0.0, i1.y, i2.y, 1.0))
        + i.x + vec4(0.0, i1.x, i2.x, 1.0));

    float n_ = 0.142857142857;
    vec3 ns = n_ * D.wyz - D.xzx;

    vec4 j = p - 49.0 * floor(p * ns.z * ns.z);
    vec4 x_ = floor(j * ns.z);
    vec4 y_ = floor(j - 7.0 * x_);

    vec4 x = x_ * ns.x + ns.yyyy;
    vec4 y = y_ * ns.x + ns.yyyy;
    vec4 h = 1.0 - abs(x) - abs(y);

    vec4 b0 = vec4(x.xy, y.xy);
    vec4 b1 = vec4(x.zw, y.zw);

    vec4 s0 = floor(b0) * 2.0 + 1.0;
    vec4 s1 = floor(b1) * 2.0 + 1.0;
    vec4 sh = -step(h, vec4(0.0));

    vec4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    vec4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

    vec3 p0 = vec3(a0.xy, h.x);
    vec3 p1 = vec3(a0.zw, h.y);
    vec3 p2 = vec3(a1.xy, h.z);
    vec3 p3 = vec3(a1.zw, h.w);

    vec4 norm = taylorInvSqrt(vec4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;

    vec4 m = max(0.6 - vec4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, vec4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

float fbm(vec3 p, int octaves) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float maxValue = 0.0;

    for (int i = 0; i < 6; i++) {
        if (i >= octaves) break;
        value += amplitude * snoise(p * frequency);
        maxValue += amplitude;
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    return value / maxValue;
}

// ============================================================================
// PHASE FUNCTIONS
// ============================================================================

// Henyey-Greenstein phase function
// g: anisotropy (-1 = backscatter, 0 = isotropic, 1 = forward scatter)
float phaseHG(float cosTheta, float g) {
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / (4.0 * PI * pow(max(denom, 0.0001), 1.5));
}

// Dual-lobe phase function (combines forward and back scatter)
// More realistic for fog/clouds
float phaseDualLobe(float cosTheta, float g) {
    // 80% forward scatter, 20% back scatter
    float forward = phaseHG(cosTheta, g);
    float back = phaseHG(cosTheta, -g * 0.5);
    return mix(back, forward, 0.8);
}

// ============================================================================
// INTERLEAVED GRADIENT NOISE (anti-banding)
// ============================================================================

float interleavedGradientNoise(vec2 screenPos) {
    vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(magic.z * fract(dot(screenPos, magic.xy)));
}

// ============================================================================
// GOD RAYS - Radial Blur with Scene Occlusion (GPU Gems technique)
// Objects naturally block light because dark pixels don't contribute
// ============================================================================

vec3 calculateGodRays(vec2 screenUV) {
    if (!u_UseSunScattering || u_GodRaysIntensity < 0.001) return vec3(0.0);

    vec2 lightScreenPos = u_SunScreenPos;

    // Edge fade to prevent artifacts when sun is near/off screen
    float edgeMargin = 0.15;
    float sunEdgeFade = 1.0;

    if (lightScreenPos.x < 0.0) {
        sunEdgeFade *= smoothstep(-0.6, edgeMargin, lightScreenPos.x);
    } else if (lightScreenPos.x > 1.0) {
        sunEdgeFade *= smoothstep(1.6, 1.0 - edgeMargin, lightScreenPos.x);
    }
    if (lightScreenPos.y < 0.0) {
        sunEdgeFade *= smoothstep(-0.6, edgeMargin, lightScreenPos.y);
    } else if (lightScreenPos.y > 1.0) {
        sunEdgeFade *= smoothstep(1.6, 1.0 - edgeMargin, lightScreenPos.y);
    }

    if (sunEdgeFade < 0.01) return vec3(0.0);

    // Radial blur parameters
    const int NUM_SAMPLES = 64;
    float density = u_GodRaysDensity;
    float decay = u_GodRaysDecay;
    float weight = 0.5;
    float exposure = u_GodRaysIntensity * 0.5;

    // Direction from pixel toward light source
    vec2 deltaTexCoord = screenUV - lightScreenPos;
    deltaTexCoord *= (1.0 / float(NUM_SAMPLES)) * density;

    // Dithering to reduce banding
    vec2 pixelCoord = screenUV * u_ScreenSize;
    float dither = interleavedGradientNoise(pixelCoord);

    // Start sampling with dithered offset
    vec2 sampleUV = screenUV - deltaTexCoord * dither;
    vec3 godRayColor = vec3(0.0);
    float illuminationDecay = pow(decay, dither);

    for (int i = 0; i < NUM_SAMPLES; i++) {
        // Step toward the light source
        sampleUV -= deltaTexCoord;

        // Check bounds
        bool outOfBounds = sampleUV.x < 0.0 || sampleUV.x > 1.0 ||
                          sampleUV.y < 0.0 || sampleUV.y > 1.0;

        if (outOfBounds) {
            illuminationDecay *= decay;
            continue;
        }

        // Edge fade for samples near screen border
        float sampleEdgeFade = 1.0;
        float fadeMargin = 0.05;
        sampleEdgeFade *= smoothstep(0.0, fadeMargin, sampleUV.x);
        sampleEdgeFade *= smoothstep(1.0, 1.0 - fadeMargin, sampleUV.x);
        sampleEdgeFade *= smoothstep(0.0, fadeMargin, sampleUV.y);
        sampleEdgeFade *= smoothstep(1.0, 1.0 - fadeMargin, sampleUV.y);

        // Sample the scene - THIS IS WHERE OCCLUSION HAPPENS
        // Bright areas (sky/sun) contribute to god rays
        // Dark areas (trees/objects) BLOCK the light
        vec3 sampleColor = texture(u_SourceTexture, sampleUV).rgb;

        // Luminance-based contribution - only bright areas create rays
        float lum = dot(sampleColor, vec3(0.2126, 0.7152, 0.0722));

        // Threshold to extract bright areas (sky, sun)
        // Lower threshold = more rays, higher = only very bright areas
        float brightMask = smoothstep(0.3, 1.0, lum);

        // Apply mask - dark objects don't contribute (occlusion!)
        sampleColor *= brightMask;

        // Accumulate with decay
        float sampleWeight = illuminationDecay * weight * sampleEdgeFade;
        godRayColor += sampleColor * sampleWeight;

        // Decay for next iteration
        illuminationDecay *= decay;
    }

    // Apply exposure and sun color tint
    godRayColor *= exposure * sunEdgeFade;
    godRayColor *= u_SunScatteringColor;

    return godRayColor;
}

// ============================================================================
// DEPTH RECONSTRUCTION
// ============================================================================

vec3 reconstructWorldPos(vec2 uv, float depth) {
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewPos = u_InvProjection * ndc;
    viewPos /= viewPos.w;
    vec4 worldPos = u_InvView * viewPos;
    return worldPos.xyz;
}

// ============================================================================
// DENSITY SAMPLING
// ============================================================================

float sampleDensity(vec3 pos) {
    float density = u_Density;

    // Height-based density falloff
    if (u_UseHeightFog) {
        float heightAboveBase = pos.y - u_BaseHeight;

        if (heightAboveBase > 0.0) {
            // Exponential falloff above base height
            density *= exp(-heightAboveBase * u_HeightFalloff);
        } else {
            // Below base height - increase density
            density *= 1.0 + abs(heightAboveBase) * u_HeightFalloff * 0.3;
        }

        // Cutoff above max height
        if (pos.y > u_MaxHeight) {
            float aboveMax = pos.y - u_MaxHeight;
            density *= exp(-aboveMax * u_HeightFalloff * 3.0);
        }
    }

    // Noise-based density variation
    if (u_UseNoise && density > 0.0001) {
        vec3 noisePos = pos * u_NoiseScale;
        noisePos += vec3(u_Time * u_NoiseSpeed * 0.3, u_Time * u_NoiseSpeed * 0.2, u_Time * u_NoiseSpeed * 0.25);

        float noiseValue = fbm(noisePos, u_NoiseOctaves);
        noiseValue = noiseValue * 0.5 + 0.5; // Remap to [0, 1]

        // Apply noise - can create holes in fog for light shafts
        density *= mix(1.0 - u_NoiseStrength, 1.0 + u_NoiseStrength * 0.5, noiseValue);
    }

    return max(density, 0.0);
}

// ============================================================================
// PHYSICALLY BASED RAY MARCHING
// ============================================================================
//
// The fog ABSORBS light (Beer-Lambert) and only ADDS light where sun reaches.
// This creates the natural darkening effect and visible light shafts.
//
// Transmittance T = exp(-extinction * distance)
// In-scattered light L = integral of (phase * sunLight * density * T) along ray
//

struct FogResult {
    vec3 inScattering;    // Light scattered INTO the viewing ray
    float transmittance;  // How much background light passes through
};

FogResult rayMarchFog(vec3 rayOrigin, vec3 rayDir, float maxDist) {
    FogResult result;
    result.inScattering = vec3(0.0);
    result.transmittance = 1.0;

    // Clamp ray distance
    float rayDist = min(maxDist, u_MaxRayDistance);

    // Skip if too close
    if (rayDist < u_DepthStart) {
        return result;
    }

    float startDist = max(u_DepthStart, 0.1);
    float effectiveRayDist = rayDist - startDist;

    if (effectiveRayDist < 0.01) {
        return result;
    }

    // Step size
    int steps = max(u_RayMarchSteps, 8);
    float stepSize = effectiveRayDist / float(steps);

    // Dithering to reduce banding
    float dither = fract(sin(dot(vTexCoord * u_ScreenSize, vec2(12.9898, 78.233))) * 43758.5453);

    // Pre-calculate sun scattering
    vec3 sunDir = normalize(u_SunDirection);
    float cosTheta = dot(rayDir, sunDir);

    // Phase function - how much light scatters toward camera
    float phase = phaseDualLobe(cosTheta, u_MieG);

    // Sun light contribution (directional)
    vec3 sunLight = u_SunScatteringColor * u_ScatteringIntensity;

    // Ambient light (from sky, fills shadows)
    vec3 ambientLight = u_FogColor * u_AmbientIntensity;

    // Ray march
    for (int i = 0; i < 64; i++) {
        if (i >= steps) break;
        if (result.transmittance < 0.005) break; // Early termination

        float t = startDist + (float(i) + dither) * stepSize;
        vec3 samplePos = rayOrigin + rayDir * t;

        // Sample density
        float density = sampleDensity(samplePos);

        if (density > 0.0001) {
            // ======================
            // EXTINCTION (absorption + out-scattering)
            // ======================
            // Beer-Lambert: light is absorbed/scattered away
            float extinction = density * u_ExtinctionFactor * stepSize;
            float sampleTransmittance = exp(-extinction);

            // ======================
            // IN-SCATTERING
            // ======================
            // Light that gets scattered INTO our viewing ray

            // Sun visibility based on height (simple approximation)
            // Higher = more sun, lower = more shadow
            float sunVisibility = 1.0;
            if (u_UseHeightFog) {
                float heightFactor = clamp((samplePos.y - u_BaseHeight) / max(u_MaxHeight - u_BaseHeight, 1.0), 0.0, 1.0);
                // Exponential falloff for more dramatic light shafts
                sunVisibility = pow(heightFactor, 0.5);
            }

            // Direct sun scattering (creates light shafts)
            vec3 directScatter = vec3(0.0);
            if (u_UseSunScattering) {
                directScatter = sunLight * phase * sunVisibility;
            }

            // Ambient scattering (fills shadows, prevents pure black)
            vec3 ambientScatter = ambientLight * (1.0 - sunVisibility * 0.5);

            // Total in-scattered light at this sample
            // Multiply by fog albedo (u_FogColor) to tint the scattered light
            vec3 inScatter = (directScatter + ambientScatter) * u_FogColor;

            // Integrate using emission-absorption model
            // The (1 - sampleTransmittance) term ensures energy conservation
            vec3 scatterContrib = inScatter * (1.0 - sampleTransmittance) * result.transmittance;
            result.inScattering += scatterContrib;

            // Update transmittance (light absorbed/scattered away)
            result.transmittance *= sampleTransmittance;
        }
    }

    return result;
}

// ============================================================================
// MAIN
// ============================================================================

void main() {
    vec3 sceneColor = texture(u_SourceTexture, vTexCoord).rgb;
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // Skybox - apply subtle atmospheric scattering
    if (depth >= 0.9999) {
        if (u_UseSunScattering) {
            vec3 rayDir = normalize(reconstructWorldPos(vTexCoord, 0.5) - u_CameraPos);
            vec3 sunDir = normalize(u_SunDirection);
            float cosTheta = dot(rayDir, sunDir);
            float phase = phaseDualLobe(cosTheta, u_MieG);

            // Subtle sun glow on sky
            float sunGlow = phase * u_ScatteringIntensity * 0.1 * u_Density;
            sceneColor += u_SunScatteringColor * sunGlow;
        }
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    // Reconstruct world position
    vec3 worldPos = reconstructWorldPos(vTexCoord, depth);

    if (any(isnan(worldPos)) || any(isinf(worldPos))) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    vec3 rayDir = normalize(worldPos - u_CameraPos);
    float dist = length(worldPos - u_CameraPos);

    if (isnan(dist) || isinf(dist) || dist < 0.01) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    // Ray march through fog
    FogResult fog = rayMarchFog(u_CameraPos, rayDir, dist);

    // ======================
    // PHYSICALLY CORRECT COMPOSITING
    // ======================
    // final = sceneColor * transmittance + inScattering
    //
    // - transmittance < 1 means fog ABSORBS light (darkens scene)
    // - inScattering adds light only where sun reaches

    vec3 finalColor = sceneColor * fog.transmittance + fog.inScattering;

    // ======================
    // GOD RAYS (Radial Blur with Occlusion)
    // ======================
    // Objects block light naturally because dark pixels don't contribute
    // This creates the light shaft effect like in the underwater shader

    vec3 godRays = calculateGodRays(vTexCoord);

    // God rays are attenuated by fog (scattered through the medium)
    // More fog = softer god rays
    float godRayAttenuation = mix(0.3, 1.0, fog.transmittance);
    finalColor += godRays * godRayAttenuation;

    // Clamp
    finalColor = max(finalColor, vec3(0.0));

    FragColor = vec4(finalColor, 1.0);
}
