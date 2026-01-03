#version 330 core

layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;      // Used for wind animation
layout(location = 2) in vec2 aTexCoord;    // Used for alpha clipping

// Instance model matrix (one per instance, locations 3-6)
layout(location = 3) in vec4 aInstanceMatrix_Col0;
layout(location = 4) in vec4 aInstanceMatrix_Col1;
layout(location = 5) in vec4 aInstanceMatrix_Col2;
layout(location = 6) in vec4 aInstanceMatrix_Col3;

uniform mat4 u_LightSpaceMatrix;  // Combined light view-projection matrix
uniform vec2 u_TextureTiling;     // Texture tiling (for alpha test)
uniform vec2 u_TextureOffset;     // Texture offset (for alpha test)

// Wind animation uniforms (must match VegetationForward.vert EXACTLY)
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
uniform float u_Time;

// Weather uniforms (for drooping/displacement)
uniform float u_RainIntensity;
uniform float u_SnowCoverage;
uniform float u_SnowAccumulation;
uniform float u_SnowDisplacement;
uniform float u_SnowSlopeMin;
uniform float u_SnowSlopeMax;

out vec2 vTexCoord;

// Pseudo-random noise function (same as VegetationForward.vert)
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// Snow placement calculation (same as VegetationForward.vert)
float calculateSnowPlacement(vec3 normal, float slopeMinDeg, float slopeMaxDeg)
{
    vec3 up = vec3(0, 1, 0);
    float dotProduct = dot(normalize(normal), up);
    float angleRad = acos(clamp(dotProduct, -1.0, 1.0));
    float angleDeg = angleRad * 57.29577951;

    float fadeWidth = 5.0;
    float fadeIn = smoothstep(slopeMinDeg, slopeMinDeg + fadeWidth, angleDeg);
    float fadeOut = 1.0 - smoothstep(slopeMaxDeg - fadeWidth, slopeMaxDeg, angleDeg);

    return clamp(fadeIn * fadeOut, 0.0, 1.0);
}

void main()
{
    // Reconstruct model matrix from instance data
    mat4 instanceModel = mat4(
        aInstanceMatrix_Col0,
        aInstanceMatrix_Col1,
        aInstanceMatrix_Col2,
        aInstanceMatrix_Col3
    );

    // Get instance position
    vec3 entityWorldPos = vec3(instanceModel[3][0], instanceModel[3][1], instanceModel[3][2]);

    // === WIND ANIMATION (COPY FROM VegetationForward.vert) ===
    float heightNormalized = clamp(aPos.y / 2.0, 0.0, 1.0);
    float trunkFactor = pow(heightNormalized, 2.0 + u_TrunkStiffness * 2.0);
    float branchFactor = pow(heightNormalized, 1.5);
    float instancePhaseOffset = hash(entityWorldPos.xz);

    vec3 windOffset = vec3(0.0);

    if (u_WindStrength > 0.0)
    {
        vec3 windDir = normalize(vec3(u_WindDirection.x, 0.0, u_WindDirection.y));
        vec3 perpDir = vec3(-windDir.z, 0.0, windDir.x);

        // Primary trunk bend
        float trunkPhase = u_Time * u_WindSpeed * 0.8 + instancePhaseOffset * 6.28;
        float trunkWave = sin(trunkPhase);
        vec3 trunkBend = windDir * trunkWave * u_WindStrength * trunkFactor * u_TrunkBendAmount * 1.5;
        trunkBend += perpDir * sin(trunkPhase * 0.7) * u_WindStrength * trunkFactor * 0.3;
        windOffset += trunkBend;

        // Wind gusts
        if (u_WindGustiness > 0.0)
        {
            float gustPhase = u_Time * u_WindSpeed * 2.5 + instancePhaseOffset * 3.14;
            float gustNoise = sin(gustPhase) * cos(gustPhase * 1.7);
            vec3 gustOffset = windDir * gustNoise * u_WindGustiness * u_WindStrength * branchFactor * 0.8;
            windOffset += gustOffset;
        }

        // Branch oscillation
        float branchPhase = u_Time * u_WindSpeed * u_BranchSpeed + instancePhaseOffset * 12.0;
        float vertexPhase = dot(aPos.xyz, vec3(5.0, 3.0, 4.0));
        float branchNoise = sin(branchPhase + vertexPhase * 2.0) * cos(branchPhase * 1.3 + vertexPhase * 1.5);
        branchNoise += sin(branchPhase * 3.0 + vertexPhase * 8.0) * u_BranchTurbulence * 0.5;
        branchNoise += cos(branchPhase * 2.5 + vertexPhase * 6.0) * u_BranchTurbulence * 0.3;

        vec3 branchSway = windDir * branchNoise * u_WindStrength * branchFactor * u_BranchAmplitude * 0.6;
        branchSway += perpDir * sin(branchPhase * 1.3 + vertexPhase) * u_WindStrength * branchFactor * u_BranchAmplitude * 0.4;
        branchSway += vec3(
            sin(branchPhase * 4.0 + vertexPhase * 10.0),
            cos(branchPhase * 3.5 + vertexPhase * 8.0),
            sin(branchPhase * 4.5 + vertexPhase * 12.0)
        ) * u_WindStrength * branchFactor * u_BranchAmplitude * 0.2;
        windOffset += branchSway;

        // Leaf flutter
        if (u_LeafFlutter > 0.0)
        {
            float leafPhase = u_Time * u_WindSpeed * u_LeafFlutterSpeed + instancePhaseOffset * 20.0 + vertexPhase * 30.0;
            float leafFlutterNoise = sin(leafPhase * 2.0) * cos(leafPhase * 3.3);
            vec3 leafMotion = vec3(
                sin(leafPhase * 2.1 + vertexPhase * 5.0) * 0.15,
                cos(leafPhase * 2.3 + vertexPhase * 4.0) * 0.1,
                sin(leafPhase * 1.9 + vertexPhase * 6.0) * 0.15
            );
            windOffset += leafMotion * leafFlutterNoise * u_LeafFlutter * u_WindStrength * branchFactor * 0.3;
        }

        // Edge turbulence
        float edgeFactor = pow(heightNormalized, 3.0);
        float edgePhase = u_Time * u_WindSpeed * 4.0 + dot(aPos.xyz, vec3(3.0, 2.0, 3.0)) * 8.0;
        vec3 edgeTurbulence = vec3(
            sin(edgePhase * 2.4 + vertexPhase * 7.0),
            cos(edgePhase * 2.1 + vertexPhase * 6.0),
            sin(edgePhase * 2.7 + vertexPhase * 8.0)
        ) * edgeFactor * u_WindStrength * 0.15 * u_BranchTurbulence;
        windOffset += edgeTurbulence;
    }

    // Weather effects
    vec3 weatherOffset = windOffset;

    if (u_RainIntensity > 0.0)
    {
        float vWindFactor = heightNormalized;
        weatherOffset.y -= u_RainIntensity * vWindFactor * 0.15;
        float rainPhase = u_Time * 3.0 + instancePhaseOffset;
        weatherOffset += vec3(sin(rainPhase) * 0.1, 0.0, cos(rainPhase) * 0.1) * u_RainIntensity * branchFactor;
    }

    if (u_SnowCoverage > 0.0)
    {
        weatherOffset.y -= u_SnowCoverage * heightNormalized * 0.2;
    }

    // Apply wind/weather offset
    vec4 localPos = vec4(aPos + weatherOffset, 1.0);
    vec4 worldPos = instanceModel * localPos;

    // Snow displacement (same as VegetationForward.vert)
    vec3 worldNormal = normalize((instanceModel * vec4(aNormal, 0.0)).xyz);
    float snowPlacement = calculateSnowPlacement(worldNormal, u_SnowSlopeMin, u_SnowSlopeMax);
    float snowAmount = u_SnowAccumulation * snowPlacement;
    float displacementAmount = (1.0 - exp(-snowAmount * 0.8)) * u_SnowDisplacement;
    worldPos.y += displacementAmount;

    // Pass texture coordinates
    vTexCoord = aTexCoord * u_TextureTiling + u_TextureOffset;

    // Transform to light space
    gl_Position = u_LightSpaceMatrix * worldPos;
}
