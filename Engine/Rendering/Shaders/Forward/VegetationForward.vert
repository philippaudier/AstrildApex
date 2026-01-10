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

// Animation time (set by renderer each frame)
uniform float u_Time;

// Global wind/weather parameters (set by renderer from WeatherComponent)
uniform float u_WindStrength;
uniform vec2 u_WindDirection;
uniform float u_WindSpeed;
uniform float u_WindGustiness;
uniform float u_BranchAmplitude;
uniform float u_BranchSpeed;
uniform float u_BranchTurbulence;
uniform float u_TrunkStiffness;
uniform float u_TrunkBendAmount;
uniform float u_LeafFlutter;
uniform float u_LeafFlutterSpeed;
uniform float u_RainIntensity;
uniform float u_SnowAccumulation;

// === GLOBAL/LOCAL/BLEND SYSTEM ===
// 0 = Use Global (from WeatherComponent via UBO)
// 1 = Use Local (material-specific parameters)
// 2 = Blend between Local and Global
uniform int u_WindMode = 0;
uniform float u_WindBlendFactor = 1.0; // 0 = local, 1 = global (only used when u_WindMode = 2)

// === LOCAL WIND PARAMETERS (material overrides) ===
// These are only used when u_WindMode = 1 (Local) or 2 (Blend)
uniform float u_WindStrength_Local = 0.5;
uniform vec2 u_WindDirection_Local = vec2(1.0, 0.0);
uniform float u_WindSpeed_Local = 1.0;
uniform float u_WindGustiness_Local = 0.0;
uniform float u_BranchAmplitude_Local = 2.5;
uniform float u_BranchSpeed_Local = 4.0;
uniform float u_BranchTurbulence_Local = 0.8;
uniform float u_TrunkStiffness_Local = 0.85;
uniform float u_TrunkBendAmount_Local = 0.3;
uniform float u_LeafFlutter_Local = 0.6;
uniform float u_LeafFlutterSpeed_Local = 8.0;

// === NORMAL ALIGNMENT CONTROL ===
// Enable preserving terrain normal alignment (prevents wind from breaking grass coverage on slopes)
uniform float u_AlignToNormal = 0.0; // 0 = disabled, 1 = enabled
uniform float u_AlignmentStrength = 1.0; // 0-1, strength of normal preservation

// === HELPER FUNCTIONS: Get effective parameter value ===
float getWindStrength() {
    if (u_WindMode == 0) return u_WindStrength;  // Global
    if (u_WindMode == 1) return u_WindStrength_Local;  // Local
    return mix(u_WindStrength_Local, u_WindStrength, u_WindBlendFactor);  // Blend
}

vec2 getWindDirection() {
    if (u_WindMode == 0) return u_WindDirection;
    if (u_WindMode == 1) return u_WindDirection_Local;
    return normalize(mix(u_WindDirection_Local, u_WindDirection, u_WindBlendFactor));
}

float getWindSpeed() {
    if (u_WindMode == 0) return u_WindSpeed;
    if (u_WindMode == 1) return u_WindSpeed_Local;
    return mix(u_WindSpeed_Local, u_WindSpeed, u_WindBlendFactor);
}

float getWindGustiness() {
    if (u_WindMode == 0) return u_WindGustiness;
    if (u_WindMode == 1) return u_WindGustiness_Local;
    return mix(u_WindGustiness_Local, u_WindGustiness, u_WindBlendFactor);
}

float getBranchAmplitude() {
    if (u_WindMode == 0) return u_BranchAmplitude;
    if (u_WindMode == 1) return u_BranchAmplitude_Local;
    return mix(u_BranchAmplitude_Local, u_BranchAmplitude, u_WindBlendFactor);
}

float getBranchSpeed() {
    if (u_WindMode == 0) return u_BranchSpeed;
    if (u_WindMode == 1) return u_BranchSpeed_Local;
    return mix(u_BranchSpeed_Local, u_BranchSpeed, u_WindBlendFactor);
}

float getBranchTurbulence() {
    if (u_WindMode == 0) return u_BranchTurbulence;
    if (u_WindMode == 1) return u_BranchTurbulence_Local;
    return mix(u_BranchTurbulence_Local, u_BranchTurbulence, u_WindBlendFactor);
}

float getTrunkStiffness() {
    if (u_WindMode == 0) return u_TrunkStiffness;
    if (u_WindMode == 1) return u_TrunkStiffness_Local;
    return mix(u_TrunkStiffness_Local, u_TrunkStiffness, u_WindBlendFactor);
}

float getTrunkBendAmount() {
    if (u_WindMode == 0) return u_TrunkBendAmount;
    if (u_WindMode == 1) return u_TrunkBendAmount_Local;
    return mix(u_TrunkBendAmount_Local, u_TrunkBendAmount, u_WindBlendFactor);
}

float getLeafFlutter() {
    if (u_WindMode == 0) return u_LeafFlutter;
    if (u_WindMode == 1) return u_LeafFlutter_Local;
    return mix(u_LeafFlutter_Local, u_LeafFlutter, u_WindBlendFactor);
}

float getLeafFlutterSpeed() {
    if (u_WindMode == 0) return u_LeafFlutterSpeed;
    if (u_WindMode == 1) return u_LeafFlutterSpeed_Local;
    return mix(u_LeafFlutterSpeed_Local, u_LeafFlutterSpeed, u_WindBlendFactor);
}

// Weather and snow always use Global (from WeatherComponent)
float getRainIntensity() { return u_RainIntensity; }
float getSnowAccumulation() { return u_SnowAccumulation; }
float getSnowIntensity() { return uSnowIntensity; }
float getSnowDisplacement() { return uSnowDisplacement; }
float getSnowSlopeMin() { return uSnowSlopeMin; }
float getSnowSlopeMax() { return uSnowSlopeMax; }

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

    // Get effective wind/weather parameters (Global/Local/Blend)
    float windStrength = getWindStrength();
    vec2 windDirection = getWindDirection();
    float windSpeed = getWindSpeed();
    float windGustiness = getWindGustiness();
    float branchAmplitude = getBranchAmplitude();
    float branchSpeed = getBranchSpeed();
    float branchTurbulence = getBranchTurbulence();
    float trunkStiffness = getTrunkStiffness();
    float trunkBendAmount = getTrunkBendAmount();
    float leafFlutter = getLeafFlutter();
    float leafFlutterSpeed = getLeafFlutterSpeed();
    float rainIntensity = getRainIntensity();
    float snowAccumulation = getSnowAccumulation();

    // === WIND FACTOR CALCULATION ===
    // Height-based influence: quadratic curve for more realistic trunk behavior
    // 0 at base (rigid), gradual increase, rapid at top
    float heightNormalized = clamp(aPos.y / 2.0, 0.0, 1.0);
    float trunkFactor = pow(heightNormalized, 2.0 + trunkStiffness * 2.0); // More rigid at base
    float branchFactor = pow(heightNormalized, 1.5); // Branches more flexible
    vWindFactor = heightNormalized;

    // Per-instance random variation (based on world position)
    float instancePhaseOffset = hash(entityWorldPos.xz);

    // === PRIMARY WIND MOTION (Trunk Sway) ===
    vec3 windOffset = vec3(0.0);

    if (windStrength > 0.0)
    {
        vec3 windDir = normalize(vec3(windDirection.x, 0.0, windDirection.y));
        vec3 perpDir = vec3(-windDir.z, 0.0, windDir.x); // Perpendicular for sway

        // === 1. PRIMARY TRUNK BEND (Low frequency, large scale) ===
        float trunkPhase = u_Time * windSpeed * 0.8 + instancePhaseOffset * 6.28;
        float trunkWave = sin(trunkPhase);

        // Trunk bends in wind direction with smooth wave
        vec3 trunkBend = windDir * trunkWave * windStrength * trunkFactor * trunkBendAmount * 1.5;

        // Subtle perpendicular sway (tree doesn't just bend one way)
        trunkBend += perpDir * sin(trunkPhase * 0.7) * windStrength * trunkFactor * 0.3;

        windOffset += trunkBend;

        // === 2. WIND GUSTS (Medium frequency chaos) ===
        if (windGustiness > 0.0)
        {
            float gustPhase = u_Time * windSpeed * 2.5 + instancePhaseOffset * 3.14;
            float gustNoise = sin(gustPhase) * cos(gustPhase * 1.7); // Chaotic pattern

            vec3 gustOffset = windDir * gustNoise * windGustiness * windStrength * branchFactor * 0.8;
            windOffset += gustOffset;
        }

        // === 3. BRANCH OSCILLATION (High frequency, small scale) ===
        // Use much higher frequency for individual branch motion
        float branchPhase = u_Time * windSpeed * branchSpeed + instancePhaseOffset * 12.0;

        // Per-vertex variation for individual branch movement (HIGH frequency)
        float vertexPhase = dot(aPos.xyz, vec3(5.0, 3.0, 4.0)); // Increased from 0.3,0.1,0.4
        float branchNoise = sin(branchPhase + vertexPhase * 2.0) * cos(branchPhase * 1.3 + vertexPhase * 1.5);

        // Add high-frequency turbulence per vertex
        branchNoise += sin(branchPhase * 3.0 + vertexPhase * 8.0) * branchTurbulence * 0.5;
        branchNoise += cos(branchPhase * 2.5 + vertexPhase * 6.0) * branchTurbulence * 0.3;

        // Branches sway in multiple directions with per-vertex variation
        vec3 branchSway = windDir * branchNoise * windStrength * branchFactor * branchAmplitude * 0.6;
        branchSway += perpDir * sin(branchPhase * 1.3 + vertexPhase) * windStrength * branchFactor * branchAmplitude * 0.4;

        // Add another layer of detail (tiny per-vertex oscillation)
        branchSway += vec3(
            sin(branchPhase * 4.0 + vertexPhase * 10.0),
            cos(branchPhase * 3.5 + vertexPhase * 8.0),
            sin(branchPhase * 4.5 + vertexPhase * 12.0)
        ) * windStrength * branchFactor * branchAmplitude * 0.2;

        windOffset += branchSway;

        // === 4. LEAF FLUTTER (Very high frequency, tiny motion) ===
        if (leafFlutter > 0.0)
        {
            // Much higher frequency for individual leaf flutter
            float leafPhase = u_Time * windSpeed * leafFlutterSpeed + instancePhaseOffset * 20.0 + vertexPhase * 30.0;
            float leafFlutterNoise = sin(leafPhase * 2.0) * cos(leafPhase * 3.3);

            // Leaves flutter on all axes with high-frequency variation
            vec3 leafMotion = vec3(
                sin(leafPhase * 2.1 + vertexPhase * 5.0) * 0.15,
                cos(leafPhase * 2.3 + vertexPhase * 4.0) * 0.1,
                sin(leafPhase * 1.9 + vertexPhase * 6.0) * 0.15
            );

            windOffset += leafMotion * leafFlutterNoise * leafFlutter * windStrength * branchFactor * 0.3;
        }

        // === 5. EDGE TURBULENCE (Detail with high-frequency per-vertex variation)
        float edgeFactor = pow(heightNormalized, 3.0);
        float edgePhase = u_Time * windSpeed * 4.0 + dot(aPos.xyz, vec3(3.0, 2.0, 3.0)) * 8.0; // Increased frequency
        vec3 edgeTurbulence = vec3(
            sin(edgePhase * 2.4 + vertexPhase * 7.0),
            cos(edgePhase * 2.1 + vertexPhase * 6.0),
            sin(edgePhase * 2.7 + vertexPhase * 8.0)
        ) * edgeFactor * windStrength * 0.15 * branchTurbulence;

        windOffset += edgeTurbulence;
    }

    // === WEATHER EFFECTS ===
    vec3 weatherOffset = windOffset;

    // Rain makes vegetation droop and sway heavily
    if (rainIntensity > 0.0)
    {
        // Drooping from water weight
        weatherOffset.y -= rainIntensity * vWindFactor * 0.15;

        // Rain adds extra chaotic motion
        float rainPhase = u_Time * 3.0 + instancePhaseOffset;
        weatherOffset += vec3(sin(rainPhase) * 0.1, 0.0, cos(rainPhase) * 0.1) * rainIntensity * branchFactor;
    }

    // Snow weighs down branches
    if (snowAccumulation > 0.0)
    {
        weatherOffset.y -= snowAccumulation * vWindFactor * 0.02; // Much lighter effect
    }

    // === NORMAL ALIGNMENT PRESERVATION ===
    // When enabled, project wind offset into tangent plane to preserve terrain normal alignment
    // This keeps grass "glued" to slopes instead of breaking away
    if (u_AlignToNormal > 0.5)
    {
        // Extract instance "up" vector from model matrix (Y column)
        vec3 instanceUp = normalize(vec3(aInstanceModel[0][1], aInstanceModel[1][1], aInstanceModel[2][1]));
        
        // Project wind offset onto tangent plane (perpendicular to instance up)
        // This removes any component that would pull away from the surface
        float upComponent = dot(weatherOffset, instanceUp);
        vec3 tangentOffset = weatherOffset - instanceUp * upComponent * u_AlignmentStrength;
        
        weatherOffset = tangentOffset;
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
    // Snow parameters always come from Global (WeatherComponent)
    float snowPlacement = calculateSnowPlacement(worldNormal, getSnowSlopeMin(), getSnowSlopeMax());
    float snowAmount = snowAccumulation * snowPlacement;

    // Vertical displacement based on snow accumulation
    // Uses exponential curve for natural build-up (more snow = more height)
    float displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * getSnowDisplacement();

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
