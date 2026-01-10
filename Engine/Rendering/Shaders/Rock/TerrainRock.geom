#version 420 core

layout(triangles) in;
layout(triangle_strip, max_vertices = 60) out; // Max ~2 rocks per triangle, ~30 vertices each

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
    vec3 localPos;      // Position in rock-local space for texturing
    float aoFactor;     // Ambient occlusion hint
    vec3 rockColor;     // Per-rock color variation
    float mossBlend;    // Moss coverage factor
} gs_out;

// === UNIFORMS ===

// Density & Distribution
uniform float u_Density;
uniform float u_ClusteringStrength;
uniform float u_ClusterNoiseScale;
uniform float u_PlacementThreshold;

// Slope & Height
uniform float u_MinSlopeY;
uniform float u_MaxSlopeY;
uniform float u_MinHeight;
uniform float u_MaxHeight;

// Size
uniform float u_MinSize;
uniform float u_MaxSize;
uniform float u_SizeVariation;
uniform float u_FlattenY;

// Noise
uniform float u_NoiseFrequency;
uniform float u_NoiseAmplitude;
uniform int u_NoiseOctaves;
uniform float u_NoiseLacunarity;
uniform float u_NoisePersistence;

// Shape
uniform float u_Sharpness;
uniform float u_FacetStrength;
uniform float u_CrackDepth;
uniform float u_CrackScale;

// Colors
uniform vec4 u_BaseColor;
uniform vec4 u_DarkColor;
uniform vec4 u_HighlightColor;
uniform float u_ColorVariation;

// Moss
uniform float u_MossAmount;
uniform vec4 u_MossColor;
uniform float u_MossTopBias;

// Embedding & Orientation
uniform float u_EmbedDepth;
uniform float u_AlignToTerrain;
uniform float u_RotationRandomness;

// LOD
uniform float u_MaxRenderDistance;
uniform float u_FadeRange;
uniform float u_LodBias;

// ============================================
// NOISE FUNCTIONS
// ============================================

// Hash functions for randomness
float hash(float n) { return fract(sin(n) * 43758.5453123); }
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float hash(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }

vec3 hash3(vec3 p) {
    return vec3(
        hash(p),
        hash(p + vec3(31.7, 17.3, 91.1)),
        hash(p + vec3(71.3, 43.9, 23.7))
    );
}

vec2 hash2(vec2 p) {
    return vec2(hash(p), hash(p + vec2(31.7, 17.3)));
}

// Smooth 3D noise
float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    f = f * f * (3.0 - 2.0 * f); // Smooth interpolation
    
    float n = i.x + i.y * 157.0 + 113.0 * i.z;
    return mix(
        mix(mix(hash(n + 0.0), hash(n + 1.0), f.x),
            mix(hash(n + 157.0), hash(n + 158.0), f.x), f.y),
        mix(mix(hash(n + 113.0), hash(n + 114.0), f.x),
            mix(hash(n + 270.0), hash(n + 271.0), f.x), f.y),
        f.z);
}

// FBM (Fractal Brownian Motion) for natural rock detail
float fbm(vec3 p, int octaves, float lacunarity, float persistence) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    
    for (int i = 0; i < octaves && i < 5; i++) {
        value += amplitude * noise3D(p * frequency);
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    return value;
}

// Voronoi for cracks and crevices
vec2 voronoi(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    
    float minDist = 1.0;
    float secondMinDist = 1.0;
    
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            for (int z = -1; z <= 1; z++) {
                vec3 neighbor = vec3(float(x), float(y), float(z));
                vec3 cellPos = hash3(i + neighbor);
                vec3 diff = neighbor + cellPos - f;
                float d = dot(diff, diff);
                
                if (d < minDist) {
                    secondMinDist = minDist;
                    minDist = d;
                } else if (d < secondMinDist) {
                    secondMinDist = d;
                }
            }
        }
    }
    
    return vec2(sqrt(minDist), sqrt(secondMinDist));
}

// ============================================
// ROCK GENERATION
// ============================================

// Octahedron base vertices (8 faces, good for rocks)
const vec3 octaVerts[6] = vec3[6](
    vec3( 1.0,  0.0,  0.0),
    vec3(-1.0,  0.0,  0.0),
    vec3( 0.0,  1.0,  0.0),
    vec3( 0.0, -1.0,  0.0),
    vec3( 0.0,  0.0,  1.0),
    vec3( 0.0,  0.0, -1.0)
);

// Octahedron triangles (8 faces)
const int octaTris[24] = int[24](
    0, 2, 4,  // +X +Y +Z
    0, 4, 3,  // +X -Y +Z
    0, 3, 5,  // +X -Y -Z
    0, 5, 2,  // +X +Y -Z
    1, 4, 2,  // -X +Y +Z
    1, 3, 4,  // -X -Y +Z
    1, 5, 3,  // -X -Y -Z
    1, 2, 5   // -X +Y -Z
);

// Apply noise displacement to a rock vertex
vec3 displaceRockVertex(vec3 localPos, vec3 rockSeed, float size) {
    // Scale position for noise sampling
    vec3 noisePos = localPos * u_NoiseFrequency + rockSeed;
    
    // FBM displacement for natural bumpy surface
    float fbmDisp = fbm(noisePos, u_NoiseOctaves, u_NoiseLacunarity, u_NoisePersistence);
    fbmDisp = fbmDisp * 2.0 - 1.0; // Remap to [-1, 1]
    
    // Voronoi for cracks/crevices
    vec2 vor = voronoi(noisePos * u_CrackScale);
    float crackFactor = smoothstep(0.0, 0.15, vor.y - vor.x); // Edge detection
    float crackDisp = -u_CrackDepth * (1.0 - crackFactor);
    
    // Sharp edges using abs noise
    float sharpNoise = abs(fbm(noisePos * 1.5, 2, 2.0, 0.5)) * u_Sharpness;
    
    // Combine displacements
    float totalDisp = fbmDisp * u_NoiseAmplitude + crackDisp + sharpNoise * 0.2;
    
    // Apply along surface normal (outward from center)
    vec3 normal = normalize(localPos);
    return localPos + normal * totalDisp * size * 0.5;
}

// Calculate normal from displaced position
vec3 calcRockNormal(vec3 localPos, vec3 rockSeed, float size) {
    float eps = 0.02;
    vec3 dx = displaceRockVertex(localPos + vec3(eps, 0, 0), rockSeed, size) -
              displaceRockVertex(localPos - vec3(eps, 0, 0), rockSeed, size);
    vec3 dy = displaceRockVertex(localPos + vec3(0, eps, 0), rockSeed, size) -
              displaceRockVertex(localPos - vec3(0, eps, 0), rockSeed, size);
    vec3 dz = displaceRockVertex(localPos + vec3(0, 0, eps), rockSeed, size) -
              displaceRockVertex(localPos - vec3(0, 0, eps), rockSeed, size);
    
    // Cross products for normal
    vec3 normal = cross(dy, dx) + cross(dx, dz) + cross(dz, dy);
    return normalize(normal);
}

// Build rotation matrix from terrain normal
mat3 buildRotationMatrix(vec3 terrainNormal, float randomAngle, float alignStrength) {
    // Blend between up vector and terrain normal
    vec3 up = mix(vec3(0.0, 1.0, 0.0), terrainNormal, alignStrength);
    up = normalize(up);
    
    // Random rotation around up axis
    float c = cos(randomAngle);
    float s = sin(randomAngle);
    
    // Build coordinate frame
    vec3 right = normalize(cross(up, vec3(c, 0.0, s)));
    if (length(right) < 0.1) right = normalize(cross(up, vec3(1.0, 0.0, 0.0)));
    vec3 forward = cross(right, up);
    
    return mat3(right, up, forward);
}

// Generate a single procedural rock
void GenerateRock(vec3 basePos, vec3 terrainNormal, vec3 rockSeed, float rockSize, vec3 rockColor)
{
    // Distance LOD check
    float distToCamera = length(basePos - uCameraPos);
    if (distToCamera > u_MaxRenderDistance) return;
    
    // Fade factor
    float fadeStart = u_MaxRenderDistance - u_FadeRange;
    float fadeFactor = 1.0 - smoothstep(fadeStart, u_MaxRenderDistance, distToCamera);
    if (fadeFactor < 0.01) return;
    
    // LOD: reduce detail at distance (skip some faces)
    float lodDetail = clamp(1.0 - (distToCamera / u_MaxRenderDistance) * (1.0 - u_LodBias), 0.3, 1.0);
    int numFaces = int(8.0 * lodDetail);
    numFaces = max(numFaces, 4); // Minimum 4 faces
    
    // Build rock transform
    float randomAngle = hash(rockSeed.xy) * 6.28318 * u_RotationRandomness;
    mat3 rotation = buildRotationMatrix(terrainNormal, randomAngle, u_AlignToTerrain);
    
    // Apply size and flattening
    vec3 scale = vec3(rockSize, rockSize * (1.0 - u_FlattenY * 0.5), rockSize);
    
    // Embed rock into ground
    vec3 embedOffset = vec3(0.0, -rockSize * u_EmbedDepth, 0.0);
    embedOffset = rotation * embedOffset;
    basePos += embedOffset;
    
    // Generate rock triangles (octahedron with displacement)
    for (int face = 0; face < numFaces; face++)
    {
        int idx = face * 3;
        
        // Get triangle vertices
        vec3 v0 = octaVerts[octaTris[idx + 0]];
        vec3 v1 = octaVerts[octaTris[idx + 1]];
        vec3 v2 = octaVerts[octaTris[idx + 2]];
        
        // Apply scale
        v0 *= scale;
        v1 *= scale;
        v2 *= scale;
        
        // Displace for rock detail
        vec3 d0 = displaceRockVertex(v0, rockSeed, rockSize);
        vec3 d1 = displaceRockVertex(v1, rockSeed, rockSize);
        vec3 d2 = displaceRockVertex(v2, rockSeed, rockSize);
        
        // Calculate normals
        vec3 n0 = calcRockNormal(v0, rockSeed, rockSize);
        vec3 n1 = calcRockNormal(v1, rockSeed, rockSize);
        vec3 n2 = calcRockNormal(v2, rockSeed, rockSize);
        
        // Apply faceting (flat shading effect)
        vec3 faceNormal = normalize(cross(d1 - d0, d2 - d0));
        n0 = normalize(mix(n0, faceNormal, u_FacetStrength));
        n1 = normalize(mix(n1, faceNormal, u_FacetStrength));
        n2 = normalize(mix(n2, faceNormal, u_FacetStrength));
        
        // Transform to world space
        vec3 w0 = basePos + rotation * d0;
        vec3 w1 = basePos + rotation * d1;
        vec3 w2 = basePos + rotation * d2;
        
        vec3 wn0 = normalize(rotation * n0);
        vec3 wn1 = normalize(rotation * n1);
        vec3 wn2 = normalize(rotation * n2);
        
        // Calculate moss based on upward-facing
        float moss0 = u_MossAmount * smoothstep(u_MossTopBias - 0.3, u_MossTopBias + 0.3, wn0.y);
        float moss1 = u_MossAmount * smoothstep(u_MossTopBias - 0.3, u_MossTopBias + 0.3, wn1.y);
        float moss2 = u_MossAmount * smoothstep(u_MossTopBias - 0.3, u_MossTopBias + 0.3, wn2.y);
        
        // AO hint based on crevices (lower areas darker)
        float ao0 = 0.5 + 0.5 * clamp(dot(n0, normalize(v0)), 0.0, 1.0);
        float ao1 = 0.5 + 0.5 * clamp(dot(n1, normalize(v1)), 0.0, 1.0);
        float ao2 = 0.5 + 0.5 * clamp(dot(n2, normalize(v2)), 0.0, 1.0);
        
        // Emit triangle
        gs_out.worldPos = w0;
        gs_out.normal = wn0;
        gs_out.localPos = v0;
        gs_out.aoFactor = ao0;
        gs_out.rockColor = rockColor;
        gs_out.mossBlend = moss0;
        gl_Position = uViewProj * vec4(w0, 1.0);
        EmitVertex();
        
        gs_out.worldPos = w1;
        gs_out.normal = wn1;
        gs_out.localPos = v1;
        gs_out.aoFactor = ao1;
        gs_out.rockColor = rockColor;
        gs_out.mossBlend = moss1;
        gl_Position = uViewProj * vec4(w1, 1.0);
        EmitVertex();
        
        gs_out.worldPos = w2;
        gs_out.normal = wn2;
        gs_out.localPos = v2;
        gs_out.aoFactor = ao2;
        gs_out.rockColor = rockColor;
        gs_out.mossBlend = moss2;
        gl_Position = uViewProj * vec4(w2, 1.0);
        EmitVertex();
        
        EndPrimitive();
    }
}

// ============================================
// MAIN
// ============================================

void main()
{
    // Get terrain triangle
    vec3 v0 = gs_in[0].worldPos;
    vec3 v1 = gs_in[1].worldPos;
    vec3 v2 = gs_in[2].worldPos;
    
    vec3 triCenter = (v0 + v1 + v2) / 3.0;
    vec3 avgNormal = normalize(gs_in[0].normal + gs_in[1].normal + gs_in[2].normal);
    
    // Slope check
    if (avgNormal.y < u_MinSlopeY || avgNormal.y > u_MaxSlopeY) return;
    
    // Height check
    if (triCenter.y < u_MinHeight || triCenter.y > u_MaxHeight) return;
    
    // Distance culling
    float distToCamera = length(triCenter - uCameraPos);
    if (distToCamera > u_MaxRenderDistance) return;
    
    // Clustering noise for natural distribution
    float clusterNoise = fbm(triCenter * u_ClusterNoiseScale, 2, 2.0, 0.5);
    float placementChance = mix(1.0, clusterNoise, u_ClusteringStrength);
    
    // Placement threshold
    if (placementChance < u_PlacementThreshold) return;
    
    // Calculate triangle area
    vec3 edge1 = v1 - v0;
    vec3 edge2 = v2 - v0;
    float triArea = length(cross(edge1, edge2)) * 0.5;
    
    // Number of rocks based on density and area
    float baseRockCount = u_Density * (triArea / 10.0); // Normalize around 10 sqm
    int numRocks = int(baseRockCount * placementChance);
    numRocks = clamp(numRocks, 0, 2); // Max 2 rocks per triangle
    
    // Generate rocks
    for (int i = 0; i < numRocks; i++)
    {
        // Random barycentric coords for placement
        vec2 seed = triCenter.xz + vec2(float(i) * 17.3, float(i) * 31.7);
        vec2 r = hash2(seed);
        float sqrtR1 = sqrt(r.x);
        float u = 1.0 - sqrtR1;
        float v = r.y * sqrtR1;
        float w = 1.0 - u - v;
        
        vec3 rockPos = v0 * u + v1 * v + v2 * w;
        vec3 rockNormal = normalize(gs_in[0].normal * u + gs_in[1].normal * v + gs_in[2].normal * w);
        
        // Random size
        float sizeRand = hash(seed + 0.5);
        float rockSize = mix(u_MinSize, u_MaxSize, sizeRand);
        rockSize *= mix(1.0 - u_SizeVariation, 1.0 + u_SizeVariation, hash(seed + 1.0));
        
        // Per-rock seed for unique shape
        vec3 rockSeed = vec3(seed, hash(seed));
        
        // Color variation
        float colorVar = (hash(seed + 2.0) - 0.5) * u_ColorVariation;
        vec3 rockColor = u_BaseColor.rgb + vec3(colorVar);
        
        // Generate the rock
        GenerateRock(rockPos, rockNormal, rockSeed, rockSize, rockColor);
    }
}
