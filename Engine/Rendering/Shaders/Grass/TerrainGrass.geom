#version 420 core

layout(triangles) in;
layout(triangle_strip, max_vertices = 60) out; // Up to 10 blades * 6 vertices per blade

#include "../Includes/Common.glsl"

// Input from vertex shader
in VS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;
} gs_in[];

// Output to fragment shader
out GS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;           // UV along grass blade (x=across, y=height 0-1)
    float heightFactor; // 0 at base, 1 at tip
    vec3 color;        // Per-vertex color
} gs_out;

// Grass parameters
uniform float u_BladeHeight;
uniform float u_BladeHeightVariation;
uniform float u_BladeWidth;
uniform float u_BladeCurvature;
uniform int u_BladesPerVertex;  // Now controls blades per triangle (1-16)
uniform float u_Density;
uniform float u_CoverageNoiseScale;
uniform float u_CoverageThreshold;

// Slope & Height constraints
uniform float u_MinSlopeY;   // cos(maxSlope) - lower Y normal = steeper
uniform float u_MaxSlopeY;   // cos(minSlope) - higher Y normal = flatter
uniform float u_MinHeight;
uniform float u_MaxHeight;

// Colors
uniform vec4 u_ColorTop;
uniform vec4 u_ColorBottom;
uniform float u_ColorVariation;

// Wind
uniform float u_WindStrength;
uniform float u_WindSpeed;
uniform float u_WindTurbulence;
uniform vec2 u_WindDirection;

// LOD
uniform float u_MaxRenderDistance;
uniform float u_FadeRange;
uniform int u_LodEnabled;
uniform float u_LodDistance1;
uniform float u_LodDistance2;
uniform float u_LodDistance3;
uniform int u_MaxBladesPerTriangle; // Configurable max blades per triangle (10-30)

// Density texture (optional)
uniform sampler2D u_DensityMap;
uniform int u_HasDensityMap;
uniform float u_DensityMapScale;

// Pseudo-random hash functions
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

vec2 hash2(vec2 p) {
    return vec2(
        fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453),
        fract(sin(dot(p, vec2(269.5, 183.3))) * 43758.5453)
    );
}

// 2D noise function for coverage
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

// FBM noise for more natural coverage patterns
float fbm(vec2 p, int octaves) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    
    for (int i = 0; i < octaves; i++) {
        value += amplitude * noise2D(p * frequency);
        amplitude *= 0.5;
        frequency *= 2.0;
    }
    return value;
}

// Generate barycentric coordinates for uniform point distribution within triangle
vec3 randomBarycentric(vec2 seed) {
    vec2 r = hash2(seed);
    
    // Uniform distribution in triangle using sqrt method
    float sqrtR1 = sqrt(r.x);
    float u = 1.0 - sqrtR1;
    float v = r.y * sqrtR1;
    float w = 1.0 - u - v;
    
    return vec3(u, v, w);
}

// Interpolate position using barycentric coordinates
vec3 barycentricInterpolate(vec3 v0, vec3 v1, vec3 v2, vec3 bary) {
    return v0 * bary.x + v1 * bary.y + v2 * bary.z;
}

// Generate a single grass blade
void GenerateGrassBlade(vec3 basePos, vec3 terrainNormal, float angle, float randomSeed, float densityFactor, int segments)
{
    // Calculate distance to camera for LOD
    float distToCamera = length(basePos - uCameraPos);

    // Skip if beyond max distance
    if (distToCamera > u_MaxRenderDistance) return;

    // Calculate fade factor for smooth LOD transition
    float fadeStart = u_MaxRenderDistance - u_FadeRange;
    float fadeFactor = 1.0 - smoothstep(fadeStart, u_MaxRenderDistance, distToCamera);
    if (fadeFactor < 0.01) return;

    // Random height variation
    float heightVar = mix(1.0 - u_BladeHeightVariation, 1.0 + u_BladeHeightVariation, hash(basePos.xz + randomSeed));
    float bladeHeight = u_BladeHeight * heightVar * fadeFactor * densityFactor;

    // Random width variation
    float widthVar = mix(0.7, 1.3, hash(basePos.xz + randomSeed + 0.5));
    float bladeWidth = u_BladeWidth * widthVar;

    // Create local coordinate system aligned with terrain normal
    vec3 up = terrainNormal;
    vec3 right = normalize(cross(up, vec3(1.0, 0.0, 0.0)));
    if (length(right) < 0.1) right = normalize(cross(up, vec3(0.0, 0.0, 1.0)));
    vec3 forward = cross(right, up);

    // Rotate around up vector by angle
    float cosA = cos(angle);
    float sinA = sin(angle);
    vec3 bladeRight = right * cosA + forward * sinA;
    vec3 bladeForward = forward * cosA - right * sinA;

    // Wind calculation with turbulence
    vec2 windDir = normalize(u_WindDirection);
    float windPhase = uTime * u_WindSpeed + basePos.x * 0.3 + basePos.z * 0.4;
    float windNoise = sin(windPhase) * 0.6 + sin(windPhase * 2.1 + randomSeed) * 0.25 + sin(windPhase * 4.3) * 0.15;
    windNoise *= u_WindTurbulence;
    vec3 windOffset = vec3(windDir.x, 0.0, windDir.y) * windNoise * u_WindStrength;

    // Color variation per blade
    float colorVar = mix(-u_ColorVariation, u_ColorVariation, hash(basePos.xz + randomSeed + 1.0));
    vec3 colorTop = u_ColorTop.rgb + vec3(colorVar);
    vec3 colorBottom = u_ColorBottom.rgb + vec3(colorVar * 0.5);

    // Generate blade with variable segments based on LOD
    for (int seg = 0; seg <= segments; seg++)
    {
        float t = float(seg) / float(segments);
        float t2 = t * t;
        
        // Taper width towards tip
        float widthScale = 1.0 - t * 0.85;
        float currentWidth = bladeWidth * widthScale;
        
        // Curve blade using quadratic bezier
        float curve = u_BladeCurvature * t2;
        vec3 curveOffset = bladeForward * curve * bladeHeight * 0.4;
        
        // Wind affects upper parts more (quadratic falloff)
        vec3 windOffsetScaled = windOffset * t2;
        
        // Calculate position along blade height
        vec3 centerPos = basePos + up * (t * bladeHeight) + curveOffset + windOffsetScaled;
        
        // Color interpolation
        vec3 vertexColor = mix(colorBottom, colorTop, t);
        
        // Create normal (facing outward)
        vec3 normal = normalize(mix(terrainNormal, bladeRight, 0.3 + t * 0.2));
        
        // Generate two vertices (left and right edge of blade)
        for (int side = 0; side < 2; side++)
        {
            float sideSign = (side == 0) ? -1.0 : 1.0;
            vec3 pos = centerPos + bladeRight * (sideSign * currentWidth * 0.5);
            
            gs_out.worldPos = pos;
            gs_out.normal = normal;
            gs_out.uv = vec2(float(side), t);
            gs_out.heightFactor = t;
            gs_out.color = vertexColor;
            
            gl_Position = uViewProj * vec4(pos, 1.0);
            EmitVertex();
        }
    }
    
    EndPrimitive();
}

void main()
{
    // Get triangle vertices
    vec3 v0 = gs_in[0].worldPos;
    vec3 v1 = gs_in[1].worldPos;
    vec3 v2 = gs_in[2].worldPos;
    
    // Get triangle center and area
    vec3 triCenter = (v0 + v1 + v2) / 3.0;
    vec3 edge1 = v1 - v0;
    vec3 edge2 = v2 - v0;
    float triArea = length(cross(edge1, edge2)) * 0.5;
    
    // Calculate average normal
    vec3 avgNormal = normalize(gs_in[0].normal + gs_in[1].normal + gs_in[2].normal);
    
    // Skip based on slope (normal.y is cos of angle from vertical)
    // u_MinSlopeY = cos(maxSlope), u_MaxSlopeY = cos(minSlope)
    if (avgNormal.y < u_MinSlopeY || avgNormal.y > u_MaxSlopeY) return;
    
    // Skip based on height
    if (triCenter.y < u_MinHeight || triCenter.y > u_MaxHeight) return;
    
    // Distance-based LOD culling (skip entire triangle if too far)
    float distToCamera = length(triCenter - uCameraPos);
    if (distToCamera > u_MaxRenderDistance) return;
    
    // Coverage noise - create natural patchy distribution using FBM
    float coverage = fbm(triCenter.xz * u_CoverageNoiseScale, 3);
    
    // Apply coverage threshold with smooth transition
    float coverageFactor = smoothstep(u_CoverageThreshold - 0.1, u_CoverageThreshold + 0.1, coverage);
    if (coverageFactor < 0.01) return;
    
    // Density map sampling (if available)
    float densityMapValue = 1.0;
    if (u_HasDensityMap > 0) {
        vec2 densityUV = triCenter.xz * u_DensityMapScale;
        densityMapValue = texture(u_DensityMap, densityUV).r;
        if (densityMapValue < 0.01) return;
    }

    // Early exit if density is 0 or negative
    if (u_Density <= 0.0) return;

    // === LOD CALCULATION ===
    // Determine LOD level and parameters based on distance
    int lodLevel = 0; // 0=highest detail, 3=lowest detail
    int numSegments = 2; // Default segments per blade
    float lodDensityMult = 1.0; // Density multiplier for LOD

    if (u_LodEnabled > 0) {
        if (distToCamera > u_LodDistance3) {
            lodLevel = 3;
            numSegments = 1; // Lowest detail: 1 segment (3 vertices per blade)
            lodDensityMult = 0.4; // 40% density at far distance
        } else if (distToCamera > u_LodDistance2) {
            lodLevel = 2;
            numSegments = 1; // Medium-low detail: 1 segment
            lodDensityMult = 0.7; // 70% density
        } else if (distToCamera > u_LodDistance1) {
            lodLevel = 1;
            numSegments = 2; // Medium-high detail: 2 segments
            lodDensityMult = 0.9; // 90% density
        } else {
            lodLevel = 0;
            numSegments = 2; // Highest detail: 2 segments
            lodDensityMult = 1.0; // 100% density
        }
    }

    // Calculate number of blades based on triangle area and density with LOD multiplier
    // Larger triangles get more blades for uniform coverage
    float baseBladeCount = float(u_BladesPerVertex);
    float areaFactor = clamp(triArea / 4.0, 0.5, 3.0); // Normalize around 4 square units
    int numBlades = int(baseBladeCount * areaFactor * u_Density * coverageFactor * densityMapValue * lodDensityMult);

    // Dynamic max based on segments: 1 segment = 3 verts, 2 segments = 6 verts
    // max_vertices = 60, so with 1 segment we can fit 20 blades, with 2 segments we fit 10 blades
    int maxBlades = (numSegments == 1) ? 20 : 10;
    maxBlades = min(maxBlades, u_MaxBladesPerTriangle); // Respect user-configured max
    numBlades = clamp(numBlades, 0, maxBlades);

    if (numBlades == 0) return;

    // Generate blades distributed across triangle surface using barycentric coords
    for (int i = 0; i < numBlades; i++)
    {
        // Generate random barycentric coordinates for this blade
        vec2 seed = triCenter.xz + vec2(float(i) * 17.3, float(i) * 31.7);
        vec3 bary = randomBarycentric(seed);

        // Interpolate position and normal using barycentric coords
        vec3 bladePos = barycentricInterpolate(v0, v1, v2, bary);
        vec3 bladeNormal = normalize(barycentricInterpolate(gs_in[0].normal, gs_in[1].normal, gs_in[2].normal, bary));

        // Add small random offset to break up patterns
        vec2 jitter = (hash2(seed + 0.5) - 0.5) * 0.2;
        bladePos.xz += jitter;

        // Skip if slope out of range at this specific point
        if (bladeNormal.y < u_MinSlopeY || bladeNormal.y > u_MaxSlopeY) continue;

        // Random rotation for each blade
        float angle = hash(seed) * 6.28318; // 2*PI
        float randomSeed = float(i) + hash(seed) * 100.0;

        // Height variation based on coverage
        float localDensity = coverageFactor * densityMapValue;

        GenerateGrassBlade(bladePos, bladeNormal, angle, randomSeed, sqrt(localDensity), numSegments);
    }
}
