#version 330 core

// ============================================================================
// UNDERWATER VOLUME POST-PROCESS SHADER
// Subnautica-style AAA underwater rendering
// ============================================================================

in vec2 vTexCoord;
out vec4 fragColor;

// Textures
uniform sampler2D u_SourceTexture;
uniform sampler2D u_DepthTexture;

// Camera & Transform
uniform mat4 u_InvProjection;
uniform mat4 u_InvView;
uniform vec3 u_CameraPos;
uniform float u_Time;
uniform vec2 u_ScreenSize;

// Master
uniform float u_WaterLevel = 0.0;

// Volumetric Fog (ray-marched)
uniform int u_FogEnabled = 1;
uniform vec3 u_FogColor = vec3(0.01, 0.05, 0.12);
uniform float u_FogDensity = 0.02;
uniform float u_Visibility = 50.0;
uniform int u_FogSteps = 32;                   // Ray march steps
uniform float u_FogScattering = 0.8;           // Forward scattering (Mie g parameter)
uniform float u_FogAmbient = 0.15;             // Ambient light in fog
uniform float u_FogHeightFalloff = 0.02;       // Density increase with depth
uniform float u_FogNoiseScale = 0.05;          // 3D noise scale for density variation
uniform float u_FogNoiseStrength = 0.3;        // How much noise affects density

// Absorption
uniform int u_AbsorptionEnabled = 1;
uniform float u_AbsorptionR = 0.45;
uniform float u_AbsorptionG = 0.08;
uniform float u_AbsorptionB = 0.02;

// God rays (GPU Gems radial blur technique)
uniform int u_GodRaysEnabled = 1;
uniform float u_GodRaysIntensity = 0.8;
uniform vec3 u_GodRaysColor = vec3(0.8, 0.9, 1.0);
uniform float u_GodRaysDensity = 0.5;
uniform float u_GodRaysDecay = 0.95;
uniform int u_GodRaysSamples = 32;
uniform vec3 u_SunDirection = vec3(0.3, -0.8, 0.3);
uniform vec2 u_SunScreenPos = vec2(0.5, 0.8);  // Sun position in screen space [0,1]

// Particles (volumetric with depth and lighting)
uniform int u_ParticlesEnabled = 1;
uniform float u_ParticleDensity = 0.5;
uniform vec3 u_ParticleColor = vec3(0.8, 0.85, 0.9);
uniform float u_ParticleBrightness = 0.15;
uniform float u_ParticleSpeed = 0.1;
uniform float u_ParticleSizeMin = 0.5;           // Minimum particle size
uniform float u_ParticleSizeMax = 3.0;           // Maximum particle size
uniform int u_ParticleDepthLayers = 5;           // Number of depth layers for volumetric effect
uniform float u_ParticleLighting = 0.8;          // How much particles react to light (0-1)
uniform float u_ParticleScattering = 0.6;        // Forward scattering when looking at sun
uniform float u_ParticleTurbulence = 0.3;        // Random movement intensity
uniform float u_ParticleGodRayGlow = 0.5;        // Extra glow when particle is in god ray
uniform float u_ParticleFocusDistance = 10.0;    // Focus distance for depth blur
uniform float u_ParticleFocusRange = 20.0;       // Focus range (DoF)
uniform float u_ParticleNearFade = 2.0;          // Fade particles too close to camera
uniform float u_ParticleFarFade = 80.0;          // Fade particles too far from camera
uniform sampler2D u_ParticleTexture;             // Optional particle texture
uniform int u_ParticleTextureEnabled = 0;        // Whether to use particle texture

// Caustics (GPU Gems inspired with chromatic aberration)
uniform int u_CausticsEnabled = 1;
uniform float u_CausticsIntensity = 0.5;
uniform float u_CausticsScale = 1.0;
uniform float u_CausticsSpeed = 1.0;
uniform int u_CausticsOctaves = 3;
uniform float u_CausticsBrightness = 1.0;
uniform float u_CausticsSharpness = 3.0;
uniform float u_CausticsDistortion = 0.5;
uniform float u_CausticsDepthFalloff = 0.2;
uniform float u_CausticsChromatic = 0.05;

// Tint & Ambient
uniform vec3 u_TintColor = vec3(0.1, 0.3, 0.5);
uniform float u_AmbientIntensity = 0.15;
uniform vec3 u_AmbientColor = vec3(0.1, 0.2, 0.3);

// Wave parameters (from WaterPlaneComponent for accurate caustics)
uniform int u_WaveIterations = 8;
uniform float u_WaveAmplitude = 1.0;
uniform float u_WaveFrequency = 1.0;
uniform float u_WaveSpeed = 2.0;
uniform float u_WaveSteepness = 0.5;
uniform float u_WaveDrag = 0.38;
uniform vec2 u_WaveDirection = vec2(1.0, 0.3);

// Screen Distortion (underwater refraction effect)
uniform int u_DistortionEnabled = 1;
uniform float u_DistortionIntensity = 0.02;      // Overall distortion strength
uniform float u_DistortionScale = 1.0;           // Scale of distortion pattern
uniform float u_DistortionSpeed = 1.0;           // Animation speed
uniform float u_DistortionChromatic = 0.005;     // RGB separation for distortion
uniform int u_DistortionUseWaves = 1;            // Use Gerstner waves for distortion (vs simple noise)
uniform float u_DistortionWaveInfluence = 0.5;   // How much waves affect distortion
uniform float u_DistortionNoiseInfluence = 0.5;  // How much noise affects distortion
uniform float u_DistortionDepthFade = 0.02;      // Fade distortion with depth (more at surface)

// Snell's Window (total internal reflection when looking up from underwater)
uniform int u_SnellWindowEnabled = 1;
uniform float u_SnellCriticalAngle = 48.6;       // Critical angle in degrees (water/air = 48.6 deg)
uniform float u_SnellEdgeSoftness = 0.1;         // Softness of the window edge (0 = hard, 1 = soft)
uniform vec3 u_SnellReflectionTint = vec3(0.05, 0.15, 0.25); // Color tint for reflected underwater
uniform float u_SnellReflectionStrength = 0.8;   // How much of underwater is reflected (0-1)
uniform float u_SnellFresnelPower = 3.0;         // Fresnel effect at window edge
uniform float u_SnellWaveDistortion = 0.5;       // How much waves distort the window edge (0-1)
uniform int u_SnellUsePlanarReflection = 1;     // Use planar reflection texture instead of procedural
uniform sampler2D u_PlanarReflectionTex;        // Planar reflection texture
uniform mat4 u_ReflectionViewProj;              // Reflection camera view-projection matrix

// Water Transition Effects (air/water surface crossing)
uniform int u_TransitionEnabled = 0;
uniform float u_TransitionProgress = 0.0;       // 1.0 = just transitioned, 0.0 = transition complete
uniform int u_TransitionDirection = 1;          // 1 = entering water, 0 = exiting water
// Entering water (bubbles)
uniform float u_EnterBubbleIntensity = 0.8;
uniform float u_EnterBubbleSize = 0.05;
uniform float u_EnterBubbleCount = 30.0;
uniform float u_EnterDistortion = 0.15;
// Exiting water (droplets)
uniform float u_ExitDropletIntensity = 0.8;
uniform float u_ExitDropletSize = 0.08;
uniform float u_ExitDropletCount = 20.0;
uniform float u_ExitDripSpeed = 0.5;
uniform int u_ExitTransitionOnly = 0;           // When true, skip underwater effects, only apply droplets

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

// Hash function for noise
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

// 3D noise
float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float n = i.x + i.y * 157.0 + 113.0 * i.z;
    vec4 h1 = vec4(hash(vec2(n, 0.0)), hash(vec2(n + 1.0, 0.0)),
                   hash(vec2(n + 157.0, 0.0)), hash(vec2(n + 158.0, 0.0)));
    vec4 h2 = vec4(hash(vec2(n + 113.0, 0.0)), hash(vec2(n + 114.0, 0.0)),
                   hash(vec2(n + 270.0, 0.0)), hash(vec2(n + 271.0, 0.0)));

    float xy1 = mix(mix(h1.x, h1.y, f.x), mix(h1.z, h1.w, f.x), f.y);
    float xy2 = mix(mix(h2.x, h2.y, f.x), mix(h2.z, h2.w, f.x), f.y);
    return mix(xy1, xy2, f.z);
}

// Interleaved Gradient Noise - high quality dithering pattern
// Source: http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare
float interleavedGradientNoise(vec2 screenPos) {
    vec3 magic = vec3(0.06711056, 0.00583715, 52.9829189);
    return fract(magic.z * fract(dot(screenPos, magic.xy)));
}

// Reconstruct world position from depth
vec3 reconstructWorldPos(vec2 uv, float depth) {
    // Convert to NDC
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

    // Transform to view space
    vec4 viewPos = u_InvProjection * ndc;
    viewPos /= viewPos.w;

    // Transform to world space
    vec4 worldPos = u_InvView * viewPos;
    return worldPos.xyz;
}

// Linear depth from depth buffer
float linearizeDepth(float depth) {
    float near = 0.1;
    float far = 1000.0;
    return (2.0 * near * far) / (far + near - depth * (far - near));
}

// Get view ray direction from screen UV (for Snell's window)
// This gives the actual direction we're looking, independent of what geometry is there
vec3 getViewRayDirection(vec2 uv) {
    // Convert UV to NDC
    vec2 ndc = uv * 2.0 - 1.0;

    // Create a point on the far plane in NDC
    vec4 farPoint = vec4(ndc, 1.0, 1.0);

    // Transform to view space
    vec4 viewDir = u_InvProjection * farPoint;
    viewDir.xyz /= viewDir.w;

    // Transform direction to world space (only rotation, no translation)
    vec3 worldDir = mat3(u_InvView) * viewDir.xyz;

    return normalize(worldDir);
}

// ============================================================================
// GERSTNER WAVE FUNCTIONS (matches WaterOcean.frag for accurate caustics)
// ============================================================================

// Gerstner wave derivative for normal calculation
vec2 wavedx(vec2 position, vec2 direction, float frequency, float timeshift) {
    float x = dot(direction, position) * frequency + timeshift;
    float wave = exp(sin(x) - 1.0);
    float dx = wave * cos(x);
    return vec2(wave, -dx);
}

// Get wave height at a position (same algorithm as WaterOcean.frag)
float getWaveHeight(vec2 position, int iterations) {
    float wavePhaseShift = length(position) * 0.1;
    float iter = 0.0;
    float frequency = u_WaveFrequency;
    float timeMultiplier = u_WaveSpeed;
    float weight = 1.0;
    float sumOfValues = 0.0;
    float sumOfWeights = 0.0;

    vec2 waveDir = normalize(u_WaveDirection);

    for(int i = 0; i < 48; i++) {
        if (i >= iterations) break;

        float angle = iter * 0.5;
        vec2 dir = vec2(
            waveDir.x * cos(angle) - waveDir.y * sin(angle),
            waveDir.x * sin(angle) + waveDir.y * cos(angle)
        );

        vec2 res = wavedx(position, dir, frequency, u_Time * timeMultiplier + wavePhaseShift);
        position += dir * res.y * weight * u_WaveDrag;

        sumOfValues += res.x * weight;
        sumOfWeights += weight;

        weight = mix(weight, 0.0, 0.2);
        frequency *= 1.18;
        timeMultiplier *= 1.07;
        iter += 1232.399963;
    }

    return (sumOfValues / sumOfWeights) * u_WaveAmplitude;
}

// Calculate water surface normal at a position using Gerstner waves
vec3 calculateWaterSurfaceNormal(vec2 pos) {
    // Use fewer iterations for performance (caustics don't need as much detail)
    int iterations = min(u_WaveIterations, 12);
    float epsilon = 0.1; // Sample distance for normal calculation

    float H = getWaveHeight(pos, iterations);
    float Hx = getWaveHeight(pos - vec2(epsilon, 0.0), iterations);
    float Hz = getWaveHeight(pos + vec2(0.0, epsilon), iterations);

    vec3 a = vec3(pos.x, H, pos.y);
    vec3 normal = normalize(cross(
        a - vec3(pos.x - epsilon, Hx, pos.y),
        a - vec3(pos.x, Hz, pos.y + epsilon)
    ));

    return normal;
}

// ============================================================================
// SCREEN DISTORTION
// ============================================================================

// 2D noise for distortion
float noise2D(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// FBM noise for smoother distortion
float fbmNoise(vec2 p, int octaves) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;

    for (int i = 0; i < 4; i++) {
        if (i >= octaves) break;
        value += amplitude * noise2D(p * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }

    return value;
}

// Calculate screen distortion offset
// Returns UV offset to apply to texture sampling
vec2 calculateDistortion(vec2 uv, vec2 worldPosXZ, float cameraDepth) {
    if (u_DistortionEnabled == 0) return vec2(0.0);

    vec2 distortion = vec2(0.0);
    float t = u_Time * u_DistortionSpeed;

    // Wave-based distortion (uses actual Gerstner waves)
    if (u_DistortionUseWaves > 0 && u_DistortionWaveInfluence > 0.0) {
        // Get wave normal at the surface position above the camera
        vec3 waveNormal = calculateWaterSurfaceNormal(worldPosXZ);
        // Use XZ components of normal as distortion
        vec2 waveDistortion = waveNormal.xz * u_DistortionWaveInfluence;
        distortion += waveDistortion;
    }

    // Noise-based distortion (adds fine detail)
    if (u_DistortionNoiseInfluence > 0.0) {
        vec2 noiseUV = uv * u_DistortionScale * 10.0;

        // Multi-layer animated noise for organic feel
        float noise1 = fbmNoise(noiseUV + vec2(t * 0.3, t * 0.2), 3);
        float noise2 = fbmNoise(noiseUV * 1.5 + vec2(-t * 0.2, t * 0.4), 3);

        vec2 noiseDistortion = vec2(
            noise1 * 2.0 - 1.0,
            noise2 * 2.0 - 1.0
        ) * u_DistortionNoiseInfluence;

        distortion += noiseDistortion;
    }

    // Depth-based fade (more distortion near surface, less deep down)
    float depthFade = exp(-cameraDepth * u_DistortionDepthFade);
    depthFade = mix(0.3, 1.0, depthFade); // Keep some distortion even at depth

    // Apply intensity and depth fade
    distortion *= u_DistortionIntensity * depthFade;

    return distortion;
}

// Sample scene with distortion and optional chromatic aberration
vec3 sampleSceneDistorted(vec2 uv, vec2 distortion) {
    if (u_DistortionChromatic > 0.001) {
        // Chromatic aberration: sample RGB with slight offsets
        vec2 chromaticOffset = normalize(distortion + vec2(0.001)) * u_DistortionChromatic;

        float r = texture(u_SourceTexture, clamp(uv + distortion + chromaticOffset, 0.001, 0.999)).r;
        float g = texture(u_SourceTexture, clamp(uv + distortion, 0.001, 0.999)).g;
        float b = texture(u_SourceTexture, clamp(uv + distortion - chromaticOffset, 0.001, 0.999)).b;

        return vec3(r, g, b);
    } else {
        return texture(u_SourceTexture, clamp(uv + distortion, 0.001, 0.999)).rgb;
    }
}

// ============================================================================
// UNDERWATER EFFECTS
// ============================================================================

// Beer-Lambert absorption
vec3 applyAbsorption(vec3 color, float depth) {
    if (u_AbsorptionEnabled == 0) return color;

    vec3 absorption = vec3(u_AbsorptionR, u_AbsorptionG, u_AbsorptionB);
    vec3 transmittance = exp(-absorption * depth);
    return color * transmittance;
}

// ============================================================================
// VOLUMETRIC UNDERWATER FOG - AAA Quality
// ============================================================================
// Based on the atmospheric volumetric fog implementation:
// - Beer-Lambert law for light extinction (absorption)
// - Henyey-Greenstein phase function for anisotropic scattering
// - Physically correct light transport
// - Smooth depth-based density gradient
// - IGN dithering for banding-free results
//
// Key difference from atmospheric fog: water absorbs light differently per
// wavelength (red absorbed first, blue travels furthest), creating the
// characteristic blue-green underwater look.
// ============================================================================

const float PI = 3.14159265359;

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
// More realistic for underwater light scattering
float phaseDualLobe(float cosTheta, float g) {
    // Water: 85% forward scatter, 15% back scatter (particles, plankton)
    float forward = phaseHG(cosTheta, g);
    float back = phaseHG(cosTheta, -g * 0.4);
    return mix(back, forward, 0.85);
}

// ============================================================================
// DENSITY SAMPLING
// ============================================================================

// Sample fog density at a world position underwater
float sampleUnderwaterDensity(vec3 worldPos) {
    // Base density
    float density = u_FogDensity;

    // Depth below water surface
    float depthBelowSurface = max(0.0, u_WaterLevel - worldPos.y);

    // Depth-based density increase (water gets murkier with depth)
    // Using exponential for smooth falloff
    if (u_FogHeightFalloff > 0.0) {
        float depthFactor = 1.0 + depthBelowSurface * u_FogHeightFalloff;
        density *= depthFactor;
    }

    // 3D noise for organic variation (currents, sediment, thermal layers)
    if (u_FogNoiseStrength > 0.001) {
        vec3 noisePos = worldPos * u_FogNoiseScale;
        // Animate slowly - underwater currents
        noisePos += vec3(u_Time * 0.015, u_Time * 0.008, u_Time * 0.012);

        // Multi-octave noise
        float noise = 0.0;
        float amp = 0.5;
        float freq = 1.0;
        for (int i = 0; i < 3; i++) {
            noise += amp * noise3D(noisePos * freq);
            amp *= 0.5;
            freq *= 2.0;
        }
        noise = noise * 0.5 + 0.5; // Remap to [0, 1]

        // Apply noise - subtle variation to avoid uniform look
        density *= mix(1.0 - u_FogNoiseStrength * 0.5, 1.0 + u_FogNoiseStrength * 0.3, noise);
    }

    return max(density, 0.0);
}

// ============================================================================
// VOLUMETRIC FOG RESULT
// ============================================================================

struct UnderwaterFogResult {
    vec3 inScattering;    // Light scattered INTO the viewing ray
    float transmittance;  // How much background light passes through (0-1)
};

// ============================================================================
// RAY MARCHING
// ============================================================================

UnderwaterFogResult rayMarchUnderwaterFog(vec3 rayOrigin, vec3 rayDir, float maxDist, float cameraDepth, vec2 screenUV) {
    UnderwaterFogResult result;
    result.inScattering = vec3(0.0);
    result.transmittance = 1.0;

    if (u_FogEnabled == 0) return result;

    // Ray march parameters
    int steps = clamp(u_FogSteps, 8, 64);
    float rayLength = min(maxDist, u_Visibility * 2.0);

    if (rayLength < 0.1) return result;

    float stepSize = rayLength / float(steps);

    // === IGN DITHERING (anti-banding) ===
    vec2 pixelCoord = screenUV * u_ScreenSize;
    float dither = interleavedGradientNoise(pixelCoord);

    // === LIGHT SETUP ===
    vec3 sunDir = normalize(-u_SunDirection);
    float sunVisibility = max(0.0, sunDir.y); // Sun above water surface

    // View-to-sun angle for phase function
    float cosTheta = dot(rayDir, sunDir);
    float phase = phaseDualLobe(cosTheta, u_FogScattering);

    // Water absorption coefficients (wavelength-dependent)
    vec3 waterAbsorption = vec3(u_AbsorptionR, u_AbsorptionG, u_AbsorptionB);

    // Light colors
    vec3 sunColor = u_GodRaysColor * sunVisibility;
    vec3 ambientColor = u_FogColor * u_FogAmbient;

    // Extinction factor for fog (separate from water absorption)
    float extinctionFactor = 1.0;

    // === RAY MARCH LOOP ===
    for (int i = 0; i < 64; i++) {
        if (i >= steps) break;
        if (result.transmittance < 0.005) break; // Early termination

        // Sample position with dithered offset
        float t = (float(i) + dither) * stepSize;
        vec3 samplePos = rayOrigin + rayDir * t;

        // Skip if above water
        if (samplePos.y > u_WaterLevel) continue;

        // Depth at sample point
        float sampleDepth = max(0.0, u_WaterLevel - samplePos.y);

        // Sample fog density
        float density = sampleUnderwaterDensity(samplePos);

        if (density > 0.0001) {
            // =====================
            // EXTINCTION (Beer-Lambert)
            // =====================
            // Combined extinction from fog particles and water absorption
            float fogExtinction = density * extinctionFactor * stepSize;
            float sampleTransmittance = exp(-fogExtinction);

            // =====================
            // IN-SCATTERING
            // =====================
            // Light reaching this point from above

            // Sun light attenuation through water (depth-dependent)
            // Light travels from surface down to sample point
            vec3 sunAttenuation = exp(-waterAbsorption * sampleDepth * 0.5);

            // Overall light reduction with depth (exponential falloff)
            float depthFalloff = exp(-sampleDepth * 0.025);

            // Direct sun scattering
            vec3 directScatter = sunColor * sunAttenuation * depthFalloff * phase;

            // Ambient scattering (sky light diffused through water)
            vec3 ambientScatter = ambientColor * exp(-waterAbsorption * sampleDepth * 0.3);
            // Ambient is stronger deeper (no direct sun) - inverse relationship
            ambientScatter *= (1.0 - depthFalloff * 0.5);

            // Total in-scattered light at this sample
            vec3 inScatter = (directScatter + ambientScatter) * u_FogColor;

            // Energy-conserving integration
            // The (1 - sampleTransmittance) term ensures conservation
            vec3 scatterContrib = inScatter * (1.0 - sampleTransmittance) * result.transmittance;
            result.inScattering += scatterContrib;

            // Update transmittance
            result.transmittance *= sampleTransmittance;
        }
    }

    // Clamp results
    result.transmittance = clamp(result.transmittance, 0.0, 1.0);

    // Visibility hard cutoff with smooth blend
    float visibilityFade = 1.0 - smoothstep(u_Visibility * 0.6, u_Visibility, maxDist);

    // Beyond visibility: fade to fog color
    if (visibilityFade < 1.0) {
        float beyondVis = 1.0 - visibilityFade;
        // Darken transmittance and add fog color
        result.transmittance *= visibilityFade;
        result.inScattering = mix(result.inScattering, u_FogColor * u_FogAmbient, beyondVis * 0.7);
    }

    return result;
}

// ============================================================================
// APPLY UNDERWATER FOG
// ============================================================================

vec3 applyUnderwaterFog(vec3 sceneColor, float dist, float cameraDepth, vec3 rayOrigin, vec3 rayDir, vec2 screenUV) {
    if (u_FogEnabled == 0) return sceneColor;

    // Ray march through underwater volume
    UnderwaterFogResult fog = rayMarchUnderwaterFog(rayOrigin, rayDir, dist, cameraDepth, screenUV);

    // =====================
    // PHYSICALLY CORRECT COMPOSITING
    // =====================
    // final = scene * transmittance + inScattering
    //
    // - transmittance < 1 means fog ABSORBS light (darkens distant objects)
    // - inScattering adds light scattered from sun/sky

    vec3 finalColor = sceneColor * fog.transmittance + fog.inScattering;

    return finalColor;
}

// =============================================================================
// GOD RAYS - GPU Gems 3 Chapter 13: Volumetric Light Scattering
// https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process
// =============================================================================
//
// Formula: L(s) = Exposure * SUM[ Weight * Decay^i * Sample(s_i) ]
//
// The technique samples the scene along rays from each pixel TOWARD the light
// source. Bright pixels (sky/light) contribute to the shafts, dark pixels
// (occluders) block them. This creates rays emanating FROM the light source.
//
// ANTI-BANDING: Uses Interleaved Gradient Noise (IGN) for temporal/spatial
// dithering to break up visible stepping artifacts.
// Note: interleavedGradientNoise() is defined in HELPER FUNCTIONS section above.

vec3 calculateGodRays(vec3 worldPos, vec3 viewDir, float cameraDepth, vec2 screenUV) {
    if (u_GodRaysEnabled == 0) return vec3(0.0);

    // Sun must be above horizon
    vec3 sunDir = normalize(-u_SunDirection);
    if (sunDir.y < 0.05) return vec3(0.0);

    // =========================
    // GPU GEMS PARAMETERS
    // =========================
    const int NUM_SAMPLES = 64;  // Reduced since dithering hides banding
    float Density = u_GodRaysDensity;
    float Decay = u_GodRaysDecay;
    float Weight = 0.6;
    float Exposure = u_GodRaysIntensity * 0.35;

    // =========================
    // SCREEN SPACE LIGHT POSITION
    // =========================
    vec2 lightScreenPos = u_SunScreenPos;

    // =========================
    // EDGE ARTIFACT PREVENTION
    // =========================
    // Fade out god rays when sun is near or outside screen edges
    // This prevents the vertical/horizontal streaking artifacts
    float edgeMargin = 0.1;
    float sunEdgeFade = 1.0;

    // Fade when sun is outside or near screen bounds
    if (lightScreenPos.x < 0.0) {
        sunEdgeFade *= smoothstep(-0.5, edgeMargin, lightScreenPos.x);
    } else if (lightScreenPos.x > 1.0) {
        sunEdgeFade *= smoothstep(1.5, 1.0 - edgeMargin, lightScreenPos.x);
    }
    if (lightScreenPos.y < 0.0) {
        sunEdgeFade *= smoothstep(-0.5, edgeMargin, lightScreenPos.y);
    } else if (lightScreenPos.y > 1.0) {
        sunEdgeFade *= smoothstep(1.5, 1.0 - edgeMargin, lightScreenPos.y);
    }

    // Early out if sun is too far off screen
    if (sunEdgeFade < 0.01) return vec3(0.0);

    // =========================
    // CALCULATE DELTA TEXCOORD
    // =========================
    // Vector from current pixel toward the light source
    vec2 deltaTexCoord = screenUV - lightScreenPos;

    // Scale by density and number of samples
    // This determines how far we sample along the ray
    deltaTexCoord *= (1.0 / float(NUM_SAMPLES)) * Density;

    // =========================
    // ANTI-BANDING DITHERING
    // =========================
    // Use Interleaved Gradient Noise for high-quality dithering
    // This breaks up the visible stepping by offsetting the ray start
    vec2 pixelCoord = screenUV * u_ScreenSize;
    float dither = interleavedGradientNoise(pixelCoord);

    // =========================
    // RADIAL BLUR SAMPLING
    // =========================
    // Start with dithered offset to break banding
    vec2 sampleUV = screenUV - deltaTexCoord * dither;
    vec3 color = vec3(0.0);
    float illuminationDecay = pow(Decay, dither); // Compensate decay for dither offset
    float totalWeight = 0.0;

    for (int i = 0; i < NUM_SAMPLES; i++) {
        // Step TOWARD the light source
        sampleUV -= deltaTexCoord;

        // Check if sample is out of bounds - stop accumulating to prevent edge artifacts
        bool outOfBounds = sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0;

        // Edge fade for samples approaching screen border
        float sampleEdgeFade = 1.0;
        float fadeMargin = 0.05;
        sampleEdgeFade *= smoothstep(0.0, fadeMargin, sampleUV.x);
        sampleEdgeFade *= smoothstep(1.0, 1.0 - fadeMargin, sampleUV.x);
        sampleEdgeFade *= smoothstep(0.0, fadeMargin, sampleUV.y);
        sampleEdgeFade *= smoothstep(1.0, 1.0 - fadeMargin, sampleUV.y);

        // Skip samples that are out of bounds
        if (outOfBounds) {
            illuminationDecay *= Decay;
            continue;
        }

        // Sample the scene with slight jitter for extra smoothness
        float sampleJitter = interleavedGradientNoise(pixelCoord + vec2(float(i) * 1.7, float(i) * 2.3));
        vec2 jitteredUV = sampleUV + (sampleJitter - 0.5) * deltaTexCoord * 0.5;
        jitteredUV = clamp(jitteredUV, 0.001, 0.999);

        vec3 sampleColor = texture(u_SourceTexture, jitteredUV).rgb;

        // Apply decay, weight, and edge fade
        float sampleWeight = illuminationDecay * Weight * sampleEdgeFade;
        sampleColor *= sampleWeight;
        totalWeight += sampleWeight;

        // Accumulate
        color += sampleColor;

        // Exponential decay for next iteration
        illuminationDecay *= Decay;
    }

    // Normalize by total weight for consistent brightness
    if (totalWeight > 0.001) {
        color /= totalWeight;
        color *= totalWeight * 0.015; // Scale back to expected range
    }

    // Apply exposure and sun edge fade
    color *= Exposure * sunEdgeFade;

    // =========================
    // UNDERWATER MODIFICATIONS
    // =========================

    // Depth attenuation - rays fade with camera depth
    float depthAtten = exp(-cameraDepth * 0.03);
    depthAtten = mix(0.15, 1.0, depthAtten);
    color *= depthAtten;

    // Tint with god ray color
    color *= u_GodRaysColor;

    // Depth-based blue shift
    vec3 deepTint = vec3(0.6, 0.8, 1.0);
    float tintFactor = smoothstep(0.0, 25.0, cameraDepth);
    color *= mix(vec3(1.0), deepTint, tintFactor * 0.3);

    return color;
}

// ============================================================================
// AAA-QUALITY UNDERWATER PARTICLES (Marine Snow)
// ============================================================================
// Inspired by Subnautica, ABZÛ, and other AAA underwater games
// Features:
// - Distinct individual particles (not noise)
// - Bokeh-style depth of field blur
// - Strong parallax between depth layers
// - Rim/back lighting when facing sun
// - Sparse distribution for realism
// ============================================================================

// High-quality hash for particle generation
float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

vec2 hash22(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx + p3.yz) * p3.zy);
}

vec3 hash33(vec3 p3) {
    p3 = fract(p3 * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return fract((p3.xxy + p3.yxx) * p3.zyx);
}

// Single distinct particle at a grid cell
// Returns: (distance to particle center, particle properties hash)
vec4 getParticleInCell(vec3 cellId, vec3 localPos, float time) {
    // Random position within cell (0-1)
    vec3 rand = hash33(cellId);

    // Particle exists probability (sparse distribution)
    float existsProb = rand.x;
    if (existsProb > u_ParticleDensity) {
        return vec4(1000.0, 0.0, 0.0, 0.0); // No particle
    }

    // Particle position within cell
    vec3 particlePos = rand * 0.8 + 0.1; // Keep away from edges

    // Animation - gentle floating motion
    float timeOffset = rand.y * 100.0;
    float phase = time * u_ParticleSpeed + timeOffset;

    // Upward drift + gentle sway
    particlePos.y -= fract(time * u_ParticleSpeed * 0.5 + rand.z);
    particlePos.x += sin(phase * 0.7) * u_ParticleTurbulence * 0.15;
    particlePos.z += cos(phase * 0.5) * u_ParticleTurbulence * 0.15;

    // Wrap position
    particlePos = fract(particlePos);

    // Distance from sample point to particle center
    vec3 diff = localPos - particlePos;
    float dist = length(diff);

    return vec4(dist, rand);
}

// Bokeh-style soft circle for out-of-focus particles
float bokehShape(float dist, float radius, float softness) {
    // Sharp edge when in focus, soft when blurred
    float edge = smoothstep(radius, radius * (1.0 - softness), dist);

    // Add slight ring effect for bokeh look
    float ring = smoothstep(radius * 0.3, radius * 0.5, dist);
    ring *= smoothstep(radius, radius * 0.7, dist);

    return mix(edge, edge * (0.7 + ring * 0.3), softness);
}

// Calculate particles for a single depth layer
vec4 sampleParticleLayer(vec3 worldPos, vec3 viewDir, float layerDist, float cameraDepth, int layerIndex) {
    // Sample position at this depth
    vec3 samplePos = u_CameraPos + viewDir * layerDist;

    // Skip if above water
    if (samplePos.y > u_WaterLevel) return vec4(0.0);

    float depthBelowSurface = max(0.0, u_WaterLevel - samplePos.y);

    // Grid scale - larger = fewer, bigger particles
    float gridScale = mix(3.0, 8.0, u_ParticleDensity);

    // Different grid offset per layer for variety
    vec3 layerOffset = vec3(float(layerIndex) * 17.3, float(layerIndex) * 23.7, float(layerIndex) * 31.1);

    // Scale position to grid
    vec3 scaledPos = samplePos * gridScale + layerOffset;
    vec3 cellId = floor(scaledPos);
    vec3 localPos = fract(scaledPos);

    // Check this cell and neighbors for particles
    vec4 result = vec4(0.0);
    float closestDist = 1000.0;
    vec3 closestRand = vec3(0.0);

    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            for (int z = -1; z <= 1; z++) {
                vec3 neighborCell = cellId + vec3(float(x), float(y), float(z));
                vec3 neighborLocal = localPos - vec3(float(x), float(y), float(z));

                vec4 particle = getParticleInCell(neighborCell, neighborLocal, u_Time);

                if (particle.x < closestDist) {
                    closestDist = particle.x;
                    closestRand = particle.yzw;
                }
            }
        }
    }

    // No particle nearby
    if (closestDist > 0.5) return vec4(0.0);

    // Particle properties from hash
    float sizeVar = closestRand.x;
    float brightnessVar = closestRand.y;
    float typeVar = closestRand.z;

    // === SIZE & DEPTH OF FIELD ===
    float baseSize = mix(u_ParticleSizeMin, u_ParticleSizeMax, sizeVar) / gridScale;

    // Depth of field - blur based on distance from focus plane
    float focusDist = abs(layerDist - u_ParticleFocusDistance);
    float defocus = smoothstep(0.0, u_ParticleFocusRange, focusDist);

    // Bokeh size increase when out of focus
    float bokehSize = baseSize * (1.0 + defocus * 3.0);
    float bokehSoftness = defocus * 0.8;

    // Particle shape
    float alpha = bokehShape(closestDist, bokehSize, bokehSoftness);

    if (alpha < 0.001) return vec4(0.0);

    // === LIGHTING ===
    vec3 sunDir = normalize(-u_SunDirection);

    // Surface light falloff
    float surfaceLight = exp(-depthBelowSurface * 0.04);

    // Back-lighting (rim light when particle between camera and sun)
    float backLight = max(0.0, dot(-viewDir, sunDir));
    backLight = pow(backLight, 3.0) * u_ParticleScattering;

    // Fresnel-like edge glow
    float edgeGlow = smoothstep(bokehSize * 0.2, bokehSize * 0.5, closestDist);
    edgeGlow *= backLight * 0.5;

    // God ray intersection boost
    float godRayBoost = 0.0;
    if (u_ParticleGodRayGlow > 0.0) {
        // Simple approximation of god ray pattern
        vec2 rayUV = samplePos.xz * 0.05;
        float rayPattern = sin(rayUV.x * 5.0 + u_Time * 0.1) * 0.5 + 0.5;
        rayPattern *= sin(rayUV.y * 4.0 - u_Time * 0.08) * 0.5 + 0.5;
        godRayBoost = rayPattern * surfaceLight * u_ParticleGodRayGlow * max(0.0, sunDir.y);
    }

    // Combined lighting
    float lighting = surfaceLight * u_ParticleLighting + 0.2;
    lighting += backLight * surfaceLight;
    lighting += godRayBoost;

    // === COLOR ===
    vec3 absorption = vec3(u_AbsorptionR, u_AbsorptionG, u_AbsorptionB);
    vec3 transmittance = exp(-absorption * depthBelowSurface * 0.3);

    vec3 color = u_ParticleColor;
    color *= 0.7 + brightnessVar * 0.6; // Brightness variation
    color *= transmittance;
    color *= lighting;

    // Warm rim light tint
    color += vec3(1.0, 0.95, 0.85) * backLight * surfaceLight * 0.4;

    // Edge highlight
    color += vec3(0.8, 0.9, 1.0) * edgeGlow;

    // === DISTANCE FADING ===
    float nearFade = smoothstep(u_ParticleNearFade * 0.5, u_ParticleNearFade * 2.0, layerDist);
    float farFade = 1.0 - smoothstep(u_ParticleFarFade * 0.6, u_ParticleFarFade, layerDist);
    float depthFade = exp(-depthBelowSurface * 0.015);
    depthFade = mix(0.3, 1.0, depthFade);

    float fade = nearFade * farFade * depthFade;

    // Defocused particles slightly dimmer
    float defocusDim = 1.0 - defocus * 0.3;

    // Final alpha
    float finalAlpha = alpha * fade * u_ParticleBrightness * defocusDim;

    return vec4(color * finalAlpha, finalAlpha);
}

// Main particle function - samples multiple depth layers with parallax
vec3 calculateParticles(vec3 worldPos, vec3 viewDir, float sceneDepth, float cameraDepth) {
    if (u_ParticlesEnabled == 0) return vec3(0.0);

    vec3 result = vec3(0.0);
    float totalAlpha = 0.0;

    // Number of depth layers (more = better quality, slower)
    int numLayers = clamp(u_ParticleDepthLayers, 2, 12);

    // Distance range
    float minDist = u_ParticleNearFade;
    float maxDist = min(sceneDepth, u_ParticleFarFade);

    if (maxDist <= minDist) return vec3(0.0);

    // Use exponential distribution for depth layers (more detail near camera)
    for (int i = 0; i < 12; i++) {
        if (i >= numLayers) break;
        if (totalAlpha > 0.9) break;

        // Exponential depth distribution
        float t = float(i) / float(numLayers - 1);
        float layerDist = minDist + (maxDist - minDist) * (1.0 - exp(-t * 3.0)) / (1.0 - exp(-3.0));

        // Add slight randomization to prevent alignment
        float jitter = hash11(float(i) * 13.7 + vTexCoord.x * 100.0 + vTexCoord.y * 57.0) * 0.3;
        layerDist += jitter * (maxDist - minDist) / float(numLayers);

        // Sample this layer
        vec4 layerParticle = sampleParticleLayer(worldPos, viewDir, layerDist, cameraDepth, i);

        // Front-to-back compositing
        float remainingAlpha = 1.0 - totalAlpha;
        result += layerParticle.rgb * remainingAlpha;
        totalAlpha += layerParticle.a * remainingAlpha;
    }

    return result;
}

// GPU Gems Caustics with Chromatic Aberration
// Simulates refracted light patterns on underwater surfaces
vec3 calculateCaustics(vec3 worldPos, float depth, vec3 surfaceNormal) {
    if (u_CausticsEnabled == 0 || depth < 0.01) return vec3(0.0);

    // Base UV coordinates scaled by caustics scale
    vec2 causticsUV = worldPos.xz * u_CausticsScale;
    float time = u_Time * u_CausticsSpeed;

    // GPU Gems: Use water surface normal to simulate light refraction
    // Light bends when passing from air (n=1.0) to water (n=1.33)
    const float IOR_WATER = 1.33;
    const float IOR_AIR = 1.0;
    float refractionRatio = IOR_AIR / IOR_WATER;

    // Approximate refraction displacement using surface normal
    vec2 refractionOffset = surfaceNormal.xz * u_CausticsDistortion * refractionRatio;

    // RGB channels with chromatic separation (simulates light dispersion)
    vec3 caustic = vec3(0.0);

    // Multi-octave caustics for detail (GPU Gems technique)
    int octaves = clamp(u_CausticsOctaves, 1, 6);

    for (int oct = 0; oct < 6; oct++) {
        if (oct >= octaves) break;

        float octaveScale = pow(2.0, float(oct));
        float octaveWeight = 1.0 / octaveScale;

        // Chromatic separation: slight offset per channel (red, green, blue)
        vec2 offsetR = vec2(0.0, 0.0);
        vec2 offsetG = vec2(u_CausticsChromatic * 0.3, u_CausticsChromatic * 0.2);
        vec2 offsetB = vec2(u_CausticsChromatic * 0.6, u_CausticsChromatic * 0.4);

        // Two layers moving in different directions (GPU Gems technique)
        for (int layer = 0; layer < 2; layer++) {
            float layerAngle = float(layer) * 1.571 + float(oct) * 0.5; // PI/2 rotation
            float layerSpeed = (layer == 0) ? 1.0 : -0.7;

            vec2 dir = vec2(cos(layerAngle), sin(layerAngle));
            vec2 uvBase = causticsUV * octaveScale + dir * time * layerSpeed;

            // Add distortion: refraction from surface + animated procedural
            vec2 proceduralDistortion = vec2(
                sin(uvBase.y * 6.0 + time * 0.5),
                cos(uvBase.x * 6.0 - time * 0.3)
            ) * 0.1 / octaveScale;

            vec2 distortion = refractionOffset + proceduralDistortion;

            // Sample caustic pattern for each channel with chromatic offset
            vec2 uvR = uvBase + distortion + offsetR;
            vec2 uvG = uvBase + distortion + offsetG;
            vec2 uvB = uvBase + distortion + offsetB;

            // Caustic pattern: based on GPU Gems intersection method
            // Using trigonometric functions to simulate refracted light focusing
            float patternR = abs(sin(uvR.x * 10.0) * sin(uvR.y * 10.0));
            float patternG = abs(sin(uvG.x * 10.0) * sin(uvG.y * 10.0));
            float patternB = abs(sin(uvB.x * 10.0) * sin(uvB.y * 10.0));

            // Sharpen caustics (GPU Gems: focused light rays)
            patternR = pow(patternR, u_CausticsSharpness);
            patternG = pow(patternG, u_CausticsSharpness);
            patternB = pow(patternB, u_CausticsSharpness);

            // Accumulate with octave weight
            caustic.r += patternR * octaveWeight;
            caustic.g += patternG * octaveWeight;
            caustic.b += patternB * octaveWeight;
        }
    }

    // Normalize by total weight (sum of 1 + 0.5 + 0.25 + ... for N octaves)
    float totalWeight = (1.0 - pow(0.5, float(octaves))) / 0.5;
    caustic /= totalWeight;

    // Depth-based attenuation (GPU Gems: light absorption with depth)
    float depthFade = exp(-depth * u_CausticsDepthFalloff);

    // Attenuation based on distance to water surface (caustics stronger near surface)
    float surfaceProximity = 1.0 - smoothstep(0.0, 30.0, depth);
    surfaceProximity = mix(0.3, 1.0, surfaceProximity);

    // Apply brightness and intensity
    caustic *= u_CausticsBrightness * u_CausticsIntensity * depthFade * surfaceProximity;

    return clamp(caustic, 0.0, 1.0);
}

// ============================================================================
// SNELL'S WINDOW (Total Internal Reflection) - Physically correct implementation
// Based on: https://godotshaders.com/shader/snells-window/
// ============================================================================

// Index of refraction for water/air interface
#define IOR_WATER 1.333
#define F0_WATER 0.02

// Physically correct Snell's window calculation
// normal: water surface normal (typically vec3(0,1,0) for flat water)
// viewDir: direction from surface point towards camera (normalized)
// ior: index of refraction (1.333 for water)
// Returns: 1.0 if inside window (can see above), 0.0 if outside (total internal reflection)
float snellsWindow(vec3 normal, vec3 viewDir, float ior) {
    // Cosine of angle between view direction and surface normal
    float cosTheta = dot(normal, viewDir);

    // Sine using trigonometric identity: sin^2 + cos^2 = 1
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));

    // Snell's law: n1 * sin(theta1) = n2 * sin(theta2)
    // For water->air: sin(theta2) = sin(theta1) * n_water / n_air = sin(theta1) * 1.333
    // Total internal reflection when sin(theta2) > 1.0
    // i.e., when sinTheta * ior > 1.0

    // step(sinTheta * ior, 1.0) returns:
    // 1.0 when sinTheta * ior <= 1.0 (inside window, can see through)
    // 0.0 when sinTheta * ior > 1.0 (outside window, total reflection)
    return step(sinTheta * ior, 1.0);
}

// Soft version with smooth transition at window edge
float snellsWindowSmooth(vec3 normal, vec3 viewDir, float ior, float softness) {
    float cosTheta = dot(normal, viewDir);
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
    float criticalValue = sinTheta * ior;

    // Smooth transition around the critical angle
    return 1.0 - smoothstep(1.0 - softness, 1.0 + softness * 0.5, criticalValue);
}

// Fresnel reflectance using Schlick's approximation
float fresnelSchlick(float cosTheta, float f0) {
    return f0 + (1.0 - f0) * pow(1.0 - cosTheta, 5.0);
}

// ============================================================================
// WATER TRANSITION EFFECT (FBM-based distortion for both enter/exit)
// ============================================================================

// Unified water transition effect using FBM distortion
// direction: 1.0 = exiting water (drips down), -1.0 = entering water (splashes up)
vec4 calculateWaterTransition(vec2 uv, float progress, float direction) {
    if (progress <= 0.0) return vec4(0.0);

    vec2 totalDistortion = vec2(0.0);
    float totalAlpha = 0.0;
    vec3 tintColor = vec3(0.0);

    // Time factor for animation (slows down as effect fades)
    float t = u_Time * u_ExitDripSpeed * 0.5;

    // Progress curve - stronger at start, smooth fade out
    float intensity = progress * progress * progress; // Cubic falloff
    float distortionStrength = u_ExitDropletIntensity * intensity;

    // === LAYER 1: Large-scale organic warping ===
    // Low frequency FBM for overall lens distortion
    vec2 largeNoiseUV = uv * 2.0 + vec2(t * 0.1, -t * 0.15);
    float largeNoise1 = fbmNoise(largeNoiseUV, 3);
    float largeNoise2 = fbmNoise(largeNoiseUV + vec2(5.3, 2.7), 3);

    vec2 largeDistortion = vec2(
        largeNoise1 * 2.0 - 1.0,
        largeNoise2 * 2.0 - 1.0
    ) * 0.04 * distortionStrength;

    totalDistortion += largeDistortion;

    // === LAYER 2: Medium detail ripples ===
    // Mid frequency for water-like ripple patterns
    vec2 mediumNoiseUV = uv * 6.0 + vec2(-t * 0.3, t * 0.2);
    float mediumNoise1 = fbmNoise(mediumNoiseUV, 4);
    float mediumNoise2 = fbmNoise(mediumNoiseUV + vec2(3.1, 7.4), 4);

    vec2 mediumDistortion = vec2(
        mediumNoise1 * 2.0 - 1.0,
        mediumNoise2 * 2.0 - 1.0
    ) * 0.025 * distortionStrength;

    totalDistortion += mediumDistortion;

    // === LAYER 3: Fine detail shimmer ===
    // High frequency for surface tension / fine details
    vec2 fineNoiseUV = uv * 15.0 + vec2(t * 0.5, -t * 0.4);
    float fineNoise1 = fbmNoise(fineNoiseUV, 2);
    float fineNoise2 = fbmNoise(fineNoiseUV + vec2(1.7, 4.2), 2);

    vec2 fineDistortion = vec2(
        fineNoise1 * 2.0 - 1.0,
        fineNoise2 * 2.0 - 1.0
    ) * 0.01 * distortionStrength;

    totalDistortion += fineDistortion;

    // === EDGE VIGNETTE ===
    // Effect is stronger at edges (like water draining from center)
    vec2 centered = uv - 0.5;
    float edgeDist = length(centered) * 1.4;
    float edgeFactor = smoothstep(0.2, 0.8, edgeDist);

    // Boost distortion at edges
    totalDistortion *= (1.0 + edgeFactor * 0.5);

    // === DIRECTIONAL FLOW ===
    // Exiting water: distortion flows DOWN (gravity, water dripping)
    // Entering water: distortion flows UP (splash, displacement)
    float flowBias = (1.0 - progress) * 0.03 * u_ExitDropletSize;
    totalDistortion.y -= flowBias * direction * (1.0 + largeNoise1 * 0.5);

    // Add horizontal splash for entering water (impact spread)
    if (direction < 0.0) {
        float splashSpread = progress * progress * 0.02 * u_ExitDropletSize;
        totalDistortion.x += (uv.x - 0.5) * splashSpread * largeNoise2;
    }

    // === VISUAL OVERLAY ===
    // Subtle wet film tint (refraction causes slight color shift)
    float filmPattern = fbmNoise(uv * 8.0 + vec2(t * 0.2, -t * 0.15 * direction), 3);
    float filmIntensity = intensity * 0.3 * filmPattern;

    // Scale distortion by size parameter
    totalDistortion *= u_ExitDropletSize;

    // Chromatic-like effect intensity based on distortion magnitude
    float distortionMag = length(totalDistortion) * 15.0;
    totalAlpha = filmIntensity + distortionMag * intensity;

    // Return format: xy = distortion, z = tint factor, w = alpha
    return vec4(totalDistortion.x, totalDistortion.y, filmIntensity, totalAlpha);
}

// ============================================================================
// MAIN
// ============================================================================

void main() {
    // =========================================================================
    // EXIT TRANSITION ONLY MODE
    // When exiting water, apply FBM-based distortion effect on normal scene
    // =========================================================================
    if (u_ExitTransitionOnly > 0 && u_TransitionEnabled > 0 && u_TransitionProgress > 0.0) {
        // Sample the source texture (normal above-water scene)
        vec3 color = texture(u_SourceTexture, vTexCoord).rgb;

        // Apply water transition effect (FBM distortion)
        // Returns: xy = distortion, z = tint factor, w = alpha
        // direction = 1.0 for exit (drips down)
        vec4 transitionEffect = calculateWaterTransition(vTexCoord, u_TransitionProgress, 1.0);

        // Apply FBM-based screen distortion
        vec2 fbmDistortion = transitionEffect.xy * u_TransitionProgress;
        vec2 distortedUV = clamp(vTexCoord + fbmDistortion, 0.0, 1.0);
        vec3 distortedColor = texture(u_SourceTexture, distortedUV).rgb;

        // Blend based on distortion magnitude (stronger distortion = more visible)
        float blendFactor = clamp(length(fbmDistortion) * 15.0, 0.0, 1.0);
        color = mix(color, distortedColor, blendFactor);

        // Subtle blue tint for wet lens feel
        float tintFactor = transitionEffect.z;
        color = mix(color, color * vec3(0.92, 0.96, 1.08), tintFactor);

        // Slight brightness variation based on distortion (refraction effect)
        color *= 1.0 + (transitionEffect.w * 0.1 - 0.05);

        fragColor = vec4(color, 1.0);
        return;
    }

    // Sample depth first (undistorted for accurate world reconstruction)
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // Reconstruct world position (undistorted)
    vec3 worldPos = reconstructWorldPos(vTexCoord, depth);
    float linearDepth = linearizeDepth(depth);

    // Camera depth below water
    float cameraDepth = max(0.0, u_WaterLevel - u_CameraPos.y);

    // View direction (normalized direction from camera to world position)
    vec3 viewDir = normalize(worldPos - u_CameraPos);

    // Distance from camera to pixel
    float dist = length(worldPos - u_CameraPos);

    // =========================================================================
    // DETECT IF LOOKING AT SOMETHING ABOVE WATER (fixes white environment issue)
    // =========================================================================
    bool pixelAboveWater = worldPos.y > u_WaterLevel;

    // Calculate where the view ray hits the water surface (for Snell's window)
    float distToWaterSurface = dist;
    vec3 waterSurfacePoint = worldPos; // Default to world pos
    if (viewDir.y > 0.001) {
        // Ray going upward - calculate intersection with water plane
        float t = (u_WaterLevel - u_CameraPos.y) / viewDir.y;
        distToWaterSurface = max(0.1, t);
        waterSurfacePoint = u_CameraPos + viewDir * distToWaterSurface;
    }

    // Effective distance for underwater effects (clamped to water surface if looking above)
    float effectiveDist = pixelAboveWater ? distToWaterSurface : dist;

    // Pixel depth below water (0 if above water)
    float pixelDepth = max(0.0, u_WaterLevel - worldPos.y);

    // =========================================================================
    // SNELL'S WINDOW (Total Internal Reflection) - Physically Correct
    // =========================================================================
    // Calculate the water surface normal at the point where view ray hits the surface
    // This creates wave-distorted edges on the Snell's window
    vec3 flatNormal = vec3(0.0, 1.0, 0.0);
    vec3 waterNormal = flatNormal;

    if (u_SnellWaveDistortion > 0.0 && viewDir.y > 0.01) {
        // Get the wave-distorted normal at the water surface intersection point
        vec3 waveNormal = calculateWaterSurfaceNormal(waterSurfacePoint.xz);

        // Blend between flat normal and wave normal based on distortion amount
        // Also reduce distortion with depth (waves are less visible from deep underwater)
        float distortionFade = exp(-cameraDepth * 0.05);
        float effectiveDistortion = u_SnellWaveDistortion * distortionFade;

        waterNormal = normalize(mix(flatNormal, waveNormal, effectiveDistortion));
    }

    // For Snell's window, we need the view direction pointing FROM the water surface
    // towards the camera (opposite of our viewDir which points from camera to scene)
    vec3 viewDirToCamera = -viewDir;

    // Calculate the window factor using physically correct Snell's law
    // windowFactor = 1.0 means inside the window (can see above water)
    // windowFactor = 0.0 means outside the window (total internal reflection)
    float windowFactor = 0.0;
    float fresnelFactor = 0.0;

    if (u_SnellWindowEnabled > 0) {
        // Depth-dependent edge softness:
        // - Shallow water: sharper edge (light travels less distance, less scattering)
        // - Deep water: softer edge (more scattering, diffusion)
        float depthSoftness = u_SnellEdgeSoftness * (1.0 + cameraDepth * 0.05);
        depthSoftness = clamp(depthSoftness, u_SnellEdgeSoftness, u_SnellEdgeSoftness * 3.0);

        // Use the smooth version for nice edge transition
        // The wave-distorted waterNormal creates organic, moving edges
        windowFactor = snellsWindowSmooth(waterNormal, viewDirToCamera, IOR_WATER, depthSoftness);

        // Calculate Fresnel effect at the edge of the window
        float cosTheta = max(0.0, dot(waterNormal, viewDirToCamera));
        fresnelFactor = fresnelSchlick(cosTheta, F0_WATER);

        // Depth affects visibility through the window:
        // - Shallow: clear view, high contrast between window and reflection
        // - Deep: murky, window content fades towards reflection color
        float depthFade = exp(-cameraDepth * 0.03); // Gradual fade with depth
        windowFactor *= mix(0.3, 1.0, depthFade); // At depth, window becomes less distinct
    }

    // =========================================================================
    // SCREEN DISTORTION
    // =========================================================================
    vec2 distortionWorldPos = u_CameraPos.xz + (vTexCoord - 0.5) * 50.0;
    vec2 screenDistortion = calculateDistortion(vTexCoord, distortionWorldPos, cameraDepth);

    // =========================================================================
    // SAMPLE SCENE COLOR
    // =========================================================================
    // Sample the main scene (what we're looking at)
    vec3 sceneColor = sampleSceneDistorted(vTexCoord, screenDistortion);

    // =========================================================================
    // PROCESS BASED ON ABOVE/BELOW WATER
    // =========================================================================
    vec3 color;

    if (pixelAboveWater) {
        // Looking at something above water surface (sky, terrain above water, etc.)
        vec3 aboveWaterColor = sceneColor;

        // Apply water surface distortion to the above-water view
        float surfaceWave = sin(vTexCoord.x * 30.0 + u_Time) * 0.02 + cos(vTexCoord.y * 25.0 + u_Time * 0.7) * 0.02;
        aboveWaterColor *= (1.0 + surfaceWave * 0.5);

        // Snell's window effect: outside the critical angle, we see total internal reflection
        if (u_SnellWindowEnabled > 0 && viewDir.y > 0.01) {
            vec3 reflectedColor;

            // === TOTAL INTERNAL REFLECTION WITH VISIBLE WAVES ===
            // In reality, even in TIR zone, you see the water surface waves
            // The waves modulate the reflection, creating rippling patterns

            // Base reflection color from fog
            reflectedColor = u_FogColor * 0.8;

            // Use the actual water surface normal from Gerstner waves (same as WaterPlane)
            vec3 waveNormal = calculateWaterSurfaceNormal(waterSurfacePoint.xz);

            // Wave normal affects how light reflects - creates bright/dark areas
            // Steeper wave slopes = darker, flatter areas = brighter
            float waveBrightness = dot(waveNormal, vec3(0.0, 1.0, 0.0));
            waveBrightness = waveBrightness * 0.5 + 0.5; // Remap to 0-1

            // Specular-like highlights where waves catch light from above
            vec3 lightDir = normalize(vec3(0.3, 1.0, 0.2)); // Approximate sun direction
            float waveHighlight = pow(max(0.0, dot(waveNormal, lightDir)), 16.0);

            // Apply wave shading to reflection color
            reflectedColor *= (0.6 + waveBrightness * 0.6); // Normal-based shading
            reflectedColor += vec3(0.15, 0.2, 0.25) * waveHighlight; // Bright highlights

            // Depth-based darkening
            float depthDarkening = exp(-cameraDepth * 0.08);
            reflectedColor *= mix(0.2, 1.0, depthDarkening);

            // Apply reflection tint
            reflectedColor *= u_SnellReflectionTint + vec3(0.3);

            // Apply absorption to reflected color
            reflectedColor = applyAbsorption(reflectedColor, cameraDepth * 0.3);

            // Blend between reflection (outside window) and above-water view (inside window)
            // windowFactor = 1.0 -> see above water (aboveWaterColor)
            // windowFactor = 0.0 -> total internal reflection (reflectedColor)
            aboveWaterColor = mix(reflectedColor * u_SnellReflectionStrength, aboveWaterColor, windowFactor);

            // Fresnel edge brightening at the window boundary
            // This creates a bright ring at the edge of the window
            float edgeFactor = windowFactor * (1.0 - windowFactor) * 4.0; // Peak at 0.5
            aboveWaterColor += u_SnellReflectionTint * edgeFactor * fresnelFactor * u_SnellFresnelPower * 0.3;
        }

        color = aboveWaterColor;

        // Apply light absorption for the water between camera and surface
        color = applyAbsorption(color, cameraDepth * 0.4);

        // Apply lighter fog only for the underwater portion of the ray
        if (u_FogEnabled > 0) {
            float fogFactor = 1.0 - exp(-u_FogDensity * distToWaterSurface * 0.3);
            fogFactor = clamp(fogFactor, 0.0, 0.6);
            // Less fog inside the Snell's window for clearer view of above-water
            color = mix(color, u_FogColor, fogFactor * (1.0 - windowFactor * 0.5));
        }
    } else {
        // Looking at something underwater - full underwater effects
        color = sceneColor;

        // Apply absorption based on depth below water surface
        float effectiveDepth = min(cameraDepth, pixelDepth);
        color = applyAbsorption(color, effectiveDepth);

        // Add caustics on underwater surfaces
        vec2 surfacePos = worldPos.xz;
        vec3 surfaceNormal = calculateWaterSurfaceNormal(surfacePos);
        vec3 caustics = calculateCaustics(worldPos, pixelDepth, surfaceNormal);
        color += caustics;

        // Apply full volumetric underwater fog (ray-marched)
        color = applyUnderwaterFog(color, dist, cameraDepth, u_CameraPos, viewDir, vTexCoord);
    }

    // =========================================================================
    // GLOBAL UNDERWATER EFFECTS (applied regardless of looking above/below water)
    // =========================================================================

    // Add underwater ambient
    vec3 ambient = u_AmbientColor * u_AmbientIntensity;
    ambient = applyAbsorption(ambient, cameraDepth * 0.5);
    color += ambient;

    // Add god rays (volumetric light shafts from surface)
    vec3 godRays = calculateGodRays(worldPos, viewDir, cameraDepth, vTexCoord);
    godRays = applyAbsorption(godRays, cameraDepth * 0.2);
    color += godRays;

    // Add volumetric floating particles with depth and lighting
    vec3 particles = calculateParticles(worldPos, viewDir, effectiveDist, cameraDepth);
    color += particles;

    // Final tint (reduced when looking above water through Snell's window)
    float tintStrength = pixelAboveWater ? 0.05 * (1.0 - windowFactor) : 0.15;
    color = mix(color, color * u_TintColor * 2.0, tintStrength);

    // =========================================================================
    // WATER TRANSITION EFFECTS (unified FBM distortion)
    // =========================================================================
    if (u_TransitionEnabled > 0 && u_TransitionProgress > 0.0) {
        // direction: 1 = entering water (splash up), 0 = exiting water (drips down)
        float direction = u_TransitionDirection > 0 ? -1.0 : 1.0;
        vec4 transitionEffect = calculateWaterTransition(vTexCoord, u_TransitionProgress, direction);

        // Apply FBM-based screen distortion
        vec2 fbmDistortion = transitionEffect.xy * u_TransitionProgress;
        vec2 distortedUV = clamp(vTexCoord + fbmDistortion, 0.0, 1.0);
        vec3 distortedColor = texture(u_SourceTexture, distortedUV).rgb;

        // Blend based on distortion magnitude
        float blendFactor = clamp(length(fbmDistortion) * 15.0, 0.0, 1.0);
        color = mix(color, distortedColor, blendFactor);

        // Subtle blue tint for wet lens feel
        float tintFactor = transitionEffect.z;
        color = mix(color, color * vec3(0.92, 0.96, 1.08), tintFactor);

        // Slight brightness variation (refraction effect)
        color *= 1.0 + (transitionEffect.w * 0.1 - 0.05);
    }

    fragColor = vec4(color, 1.0);
}
