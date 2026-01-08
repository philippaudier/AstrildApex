#version 330 core

in vec2 vTexCoord;
out vec4 FragColor;

uniform sampler2D u_SourceTexture;    // HDR scene color
uniform sampler2D u_DepthTexture;     // Depth buffer

// Camera parameters for depth reconstruction
uniform mat4 u_InvProjection;
uniform mat4 u_InvView;
uniform vec3 u_CameraPos;

// Fog parameters
uniform vec3 u_FogColor;
uniform float u_Density;
uniform float u_DepthStart;
uniform float u_DepthEnd;
uniform bool u_UseExponential;

// Height-based fog
uniform bool u_UseHeightFog;
uniform float u_HeightFalloff;
uniform float u_BaseHeight;
uniform float u_MaxHeight;

// Scattering & atmosphere
uniform float u_ScatteringIntensity;
uniform float u_ExtinctionFactor;
uniform vec3 u_SunScatteringColor;
uniform bool u_UseSunScattering;
uniform vec3 u_SunDirection; // Directional light direction

// Noise (optional)
uniform bool u_UseNoise;
uniform float u_NoiseScale;
uniform float u_NoiseSpeed;
uniform float u_NoiseStrength;
uniform float u_Time;

// Screen resolution for depth buffer analysis
uniform vec2 u_ScreenSize;

// Simple 3D noise function
float hash(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    return mix(
        mix(mix(hash(i + vec3(0, 0, 0)), hash(i + vec3(1, 0, 0)), f.x),
            mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
        mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x),
            mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y),
        f.z
    );
}

// Reconstruct world position from depth
vec3 worldPositionFromDepth(float depth, vec2 texCoord) {
    // Convert depth to NDC
    float z = depth * 2.0 - 1.0;

    // Reconstruct clip space position
    vec4 clipSpacePosition = vec4(texCoord * 2.0 - 1.0, z, 1.0);

    // Transform to view space
    vec4 viewSpacePosition = u_InvProjection * clipSpacePosition;
    
    // Safety check to avoid division by zero
    if (abs(viewSpacePosition.w) < 0.0001) {
        return u_CameraPos; // Fallback to camera position
    }
    
    viewSpacePosition /= viewSpacePosition.w;

    // Transform to world space
    vec4 worldSpacePosition = u_InvView * viewSpacePosition;

    return worldSpacePosition.xyz;
}

// Analyze local depth buffer to detect valleys and depressions
// Returns a factor [0,1] where 1 = deep valley (more fog), 0 = peak (less fog)
float calculateValleyFactor(vec2 texCoord, float centerDepth) {
    if (centerDepth >= 0.9999) return 0.0; // Skybox
    
    // Safety check for screen size
    if (u_ScreenSize.x < 1.0 || u_ScreenSize.y < 1.0) return 0.0;
    
    vec2 texelSize = 1.0 / u_ScreenSize;
    float valleyAccumulation = 0.0;
    float sampleCount = 0.0;
    
    // Sample surrounding pixels in a 5x5 pattern
    const int radius = 2;
    for (int x = -radius; x <= radius; x++) {
        for (int y = -radius; y <= radius; y++) {
            if (x == 0 && y == 0) continue; // Skip center
            
            vec2 offset = vec2(float(x), float(y)) * texelSize;
            vec2 sampleCoord = texCoord + offset;
            
            // Clamp to valid texture coordinates
            if (sampleCoord.x < 0.0 || sampleCoord.x > 1.0 || 
                sampleCoord.y < 0.0 || sampleCoord.y > 1.0) continue;
            
            float sampleDepth = texture(u_DepthTexture, sampleCoord).r;
            if (sampleDepth >= 0.9999) continue; // Skip skybox
            
            // If surrounding pixels are closer to camera (smaller depth),
            // this pixel is in a valley
            float depthDiff = sampleDepth - centerDepth;
            if (depthDiff < 0.0) {
                // This pixel is further = in a depression
                valleyAccumulation += abs(depthDiff);
                sampleCount += 1.0;
            }
        }
    }
    
    if (sampleCount < 1.0) return 0.0;
    
    // Normalize and amplify the valley detection
    float avgValley = valleyAccumulation / sampleCount;
    return clamp(avgValley * 50.0, 0.0, 1.0); // Amplify by 50x for visibility
}

// Calculate relative height compared to nearby geometry
// Returns negative value if in a depression, positive if on a peak
float calculateRelativeHeight(vec2 texCoord, vec3 worldPos) {
    // Safety check for screen size
    if (u_ScreenSize.x < 1.0 || u_ScreenSize.y < 1.0) return 0.0;
    
    vec2 texelSize = 1.0 / u_ScreenSize;
    float avgHeight = 0.0;
    float sampleCount = 0.0;
    
    // Sample surrounding world positions
    const int radius = 3;
    for (int x = -radius; x <= radius; x += radius) {
        for (int y = -radius; y <= radius; y += radius) {
            if (x == 0 && y == 0) continue;
            
            vec2 offset = vec2(float(x), float(y)) * texelSize * 2.0;
            vec2 sampleCoord = texCoord + offset;
            
            if (sampleCoord.x < 0.0 || sampleCoord.x > 1.0 || 
                sampleCoord.y < 0.0 || sampleCoord.y > 1.0) continue;
            
            float sampleDepth = texture(u_DepthTexture, sampleCoord).r;
            if (sampleDepth >= 0.9999) continue;
            
            vec3 sampleWorldPos = worldPositionFromDepth(sampleDepth, sampleCoord);
            avgHeight += sampleWorldPos.y;
            sampleCount += 1.0;
        }
    }
    
    if (sampleCount < 1.0) return 0.0;
    
    avgHeight /= sampleCount;
    return avgHeight - worldPos.y; // Negative if we're lower than average (valley)
}

void main()
{
    vec3 sceneColor = texture(u_SourceTexture, vTexCoord).rgb;
    float depth = texture(u_DepthTexture, vTexCoord).r;

    // DEBUG MODE: Uncomment to visualize depth buffer
    // FragColor = vec4(vec3(depth), 1.0);
    // return;

    // Skip fog on skybox (depth = 1.0)
    // IMPORTANT: Skybox should NOT have fog applied to it
    if (depth >= 0.9999) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    // Reconstruct world position
    vec3 worldPos = worldPositionFromDepth(depth, vTexCoord);
    
    // Safety check for invalid world positions (NaN/Inf)
    if (any(isnan(worldPos)) || any(isinf(worldPos))) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    // Calculate distance from camera
    float distance = length(worldPos - u_CameraPos);
    
    // Safety check for invalid distances
    if (isnan(distance) || isinf(distance)) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    // === DEPTH-BASED FOG DENSITY ===
    float depthFactor = 0.0;
    if (u_UseExponential) {
        // Exponential fog
        depthFactor = 1.0 - exp(-distance * u_Density);
    } else {
        // Linear fog
        depthFactor = smoothstep(u_DepthStart, u_DepthEnd, distance);
    }
    
    // Clamp to prevent issues
    depthFactor = clamp(depthFactor, 0.0, 1.0);

    // === HEIGHT-BASED FOG DENSITY ===
    float heightFactor = 1.0;
    if (u_UseHeightFog) {
        // Exponential height falloff
        float heightAboveBase = max(0.0, worldPos.y - u_BaseHeight);
        heightFactor = exp(-heightAboveBase * u_HeightFalloff);

        // Clamp at max height
        if (worldPos.y > u_MaxHeight) {
            heightFactor *= exp(-(worldPos.y - u_MaxHeight) * u_HeightFalloff * 2.0);
        }
        
        // === VALLEY DETECTION FOR RELIEF ===
        // Detect if this pixel is in a valley/depression
        float valleyFactor = calculateValleyFactor(vTexCoord, depth);
        
        // Calculate relative height compared to nearby geometry
        float relativeHeight = calculateRelativeHeight(vTexCoord, worldPos);
        
        // If we're in a valley (negative relative height), increase fog density
        float depressionBoost = 1.0;
        if (relativeHeight < 0.0) {
            // More fog in depressions - exponential boost
            depressionBoost = 1.0 + abs(relativeHeight) * 0.5; // Scale by relative depth
            depressionBoost = clamp(depressionBoost, 1.0, 3.0);
        } else {
            // Less fog on peaks
            depressionBoost = max(0.3, 1.0 - relativeHeight * 0.2);
        }
        
        // Apply valley factor - combines depth analysis and height analysis
        heightFactor *= mix(1.0, depressionBoost, valleyFactor);
        
        // Additional boost for very low areas (below base height)
        if (worldPos.y < u_BaseHeight) {
            float belowBase = u_BaseHeight - worldPos.y;
            heightFactor *= 1.0 + belowBase * 0.1; // More fog the deeper we go
        }
    }

    // === COMBINED DENSITY ===
    float combinedDensity = depthFactor * heightFactor;
    
    // Safety clamp - prevent extreme values
    combinedDensity = clamp(combinedDensity, 0.0, 10.0);

    // === NOISE (optional detail) ===
    if (u_UseNoise) {
        vec3 noiseCoord = worldPos * u_NoiseScale + vec3(u_Time * u_NoiseSpeed);
        float noiseValue = noise3D(noiseCoord);
        combinedDensity *= (1.0 - u_NoiseStrength) + noiseValue * u_NoiseStrength;
    }

    // === SUN SCATTERING (optional) ===
    vec3 finalFogColor = u_FogColor;
    if (u_UseSunScattering) {
        vec3 viewDir = normalize(worldPos - u_CameraPos);
        float sunAlignment = max(0.0, dot(viewDir, normalize(-u_SunDirection)));

        // Mie scattering approximation (forward scattering)
        float mieScatter = pow(sunAlignment, 8.0) * u_ScatteringIntensity;

        finalFogColor = mix(u_FogColor, u_SunScatteringColor, mieScatter);
    }

    // === APPLY FOG ===
    // Beer's law for light extinction through fog
    float transmission = exp(-combinedDensity * u_ExtinctionFactor);
    transmission = clamp(transmission, 0.0, 1.0);

    vec3 finalColor = mix(finalFogColor, sceneColor, transmission);
    
    // Final safety check
    if (any(isnan(finalColor)) || any(isinf(finalColor))) {
        FragColor = vec4(sceneColor, 1.0);
        return;
    }

    FragColor = vec4(finalColor, 1.0);
}
