#version 420 core

#include "../Includes/Common.glsl"

// Vertex attributes (standard per-vertex data)
layout(location=0) in vec3 aPos;
layout(location=1) in vec3 aNormal;
layout(location=2) in vec2 aUV;

// Standard uniforms (like ForwardBase)
uniform mat4 u_Model;
uniform mat3 u_NormalMat;
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

// === ADVANCED WIND PARAMETERS ===
// Primary wind wave
uniform float u_WindStrength;      // 0.0 to 1.0 - Overall wind intensity
uniform vec2 u_WindDirection;      // Normalized wind direction (XZ plane)
uniform float u_WindSpeed;         // Primary wave speed (Hz)
uniform float u_WindGustiness;     // Turbulence/chaos factor (0.0 = smooth, 1.0 = gusty)

// Branch motion (high-frequency detail)
uniform float u_BranchAmplitude;   // Branch sway amplitude multiplier (default: 1.0)
uniform float u_BranchSpeed;       // Branch oscillation speed (default: 3.0)
uniform float u_BranchTurbulence;  // Branch detail/noise intensity (default: 0.5)

// Trunk motion (low-frequency)
uniform float u_TrunkStiffness;    // Trunk rigidity (0.0 = flexible, 1.0 = rigid, default: 0.7)
uniform float u_TrunkBendAmount;   // How much trunk bends at top (default: 0.5)

// Leaf flutter (very high-frequency)
uniform float u_LeafFlutter;       // Leaf flutter intensity (default: 0.3)
uniform float u_LeafFlutterSpeed;  // Leaf flutter speed (default: 5.0)

uniform float u_Time;              // Game time for animation

// Weather parameters
uniform float u_RainIntensity;     // 0.0 = no rain, 1.0 = heavy rain
uniform float u_SnowCoverage;      // 0.0 = no snow, 1.0 = full snow coverage

// Snow displacement parameters
uniform float u_SnowAccumulation;  // Accumulated snow (can exceed 1.0)
uniform float u_SnowDisplacement;  // Max displacement height
uniform float u_SnowSlopeMin;      // Min slope for snow placement (degrees)
uniform float u_SnowSlopeMax;      // Max slope for snow placement (degrees)

// LOD uniforms removed: vegetation shader no longer performs distance-based LOD/culling

// Per-instance model matrix (4 vec4 attributes at locations 3..6)
layout(location=3) in mat4 aInstanceModel;

// Outputs
out vec3 vWorldPos;
out vec3 vNormal;
out vec2 vUV;
out float vWindFactor;  // How much this vertex is affected by wind (based on height)
out vec3 vInstancePos;  // Instance position for rain/snow effects
out float vDistanceToCamera;  // Distance from camera for dithered fade

// Pseudo-random noise function for per-instance variation
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Calculate snow placement based on surface angle (same as ForwardBase)
float calculateSnowPlacement(vec3 normal, float slopeMinDeg, float slopeMaxDeg)
{
    vec3 up = vec3(0, 1, 0);
    float dotProduct = dot(normalize(normal), up);
    float angleRad = acos(clamp(dotProduct, -1.0, 1.0));
    float angleDeg = angleRad * 57.29577951; // radians to degrees

    float fadeWidth = 5.0;
    float fadeIn = smoothstep(slopeMinDeg, slopeMinDeg + fadeWidth, angleDeg);
    float fadeOut = 1.0 - smoothstep(slopeMaxDeg - fadeWidth, slopeMaxDeg, angleDeg);

    return clamp(fadeIn * fadeOut, 0.0, 1.0);
}

void main()
{
    // Get instance/world position from per-instance model matrix
    // Use instance model when available (vegetation uses instancing).
    vec3 entityWorldPos = vec3(aInstanceModel[3][0], aInstanceModel[3][1], aInstanceModel[3][2]);
    vInstancePos = entityWorldPos;

    // === WIND FACTOR CALCULATION ===
    // Height-based influence: quadratic curve for more realistic trunk behavior
    // 0 at base (rigid), gradual increase, rapid at top
    float heightNormalized = clamp(aPos.y / 2.0, 0.0, 1.0);
    float trunkFactor = pow(heightNormalized, 2.0 + u_TrunkStiffness * 2.0); // More rigid at base
    float branchFactor = pow(heightNormalized, 1.5); // Branches more flexible
    vWindFactor = heightNormalized;

    // Per-instance random variation (based on world position)
    float instancePhaseOffset = hash(entityWorldPos.xz);
    
    // === PRIMARY WIND MOTION (Trunk Sway) ===
    vec3 windOffset = vec3(0.0);
    
    if (u_WindStrength > 0.0)
    {
        vec3 windDir = normalize(vec3(u_WindDirection.x, 0.0, u_WindDirection.y));
        vec3 perpDir = vec3(-windDir.z, 0.0, windDir.x); // Perpendicular for sway
        
        // === 1. PRIMARY TRUNK BEND (Low frequency, large scale) ===
        float trunkPhase = u_Time * u_WindSpeed * 0.8 + instancePhaseOffset * 6.28;
        float trunkWave = sin(trunkPhase);
        
        // Trunk bends in wind direction with smooth wave
        vec3 trunkBend = windDir * trunkWave * u_WindStrength * trunkFactor * u_TrunkBendAmount * 1.5;
        
        // Subtle perpendicular sway (tree doesn't just bend one way)
        trunkBend += perpDir * sin(trunkPhase * 0.7) * u_WindStrength * trunkFactor * 0.3;
        
        windOffset += trunkBend;
        
        // === 2. WIND GUSTS (Medium frequency chaos) ===
        if (u_WindGustiness > 0.0)
        {
            float gustPhase = u_Time * u_WindSpeed * 2.5 + instancePhaseOffset * 3.14;
            float gustNoise = sin(gustPhase) * cos(gustPhase * 1.7); // Chaotic pattern
            
            vec3 gustOffset = windDir * gustNoise * u_WindGustiness * u_WindStrength * branchFactor * 0.8;
            windOffset += gustOffset;
        }
        
        // === 3. BRANCH OSCILLATION (High frequency, small scale) ===
        // Use much higher frequency for individual branch motion
        float branchPhase = u_Time * u_WindSpeed * u_BranchSpeed + instancePhaseOffset * 12.0;
        
        // Per-vertex variation for individual branch movement (HIGH frequency)
        float vertexPhase = dot(aPos.xyz, vec3(5.0, 3.0, 4.0)); // Increased from 0.3,0.1,0.4
        float branchNoise = sin(branchPhase + vertexPhase * 2.0) * cos(branchPhase * 1.3 + vertexPhase * 1.5);
        
        // Add high-frequency turbulence per vertex
        branchNoise += sin(branchPhase * 3.0 + vertexPhase * 8.0) * u_BranchTurbulence * 0.5;
        branchNoise += cos(branchPhase * 2.5 + vertexPhase * 6.0) * u_BranchTurbulence * 0.3;
        
        // Branches sway in multiple directions with per-vertex variation
        vec3 branchSway = windDir * branchNoise * u_WindStrength * branchFactor * u_BranchAmplitude * 0.6;
        branchSway += perpDir * sin(branchPhase * 1.3 + vertexPhase) * u_WindStrength * branchFactor * u_BranchAmplitude * 0.4;
        
        // Add another layer of detail (tiny per-vertex oscillation)
        branchSway += vec3(
            sin(branchPhase * 4.0 + vertexPhase * 10.0),
            cos(branchPhase * 3.5 + vertexPhase * 8.0),
            sin(branchPhase * 4.5 + vertexPhase * 12.0)
        ) * u_WindStrength * branchFactor * u_BranchAmplitude * 0.2;
        
        windOffset += branchSway;
        
        // === 4. LEAF FLUTTER (Very high frequency, tiny motion) ===
        if (u_LeafFlutter > 0.0)
        {
            // Much higher frequency for individual leaf flutter
            float leafPhase = u_Time * u_WindSpeed * u_LeafFlutterSpeed + instancePhaseOffset * 20.0 + vertexPhase * 30.0;
            float leafFlutterNoise = sin(leafPhase * 2.0) * cos(leafPhase * 3.3);
            
            // Leaves flutter on all axes with high-frequency variation
            vec3 leafMotion = vec3(
                sin(leafPhase * 2.1 + vertexPhase * 5.0) * 0.15,
                cos(leafPhase * 2.3 + vertexPhase * 4.0) * 0.1,
                sin(leafPhase * 1.9 + vertexPhase * 6.0) * 0.15
            );
            
            windOffset += leafMotion * leafFlutterNoise * u_LeafFlutter * u_WindStrength * branchFactor * 0.3;
        }
        
        // === 5. EDGE TURBULENCE (Detail with high-frequency per-vertex variation)
        float edgeFactor = pow(heightNormalized, 3.0);
        float edgePhase = u_Time * u_WindSpeed * 4.0 + dot(aPos.xyz, vec3(3.0, 2.0, 3.0)) * 8.0; // Increased frequency
        vec3 edgeTurbulence = vec3(
            sin(edgePhase * 2.4 + vertexPhase * 7.0),
            cos(edgePhase * 2.1 + vertexPhase * 6.0),
            sin(edgePhase * 2.7 + vertexPhase * 8.0)
        ) * edgeFactor * u_WindStrength * 0.15 * u_BranchTurbulence;
        
        windOffset += edgeTurbulence;
    }

    // === WEATHER EFFECTS ===
    vec3 weatherOffset = windOffset;
    
    // Rain makes vegetation droop and sway heavily
    if (u_RainIntensity > 0.0)
    {
        // Drooping from water weight
        weatherOffset.y -= u_RainIntensity * vWindFactor * 0.15;
        
        // Rain adds extra chaotic motion
        float rainPhase = u_Time * 3.0 + instancePhaseOffset;
        weatherOffset += vec3(sin(rainPhase) * 0.1, 0.0, cos(rainPhase) * 0.1) * u_RainIntensity * branchFactor;
    }
    
    // Snow weighs down branches
    if (u_SnowCoverage > 0.0)
    {
        weatherOffset.y -= u_SnowCoverage * vWindFactor * 0.2;
    }

    // Transform vertex position with per-instance model matrix and weather effects
    vec4 localPos = vec4(aPos + weatherOffset, 1.0);
    vec4 worldPos = aInstanceModel * localPos;

    // Transform normal by model (treat as direction - w=0). Using aInstanceModel
    // is sufficient here (models typically use uniform scale); for non-uniform
    // scale this is an approximation but acceptable for vegetation.
    vec3 worldNormal = normalize((aInstanceModel * vec4(aNormal, 0.0)).xyz);

    // === SNOW DISPLACEMENT ===
    // Calculate snow displacement (AFTER wind/weather effects)
    float snowPlacement = calculateSnowPlacement(worldNormal, u_SnowSlopeMin, u_SnowSlopeMax);
    float snowAmount = u_SnowAccumulation * snowPlacement;

    // Vertical displacement based on snow accumulation
    // Uses exponential curve for natural build-up (more snow = more height)
    float displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * u_SnowDisplacement;

    // Displace vertex upward along world Y-axis
    worldPos.y += displacementAmount;

    // Smooth normals for rounded snow edges
    // Blend normal towards up vector based on snow amount
    float normalSmoothFactor = clamp(snowAmount * 0.3, 0.0, 0.7);
    vec3 smoothedNormal = normalize(mix(worldNormal, vec3(0, 1, 0), normalSmoothFactor));

    vWorldPos = worldPos.xyz;
    vNormal = smoothedNormal;

    // UV with tiling and offset
    vUV = aUV * u_TextureTiling + u_TextureOffset;

    // Calculate distance to camera for dithered fade
    vDistanceToCamera = length(worldPos.xyz - uCameraPos);

    // Final vertex position using Common.glsl's uViewProj
    gl_Position = uViewProj * worldPos;

    // Clipping plane support (for planar reflections)
    if (uClipPlaneEnabled > 0.5) {
        gl_ClipDistance[0] = dot(worldPos, uClipPlane);
    } else {
        gl_ClipDistance[0] = 1.0; // Always pass if clipping disabled
    }
}
