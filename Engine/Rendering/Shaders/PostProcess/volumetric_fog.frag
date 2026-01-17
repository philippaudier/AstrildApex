#version 330 core

// ============================================================================
// VOLUMETRIC FOG - Fixed Version
// ============================================================================
// - Fog is drawn over sky
// - God rays only from actual sky (not bright objects)
// - Sun glow respects occlusion
// - Steps parameter visibly affects quality
// ============================================================================

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;
uniform sampler2D u_DepthTexture;

uniform mat4 u_InvProjection;
uniform mat4 u_InvView;
uniform vec3 u_CameraPos;
uniform float u_Time;
uniform vec2 u_ScreenSize;

// Fog parameters
uniform vec3 u_FogColor;
uniform float u_Density;
uniform float u_DepthStart;
uniform float u_DepthEnd;
uniform float u_Intensity;  // Overall effect intensity (0-1 blend with original)

// Ray marching
uniform int u_RayMarchSteps;

// Height fog
uniform bool u_UseHeightFog;
uniform float u_HeightFalloff;
uniform float u_BaseHeight;
uniform float u_MaxHeight;

// Scattering
uniform bool u_UseSunScattering;
uniform vec3 u_SunDirection;
uniform vec3 u_SunScatteringColor;
uniform float u_ScatteringIntensity;
uniform float u_MieG;
uniform float u_ExtinctionFactor;

// Scatter color source: 0=FogColor, 1=SunColor, 2=Atmospheric, 3=Custom
uniform int u_ScatterColorSource;
uniform vec3 u_InScatterColor;      // Custom inscatter color
uniform vec3 u_AmbientColor;        // Ambient fog color (sky-tinted)
uniform float u_AmbientIntensity;
uniform bool u_UseAmbientFromSky;
uniform vec3 u_SkyColor;            // Sky/atmosphere color for ambient

// God rays
uniform vec2 u_SunScreenPos;
uniform float u_GodRaysIntensity;
uniform float u_GodRaysDensity;
uniform float u_GodRaysDecay;

// Noise
uniform bool u_UseNoise;
uniform float u_NoiseScale;
uniform float u_NoiseSpeed;
uniform float u_NoiseStrength;
uniform int u_NoiseOctaves;

// Underwater detection - skip sky/fog effects when underwater
uniform bool u_IsUnderwater;
uniform float u_WaterLevel;

const float PI = 3.14159265359;

// ============================================================================
// NOISE
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

float phaseHG(float cosTheta, float g) {
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / (4.0 * PI * pow(max(denom, 0.0001), 1.5));
}

// ============================================================================
// DITHERING - Interleaved Gradient Noise
// ============================================================================

float IGN(vec2 screenPos) {
    return fract(52.9829189 * fract(dot(screenPos, vec2(0.06711056, 0.00583715))));
}

// ============================================================================
// DEPTH & POSITION
// ============================================================================

vec3 reconstructWorldPos(vec2 uv, float depth) {
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    vec4 viewPos = u_InvProjection * ndc;
    viewPos /= viewPos.w;
    vec4 worldPos = u_InvView * viewPos;
    return worldPos.xyz;
}

vec3 getViewDir(vec2 uv) {
    vec4 ndc = vec4(uv * 2.0 - 1.0, 1.0, 1.0);
    vec4 view = u_InvProjection * ndc;
    view.xyz /= view.w;
    return normalize((u_InvView * vec4(view.xyz, 0.0)).xyz);
}

// ============================================================================
// DENSITY
// ============================================================================

float sampleDensity(vec3 pos) {
    float density = u_Density;

    if (u_UseHeightFog) {
        float heightAboveBase = pos.y - u_BaseHeight;
        if (heightAboveBase > 0.0) {
            density *= exp(-heightAboveBase * u_HeightFalloff);
        } else {
            density *= 1.0 + abs(heightAboveBase) * u_HeightFalloff * 0.3;
        }
        if (pos.y > u_MaxHeight) {
            density *= exp(-(pos.y - u_MaxHeight) * u_HeightFalloff * 3.0);
        }
    }

    if (u_UseNoise && density > 0.0001) {
        vec3 noisePos = pos * u_NoiseScale;
        noisePos += vec3(u_Time * u_NoiseSpeed * 0.3, u_Time * u_NoiseSpeed * 0.2, u_Time * u_NoiseSpeed * 0.25);
        float noiseValue = fbm(noisePos, u_NoiseOctaves) * 0.5 + 0.5;
        density *= mix(1.0 - u_NoiseStrength, 1.0 + u_NoiseStrength * 0.5, noiseValue);
    }

    return max(density, 0.0);
}

// ============================================================================
// GOD RAYS - Only from actual sky, with direction check
// ============================================================================

vec3 calculateGodRays(vec2 screenUV, vec3 viewDir, vec3 sunDir) {
    if (!u_UseSunScattering || u_GodRaysIntensity < 0.001) return vec3(0.0);

    // CRITICAL: Only show god rays when looking TOWARD the sun
    float facingSun = dot(viewDir, sunDir);
    if (facingSun < 0.0) return vec3(0.0); // Looking away from sun

    vec2 lightScreenPos = u_SunScreenPos;

    // Sun must be reasonably on screen
    if (lightScreenPos.x < -0.5 || lightScreenPos.x > 1.5 ||
        lightScreenPos.y < -0.5 || lightScreenPos.y > 1.5) {
        return vec3(0.0);
    }

    // Edge fade
    float sunEdgeFade = 1.0;
    sunEdgeFade *= smoothstep(-0.3, 0.1, lightScreenPos.x);
    sunEdgeFade *= smoothstep(1.3, 0.9, lightScreenPos.x);
    sunEdgeFade *= smoothstep(-0.3, 0.1, lightScreenPos.y);
    sunEdgeFade *= smoothstep(1.3, 0.9, lightScreenPos.y);

    if (sunEdgeFade < 0.01) return vec3(0.0);

    // Radial blur toward sun
    const int NUM_SAMPLES = 48;
    vec2 deltaUV = (screenUV - lightScreenPos) / float(NUM_SAMPLES) * u_GodRaysDensity;

    float dither = IGN(screenUV * u_ScreenSize);
    vec2 sampleUV = screenUV - deltaUV * dither;

    vec3 godRays = vec3(0.0);
    float illumination = pow(u_GodRaysDecay, dither);

    for (int i = 0; i < NUM_SAMPLES; i++) {
        sampleUV -= deltaUV;

        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0) {
            illumination *= u_GodRaysDecay;
            continue;
        }

        vec3 sampleColor = texture(u_SourceTexture, sampleUV).rgb;
        float sampleDepth = texture(u_DepthTexture, sampleUV).r;

        // ONLY sky pixels contribute - this prevents bright objects from creating rays
        float isSky = step(0.9999, sampleDepth);

        // Brightness threshold
        float brightness = dot(sampleColor, vec3(0.299, 0.587, 0.114));
        float brightMask = smoothstep(0.4, 0.9, brightness);

        // Must be BOTH sky AND bright
        float contribution = isSky * brightMask;

        godRays += sampleColor * contribution * illumination;
        illumination *= u_GodRaysDecay;
    }

    godRays *= u_GodRaysIntensity * 0.3 * sunEdgeFade;
    godRays *= u_SunScatteringColor;

    // Fade based on angle to sun (stronger when looking directly at sun)
    godRays *= smoothstep(0.0, 0.5, facingSun);

    return godRays;
}

// ============================================================================
// RAY MARCHING
// ============================================================================

struct FogResult {
    vec3 inScattering;
    float transmittance;
};

FogResult rayMarchFog(vec3 rayOrigin, vec3 rayDir, float maxDist, bool isSky) {
    FogResult result;
    result.inScattering = vec3(0.0);
    result.transmittance = 1.0;

    // For sky, use DepthEnd; for objects, use min of object distance and DepthEnd
    float rayDist = isSky ? u_DepthEnd : min(maxDist, u_DepthEnd);

    if (rayDist < u_DepthStart) return result;

    float startDist = max(u_DepthStart, 0.1);
    float effectiveDist = rayDist - startDist;
    if (effectiveDist < 0.01) return result;

    // Distance-based fog fade (smooth falloff near DepthEnd)
    float distanceFade = 1.0;
    if (u_DepthEnd > u_DepthStart) {
        float fadeStart = u_DepthEnd * 0.7; // Start fading at 70% of end distance
        distanceFade = 1.0 - smoothstep(fadeStart, u_DepthEnd, rayDist);
    }

    // Use actual step count from parameter
    int steps = clamp(u_RayMarchSteps, 4, 64);
    float stepSize = effectiveDist / float(steps);

    // Dithering - more visible with fewer steps
    vec2 screenPos = vTexCoord * u_ScreenSize;
    float dither = IGN(screenPos);

    vec3 sunDir = normalize(u_SunDirection);
    float cosTheta = dot(rayDir, sunDir);
    float phase = phaseHG(cosTheta, u_MieG);

    // Determine inscatter color based on source mode
    vec3 scatterColor;
    if (u_ScatterColorSource == 0) {
        // FogColor mode (original behavior)
        scatterColor = u_FogColor;
    } else if (u_ScatterColorSource == 1) {
        // SunColor mode - use actual sun light color
        scatterColor = u_SunScatteringColor;
    } else if (u_ScatterColorSource == 2) {
        // Atmospheric mode - blend sun and sky based on view direction
        float skyBlend = max(0.0, rayDir.y); // More sky color when looking up
        scatterColor = mix(u_SunScatteringColor, u_SkyColor, skyBlend * 0.5);
    } else {
        // Custom mode - use explicit inscatter color
        scatterColor = u_InScatterColor;
    }

    vec3 sunLight = u_SunScatteringColor * u_ScatteringIntensity;

    // Ambient color: use sky color if enabled, otherwise use explicit ambient color
    vec3 ambientColor = u_UseAmbientFromSky ? u_SkyColor : u_AmbientColor;
    vec3 ambientLight = ambientColor * u_AmbientIntensity;

    // Ray march with explicit step count
    for (int i = 0; i < 64; i++) {
        if (i >= steps) break;
        if (result.transmittance < 0.01) break;

        float t = startDist + (float(i) + dither) * stepSize;
        vec3 samplePos = rayOrigin + rayDir * t;

        float density = sampleDensity(samplePos);

        if (density > 0.0001) {
            float extinction = density * u_ExtinctionFactor * stepSize;
            float sampleT = exp(-extinction);

            // Sun visibility (height-based approximation)
            float sunVis = 1.0;
            if (u_UseHeightFog) {
                float hFactor = clamp((samplePos.y - u_BaseHeight) / max(u_MaxHeight - u_BaseHeight, 1.0), 0.0, 1.0);
                sunVis = pow(hFactor, 0.5);
            }

            // Direct scattering: sun light * phase * visibility, tinted by scatter color
            vec3 directScatter = u_UseSunScattering ? sunLight * phase * sunVis * scatterColor : vec3(0.0);

            // Ambient scattering: fills shadows, more in occluded areas
            vec3 ambientScatter = ambientLight * (1.0 - sunVis * 0.5);

            // Final inscatter (no longer multiply by fog color - already applied above)
            vec3 inScatter = directScatter + ambientScatter;

            result.inScattering += inScatter * (1.0 - sampleT) * result.transmittance;
            result.transmittance *= sampleT;
        }
    }

    // Apply distance-based fade (fog fades out near DepthEnd)
    // But NOT for sky - sky should always get full fog effect
    if (!isSky) {
        result.inScattering *= distanceFade;
        result.transmittance = mix(1.0, result.transmittance, distanceFade);
    }

    return result;
}

// ============================================================================
// MAIN
// ============================================================================

void main() {
    vec3 sceneColor = texture(u_SourceTexture, vTexCoord).rgb;
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // When underwater, skip volumetric fog entirely - let water surface be visible
    if (u_IsUnderwater) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    bool isSky = (depth >= 0.9999);
    vec3 rayDir = getViewDir(vTexCoord);
    vec3 sunDir = normalize(u_SunDirection);

    vec3 finalColor;

    if (isSky) {
        // Sky pixel - still apply fog (dense fog can block sky)
        FogResult fog = rayMarchFog(u_CameraPos, rayDir, u_DepthEnd, true);

        // Sky is attenuated by fog
        finalColor = sceneColor * fog.transmittance + fog.inScattering;

        // Sun glow - ONLY if looking toward sun (phase function naturally handles this)
        // The phase function already makes glow strongest when looking at sun
        if (u_UseSunScattering) {
            float cosTheta = dot(rayDir, sunDir);
            // Smooth falloff instead of hard cutoff to avoid visible triangle artifacts
            float glowFalloff = smoothstep(0.7, 0.98, cosTheta);
            if (glowFalloff > 0.0) {
                float sunGlow = phaseHG(cosTheta, u_MieG) * u_ScatteringIntensity * 0.05 * glowFalloff;
                // Attenuate glow by fog transmittance (dense fog blocks sun)
                finalColor += u_SunScatteringColor * sunGlow * fog.transmittance;
            }
        }
    } else {
        // Scene object
        vec3 worldPos = reconstructWorldPos(vTexCoord, depth);
        float dist = length(worldPos - u_CameraPos);

        if (isnan(dist) || isinf(dist) || dist < 0.01) {
            FragColor = vec4(sceneColor, 1.0);
            return;
        }

        FogResult fog = rayMarchFog(u_CameraPos, rayDir, dist, false);
        finalColor = sceneColor * fog.transmittance + fog.inScattering;
    }

    // God rays - only when looking toward sun and from sky pixels only
    vec3 godRays = calculateGodRays(vTexCoord, rayDir, sunDir);
    finalColor += godRays;

    // Apply overall effect intensity (blend between original and fog result)
    finalColor = mix(sceneColor, finalColor, u_Intensity);

    FragColor = vec4(max(finalColor, vec3(0.0)), 1.0);
}
