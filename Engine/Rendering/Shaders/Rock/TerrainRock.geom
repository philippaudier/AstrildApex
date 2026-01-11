#version 420 core

layout(triangles) in;
layout(triangle_strip, max_vertices = 48) out; // 1 rock per triangle, max 8 faces * 3 vertices = 24 + margin

#include "../Includes/Common.glsl"

// Input from vertex shader
in VS_OUT {
    vec3 worldPos;
    vec3 normal;
    vec2 uv;
} gs_in[];

// Output to fragment shader - simplified to reduce per-vertex data
out GS_OUT {
    vec3 worldPos;
    vec3 normal;
    float aoFactor;     // AO + height packed
    vec3 rockColor;     // Per-rock color variation includes moss
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
uniform int u_LodEnabled;
uniform float u_LodDistance1;
uniform float u_LodDistance2;
uniform float u_LodDistance3;

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

// Octahedron - 6 vertices, 8 complete faces - CLOSED shape
const vec3 baseVerts[6] = vec3[6](
    vec3( 1.0,  0.0,  0.0),  // +X
    vec3(-1.0,  0.0,  0.0),  // -X
    vec3( 0.0,  1.0,  0.0),  // +Y (top)
    vec3( 0.0, -1.0,  0.0),  // -Y (bottom)
    vec3( 0.0,  0.0,  1.0),  // +Z
    vec3( 0.0,  0.0, -1.0)   // -Z
);

// 8 triangular faces - complete closed octahedron with correct CCW winding (viewed from outside)
const int rockTris[24] = int[24](
    // Top 4 faces (connecting to +Y vertex)
    2, 4, 0,  // top-front-right
    2, 0, 5,  // top-right-back  
    2, 5, 1,  // top-back-left
    2, 1, 4,  // top-left-front
    // Bottom 4 faces (connecting to -Y vertex)
    3, 0, 4,  // bottom-front-right
    3, 5, 0,  // bottom-right-back
    3, 1, 5,  // bottom-back-left
    3, 4, 1   // bottom-left-front
);

// Randomize a base vertex position for more organic shapes
vec3 randomizeVertex(vec3 v, vec3 seed, float amount) {
    vec3 rand = hash3(seed + v * 7.3) * 2.0 - 1.0;
    return normalize(v + rand * amount);
}

// Apply noise displacement to a rock vertex - more organic version
vec3 displaceRockVertex(vec3 localPos, vec3 rockSeed, float size, float roundness) {
    // Scale position for noise sampling
    vec3 noisePos = localPos * u_NoiseFrequency + rockSeed;
    
    // FBM displacement for natural bumpy surface
    float fbmDisp = fbm(noisePos, u_NoiseOctaves, u_NoiseLacunarity, u_NoisePersistence);
    fbmDisp = fbmDisp * 2.0 - 1.0; // Remap to [-1, 1]
    
    // Voronoi for cracks/crevices - reduced for rounder rocks
    vec2 vor = voronoi(noisePos * u_CrackScale);
    float crackFactor = smoothstep(0.0, 0.15, vor.y - vor.x);
    float crackDisp = -u_CrackDepth * (1.0 - crackFactor) * (1.0 - roundness);
    
    // Sharp edges - reduced based on roundness
    float sharpNoise = abs(fbm(noisePos * 1.5, 2, 2.0, 0.5)) * u_Sharpness * (1.0 - roundness);
    
    // Smooth bulging noise for rounded rocks
    float bulgeNoise = sin(noisePos.x * 2.0) * sin(noisePos.y * 2.0) * sin(noisePos.z * 2.0) * 0.3 * roundness;
    
    // Combine displacements
    float totalDisp = fbmDisp * u_NoiseAmplitude + crackDisp + sharpNoise * 0.2 + bulgeNoise;
    
    // Soften displacement for roundness
    totalDisp *= mix(1.0, 0.5, roundness);
    
    // Apply along surface normal (outward from center)
    vec3 normal = normalize(localPos);
    return localPos + normal * totalDisp * size * 0.5;
}

// Calculate normal from displaced position
vec3 calcRockNormal(vec3 localPos, vec3 rockSeed, float size, float roundness) {
    float eps = 0.02;
    vec3 dx = displaceRockVertex(localPos + vec3(eps, 0, 0), rockSeed, size, roundness) -
              displaceRockVertex(localPos - vec3(eps, 0, 0), rockSeed, size, roundness);
    vec3 dy = displaceRockVertex(localPos + vec3(0, eps, 0), rockSeed, size, roundness) -
              displaceRockVertex(localPos - vec3(0, eps, 0), rockSeed, size, roundness);
    vec3 dz = displaceRockVertex(localPos + vec3(0, 0, eps), rockSeed, size, roundness) -
              displaceRockVertex(localPos - vec3(0, 0, eps), rockSeed, size, roundness);
    
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

    // === LOD SYSTEM ===
    // Determine number of faces based on distance LOD
    int numFaces = 8; // Default: highest detail (8 faces)

    if (u_LodEnabled > 0) {
        if (distToCamera > u_LodDistance3) {
            numFaces = 4; // LOD 3: lowest detail - simple tetrahedron
        } else if (distToCamera > u_LodDistance2) {
            numFaces = 6; // LOD 2: medium detail
        } else if (distToCamera > u_LodDistance1) {
            numFaces = 7; // LOD 1: medium-high detail
        } else {
            numFaces = 8; // LOD 0: highest detail - full octahedron
        }
        // Apply bias multiplier
        numFaces = int(float(numFaces) * u_LodBias);
        numFaces = clamp(numFaces, 4, 8); // Ensure valid range
    } else {
        // Fallback: old LOD system using distance-based interpolation
        float lodDetail = clamp(1.0 - (distToCamera / u_MaxRenderDistance) * (1.0 - u_LodBias), 0.3, 1.0);
        numFaces = int(8.0 * lodDetail);
        numFaces = max(numFaces, 4);
    }
    
    // Per-rock randomness for shape variety
    float rockRand = hash(rockSeed);
    float roundness = rockRand * 0.7 + 0.3; // 0.3 to 1.0 - more rounded variety
    float asymmetry = hash(rockSeed.yx) * 0.5; // Random asymmetric stretching
    
    // Build rock transform
    float randomAngle = hash(rockSeed.xy) * 6.28318 * u_RotationRandomness;
    mat3 rotation = buildRotationMatrix(terrainNormal, randomAngle, u_AlignToTerrain);
    
    // Apply size with random asymmetric scaling for variety
    vec3 asymScale = vec3(
        1.0 + (hash(rockSeed + 0.1) - 0.5) * asymmetry,
        1.0 + (hash(rockSeed + 0.2) - 0.5) * asymmetry,
        1.0 + (hash(rockSeed + 0.3) - 0.5) * asymmetry
    );
    vec3 scale = vec3(rockSize) * asymScale;
    scale.y *= (1.0 - u_FlattenY * 0.5);
    
    // Embed rock into ground
    vec3 embedOffset = vec3(0.0, -rockSize * u_EmbedDepth, 0.0);
    embedOffset = rotation * embedOffset;
    basePos += embedOffset;
    
    // Generate rock triangles with more organic base shape
    for (int face = 0; face < numFaces; face++)
    {
        int idx = face * 3;
        
        // Get base vertices and randomize them for organic shape
        vec3 v0 = randomizeVertex(baseVerts[rockTris[idx + 0]], rockSeed, asymmetry);
        vec3 v1 = randomizeVertex(baseVerts[rockTris[idx + 1]], rockSeed, asymmetry);
        vec3 v2 = randomizeVertex(baseVerts[rockTris[idx + 2]], rockSeed, asymmetry);
        
        // Apply scale
        v0 *= scale;
        v1 *= scale;
        v2 *= scale;
        
        // Displace for rock detail with roundness
        vec3 d0 = displaceRockVertex(v0, rockSeed, rockSize, roundness);
        vec3 d1 = displaceRockVertex(v1, rockSeed, rockSize, roundness);
        vec3 d2 = displaceRockVertex(v2, rockSeed, rockSize, roundness);
        
        // Calculate normals
        vec3 n0 = calcRockNormal(v0, rockSeed, rockSize, roundness);
        vec3 n1 = calcRockNormal(v1, rockSeed, rockSize, roundness);
        vec3 n2 = calcRockNormal(v2, rockSeed, rockSize, roundness);
        
        // Apply faceting (reduced for rounder rocks)
        vec3 faceNormal = normalize(cross(d1 - d0, d2 - d0));
        float facetAmount = u_FacetStrength * (1.0 - roundness * 0.7); // Less faceting for round rocks
        n0 = normalize(mix(n0, faceNormal, facetAmount));
        n1 = normalize(mix(n1, faceNormal, facetAmount));
        n2 = normalize(mix(n2, faceNormal, facetAmount));
        
        // Transform to world space
        vec3 w0 = basePos + rotation * d0;
        vec3 w1 = basePos + rotation * d1;
        vec3 w2 = basePos + rotation * d2;
        
        vec3 wn0 = normalize(rotation * n0);
        vec3 wn1 = normalize(rotation * n1);
        vec3 wn2 = normalize(rotation * n2);
        
        // Calculate moss based on upward-facing
        // Moss blended into color
        float moss0 = u_MossAmount * smoothstep(u_MossTopBias - 0.3, u_MossTopBias + 0.3, wn0.y);
        float moss1 = u_MossAmount * smoothstep(u_MossTopBias - 0.3, u_MossTopBias + 0.3, wn1.y);
        float moss2 = u_MossAmount * smoothstep(u_MossTopBias - 0.3, u_MossTopBias + 0.3, wn2.y);
        
        // Blend moss into rock color directly
        vec3 mossCol = u_MossColor.rgb;
        vec3 color0 = mix(rockColor, mossCol, moss0);
        vec3 color1 = mix(rockColor, mossCol, moss1);
        vec3 color2 = mix(rockColor, mossCol, moss2);
        
        // AO hint based on crevices (lower areas darker)
        float ao0 = 0.5 + 0.5 * clamp(dot(n0, normalize(v0)), 0.0, 1.0);
        float ao1 = 0.5 + 0.5 * clamp(dot(n1, normalize(v1)), 0.0, 1.0);
        float ao2 = 0.5 + 0.5 * clamp(dot(n2, normalize(v2)), 0.0, 1.0);
        
        // Emit triangle - simplified output
        gs_out.worldPos = w0;
        gs_out.normal = wn0;
        gs_out.aoFactor = ao0;
        gs_out.rockColor = color0;
        gl_Position = uViewProj * vec4(w0, 1.0);
        EmitVertex();
        
        gs_out.worldPos = w1;
        gs_out.normal = wn1;
        gs_out.aoFactor = ao1;
        gs_out.rockColor = color1;
        gl_Position = uViewProj * vec4(w1, 1.0);
        EmitVertex();
        
        gs_out.worldPos = w2;
        gs_out.normal = wn2;
        gs_out.aoFactor = ao2;
        gs_out.rockColor = color2;
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
    
    // === IMPROVED PLACEMENT - avoid grid patterns ===
    // Use a unique hash based on ALL vertex positions, not just center
    // This ensures each triangle has a truly unique seed
    float triHash = hash(v0 + v1 * 0.7 + v2 * 0.3);
    vec3 triSeed = vec3(
        hash(vec3(v0.x, v1.z, v2.y)),
        hash(vec3(v1.x, v2.z, v0.y)),
        hash(vec3(v2.x, v0.z, v1.y))
    );
    
    // Early exit if density is 0 or negative
    if (u_Density <= 0.0) return;

    // Clustering noise - sample at world position with offset to break grid
    vec3 clusterSamplePos = triCenter + triSeed * 10.0;
    float clusterNoise = fbm(clusterSamplePos * u_ClusterNoiseScale, 2, 2.0, 0.5);
    float placementChance = mix(1.0, clusterNoise, u_ClusteringStrength);

    // Placement threshold with CUBIC curve for extreme low-density control
    // density 0.01 -> 0.001, density 0.1 -> 0.01, density 0.5 -> 0.125
    float placementRand = hash(triSeed.xy + triSeed.z);
    float effectiveDensity = u_Density * u_Density * u_Density; // Cubic for very low densities
    effectiveDensity *= placementChance;
    
    // Also apply sparseness (PlacementThreshold) - higher = fewer rocks
    effectiveDensity *= (1.0 - u_PlacementThreshold * 0.99);
    
    if (placementRand > effectiveDensity) return;
    
    // Calculate triangle area
    vec3 edge1 = v1 - v0;
    vec3 edge2 = v2 - v0;
    float triArea = length(cross(edge1, edge2)) * 0.5;
    
    // Skip very tiny triangles
    if (triArea < 0.01) return;
    
    // Generate ONE rock per qualifying triangle (cleaner distribution)
    // The density is handled by the placement threshold above
    
    // === BARYCENTRIC PLACEMENT - well inside triangle, not on edges ===
    // Use improved random that avoids edges
    float r1 = hash(triSeed.xz);
    float r2 = hash(triSeed.yz);
    
    // Shrink towards center to avoid edge placement
    // Map [0,1] to [0.2, 0.8] range to stay away from edges
    r1 = 0.2 + r1 * 0.6;
    r2 = 0.2 + r2 * 0.6;
    
    // Barycentric coordinates using square root for uniform distribution
    float sqrtR1 = sqrt(r1);
    float baryU = 1.0 - sqrtR1;
    float baryV = r2 * sqrtR1;
    float baryW = 1.0 - baryU - baryV;
    
    // Ensure we're inside triangle (normalize barycentric coords)
    float total = baryU + baryV + baryW;
    baryU /= total;
    baryV /= total;
    baryW /= total;
    
    // Additional pull towards center to avoid edges
    float centerPull = 0.25;
    baryU = mix(baryU, 0.333, centerPull);
    baryV = mix(baryV, 0.333, centerPull);
    baryW = mix(baryW, 0.333, centerPull);
    
    vec3 rockPos = v0 * baryU + v1 * baryV + v2 * baryW;
    vec3 rockNormal = normalize(gs_in[0].normal * baryU + gs_in[1].normal * baryV + gs_in[2].normal * baryW);
    
    // Random size using unique seed
    float sizeRand = hash(triSeed + 0.5);
    float rockSize = mix(u_MinSize, u_MaxSize, sizeRand);
    rockSize *= mix(1.0 - u_SizeVariation, 1.0 + u_SizeVariation, hash(triSeed.xy + 1.0));
    
    // Per-rock seed for unique shape - use full triangle vertex info
    vec3 rockSeed = triSeed * 100.0 + vec3(triHash * 50.0);
    
    // Color variation
    float colorVar = (hash(triSeed + 2.0) - 0.5) * u_ColorVariation;
    vec3 rockColor = u_BaseColor.rgb + vec3(colorVar);
    
    // Generate the rock
    GenerateRock(rockPos, rockNormal, rockSeed, rockSize, rockColor);
}
