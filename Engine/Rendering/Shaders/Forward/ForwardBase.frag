#version 420 core

#include "../Includes/Common.glsl"
#include "../Includes/IBL.glsl"
#include "../Includes/Lighting.glsl"
#include "../Includes/Fog.glsl"
#include "../Includes/Shadows.glsl"

layout(location=0) out vec4 outColor;
layout(location=1) out uint outId;

in vec3 vWorldPos;
in vec3 vNormal;
in vec3 vObjectNormal;
in vec2 vUV;

// === BASE TEXTURES ===
uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;

// === PBR TEXTURES ===
uniform sampler2D u_EmissiveTex;
uniform sampler2D u_MetallicTex;        // Metallic map (R channel)
uniform sampler2D u_RoughnessTex;       // Roughness map (R channel)
uniform sampler2D u_MetallicRoughnessTex; // GLTF 2.0 combined (G=roughness, B=metallic)
uniform sampler2D u_OcclusionTex;       // Ambient occlusion (R channel)
uniform sampler2D u_HeightTex;          // Height/Parallax (R channel)

// === DETAIL TEXTURES ===
uniform sampler2D u_DetailMaskTex;      // Detail mask (R channel)
uniform sampler2D u_DetailAlbedoTex;    // Detail albedo (RGB)
uniform sampler2D u_DetailNormalTex;    // Detail normal (RGB)

// Debug switches (0 = off). Can be set from C# with SetInt("u_DebugShowAlbedo", 1) or
// SetInt("u_DebugShowNormals", 1) to visualize the respective data.
uniform int u_DebugShowAlbedo;
uniform int u_DebugShowNormals;
uniform int u_DebugShowAO;  // Debug: show AO texture
// Shadow debugging uniforms (optional)
uniform vec4  u_AlbedoColor;

// === LOD TRANSITION (Unreal-style dithered fade) ===
uniform float u_LodTransition; // 0.0 = fully transparent, 1.0 = fully opaque
uniform int u_TransparencyMode; // 0 = opaque, 1 = transparent
uniform float u_NormalStrength;

// === PBR PARAMETERS ===
uniform float u_Metallic;

uniform float u_Smoothness;
uniform float u_OcclusionStrength;
uniform vec3  u_EmissiveColor;
uniform float u_HeightScale;

uniform uint  u_ObjectId;

// Triplanar mapping settings
uniform int u_UseTriplanar; // 0 = off, 1 = on
uniform float u_TriplanarScale; // Scale factor for world-space UVs
uniform float u_TriplanarBlendSharpness; // Controls blend sharpness between projections

// Stylization parameters
uniform float u_Saturation;
uniform float u_Brightness;
uniform float u_Contrast;
uniform float u_Hue;
uniform float u_Emission;

// Alpha clipping (alpha test)
uniform int u_AlphaClippingEnabled;
uniform float u_AlphaClipThreshold;

// PERFORMANCE OPTIMIZATION: Texture presence flags (set from CPU to avoid expensive textureSize() calls)
// Each flag is 0 = not present (1x1 placeholder), 1 = present (actual texture)
// Using uniforms saves ~200 GPU cycles per fragment vs textureSize() checks
uniform int u_HasMetallicRoughnessTex;
uniform int u_HasMetallicTex;
uniform int u_HasRoughnessTex;
uniform int u_HasOcclusionTex;
uniform int u_HasEmissiveTex;
uniform int u_HasHeightTex;
uniform int u_HasDetailMask;
uniform int u_HasDetailAlbedo;
uniform int u_HasDetailNormal;

// SSAO uniforms
uniform sampler2D u_SSAOTexture;
uniform int u_SSAOEnabled;
uniform float u_SSAOStrength;
uniform vec2 u_ScreenSize;
// Debug: show shadow projection / sampling when non-zero
uniform int u_DebugShowShadows;

// === WATER CAUSTICS (for objects below water) ===
uniform int   u_WaterCausticsEnabled;  // 0 = off, 1 = on
uniform float u_WaterCausticsStrength;
uniform float u_WaterCausticsScale;
uniform float u_WaterCausticsSpeed;
uniform vec3  u_WaterCausticsColor;
uniform float u_WaterCausticsSplit;      // Chromatic aberration
uniform float u_WaterCausticsDistortion; // Physics strength
uniform sampler2D u_WaterNormalTex;      // Water surface normal map (for caustics calculation)
uniform sampler2D u_WaterNormalTex2;     // Second water normal layer
uniform float u_WaterNormalStrength;
uniform float u_WaterNormalStrength2;
uniform float u_WaterNormalBlend;
uniform float u_Time;                    // For caustics animation

// === WEATHER PARAMETERS ===
uniform float u_RainIntensity;
uniform float u_SnowAccumulation;  // Accumulated snow (can exceed 1.0)
uniform float u_SnowIntensity;     // Snow falling rate
uniform float u_Wetness;

// Advanced snow parameters
uniform float u_SnowSlopeMin;
uniform float u_SnowSlopeMax;
uniform float u_SnowSparkle;
uniform float u_SnowDisplacement;

// Snow material textures
uniform sampler2D u_SnowAlbedoTex;
uniform sampler2D u_SnowNormalTex;
uniform sampler2D u_SnowMetallicRoughnessTex;
uniform vec4 u_SnowAlbedoColor;
uniform float u_SnowMetallic;
uniform float u_SnowRoughness;
uniform vec2 u_SnowTextureTiling;
uniform float u_SnowNormalStrength;

// === SNOW UTILITY FUNCTIONS ===
// Calculate snow placement based on slope (0.0 = vertical, 1.0 = horizontal)
float calculateSnowPlacement(vec3 normal, float slopeMinDeg, float slopeMaxDeg)
{
    vec3 up = vec3(0, 1, 0);
    float upDot = dot(normalize(normal), up);

    // Convert to angle in degrees
    float angleFromVertical = degrees(acos(upDot));

    // Extended fade ranges for smoother accumulation/melting (20 degrees instead of 10)
    // This creates a gradual transition where snow progressively accumulates/melts
    float fadeInStart = slopeMinDeg;
    float fadeInEnd = slopeMinDeg + 20.0;  // Larger range for gradual accumulation
    float fadeOutStart = slopeMaxDeg - 20.0;  // Larger range for gradual melting
    float fadeOutEnd = slopeMaxDeg;

    float fadeIn = smoothstep(fadeInStart, fadeInEnd, angleFromVertical);
    float fadeOut = 1.0 - smoothstep(fadeOutStart, fadeOutEnd, angleFromVertical);

    // FIXED: Was (1.0 - fadeIn) * fadeOut which inverted the logic
    return clamp(fadeIn * fadeOut, 0.0, 1.0);
}

// Calculate snow sparkle effect
// Uses random micro-facet normals + real directional light for realistic sparkles
float calculateSnowSparkle(vec3 worldPos, vec3 normal, vec3 viewDir, float sparkleIntensity)
{
    // Early out if no light or no sparkle intensity
    if (uDirLightIntensity <= 0.0 || sparkleIntensity < 0.01) return 0.0;

    // Generate random sparkle pattern
    vec2 p = worldPos.xz * 10.0;
    float random1 = fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453);
    float random2 = fract(sin(dot(p, vec2(39.346, 11.135))) * 22345.6789);
    float sparkleNoise = random1 * random2;

    // Only 30% of surface sparkles (like real ice crystals)
    if (sparkleNoise < 0.7) return 0.0;

    vec3 N = normalize(normal);
    vec3 V = normalize(viewDir);
    vec3 L = normalize(-uDirLightDirection); // Real directional light

    // Create random micro-facet normal for this sparkle crystal
    // Each ice crystal has a random orientation
    float theta = random1 * 6.28318; // Random angle around normal (0-360°)
    float phi = acos(1.0 - random2 * 0.5); // Random tilt (0-60°)

    vec3 tangent = normalize(cross(N, vec3(0.0, 1.0, 0.0)));
    if (length(tangent) < 0.1) tangent = normalize(cross(N, vec3(1.0, 0.0, 0.0)));
    vec3 bitangent = normalize(cross(N, tangent));

    vec3 microNormal = cos(phi) * N + sin(phi) * (cos(theta) * tangent + sin(theta) * bitangent);
    microNormal = normalize(microNormal);

    // Calculate specular reflection from the light towards the view
    // This is the classic Blinn-Phong but with micro-facets
    vec3 H = normalize(L + V); // Halfway vector between light and view
    float NdotH = max(0.0, dot(microNormal, H));
    float specular = pow(NdotH, 512.0); // Very sharp, like real ice crystal reflections

    // Only sparkle if the micro-facet faces both the light and the viewer
    float NdotL = max(0.0, dot(microNormal, L));
    float NdotV = max(0.0, dot(microNormal, V));
    if (NdotL < 0.1 || NdotV < 0.1) return 0.0; // Crystal not oriented correctly

    // Normalize sparkle noise to 0-1 range
    float sparkleAmount = (sparkleNoise - 0.7) / 0.3;

    // Final sparkle = specular * random pattern * light intensity * sparkle intensity
    // Modulated by light color (sparkles take the color of the light)
    float luminance = dot(uDirLightColor, vec3(0.299, 0.587, 0.114));
    float sparkle = specular * sparkleAmount * sparkleIntensity * uDirLightIntensity * luminance * 8.0;

    return sparkle;
}

// Stylization utility functions
vec3 adjustSaturation(vec3 color, float saturation) {
    vec3 grayscale = vec3(dot(color, vec3(0.299, 0.587, 0.114)));
    return mix(grayscale, color, saturation);
}

vec3 adjustBrightness(vec3 color, float brightness) {
    return color * brightness;
}

vec3 adjustContrast(vec3 color, float contrast) {
    return (color - 0.5) * contrast + 0.5;
}

vec3 rgb2hsv(vec3 rgb) {
    vec4 k = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(rgb.bg, k.wz), vec4(rgb.gb, k.xy), step(rgb.b, rgb.g));
    vec4 q = mix(vec4(p.xyw, rgb.r), vec4(rgb.r, p.yzx), step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 hsv) {
    vec4 k = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(hsv.xxx + k.xyz) * 6.0 - k.www);
    return hsv.z * mix(k.xxx, clamp(p - k.xxx, 0.0, 1.0), hsv.y);
}

vec3 adjustHue(vec3 color, float hue) {
    // Convert RGB to HSV, adjust hue, convert back to RGB
    vec3 hsv = rgb2hsv(color);
    hsv.x = fract(hsv.x + hue * 0.5); // hue is in -1 to 1 range, convert to 0-1
    return hsv2rgb(hsv);
}

// Triplanar texture sampling with proper normal blending
struct TriplanarSample {
    vec3 albedo;
    vec3 normal;
};

TriplanarSample SampleTriplanar(vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    // Calculate blend weights based on surface normal
    vec3 blendWeights = abs(worldNormal);

    // Apply sharpness to blend (higher = sharper transitions)
    blendWeights = pow(blendWeights, vec3(blendSharpness));

    // Normalize blend weights so they sum to 1
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z);

    // Calculate UVs for each projection plane
    vec2 uvX = worldPos.yz * scale; // Side projection (X-axis)
    vec2 uvY = worldPos.xz * scale; // Top/Bottom projection (Y-axis)
    vec2 uvZ = worldPos.xy * scale; // Front/Back projection (Z-axis)

    // Sample albedo from all three projections
    vec3 albedoX = texture(u_AlbedoTex, uvX).rgb;
    vec3 albedoY = texture(u_AlbedoTex, uvY).rgb;
    vec3 albedoZ = texture(u_AlbedoTex, uvZ).rgb;

    // Blend albedo
    vec3 albedo = albedoX * blendWeights.x +
                  albedoY * blendWeights.y +
                  albedoZ * blendWeights.z;

    // Sample normal maps from all three projections
    vec3 normalX = texture(u_NormalTex, uvX).xyz * 2.0 - 1.0;
    vec3 normalY = texture(u_NormalTex, uvY).xyz * 2.0 - 1.0;
    vec3 normalZ = texture(u_NormalTex, uvZ).xyz * 2.0 - 1.0;

    // Flip Y channel for OpenGL normal maps
    normalX.y = -normalX.y;
    normalY.y = -normalY.y;
    normalZ.y = -normalZ.y;

    // Transform tangent-space normals to world space for each projection
    // X-axis projection (YZ plane): tangent=Y, bitangent=Z, normal=X
    vec3 worldNormalX = vec3(normalX.z, normalX.x, normalX.y);
    // Y-axis projection (XZ plane): tangent=X, bitangent=Z, normal=Y
    vec3 worldNormalY = vec3(normalY.x, normalY.z, normalY.y);
    // Z-axis projection (XY plane): tangent=X, bitangent=Y, normal=Z
    vec3 worldNormalZ = vec3(normalZ.x, normalZ.y, normalZ.z);

    // Flip X-projection to match world-space orientation
    worldNormalX.x = -worldNormalX.x;

    // Blend world-space normals
    vec3 blendedNormal = worldNormalX * blendWeights.x +
                         worldNormalY * blendWeights.y +
                         worldNormalZ * blendWeights.z;

    TriplanarSample result;
    result.albedo = albedo;
    result.normal = blendedNormal;
    return result;
}

// General-purpose triplanar helpers for colors, grayscale and normals
void ComputeTriplanarUVs(vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness,
                         out vec3 blendWeights, out vec2 uvX, out vec2 uvY, out vec2 uvZ)
{
    blendWeights = abs(worldNormal);
    blendWeights = pow(blendWeights, vec3(blendSharpness));
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-6);

    uvX = worldPos.yz * scale;
    uvY = worldPos.xz * scale;
    uvZ = worldPos.xy * scale;
}

vec3 SampleTriplanarColor(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    vec3 bw; vec2 ux, uy, uz;
    ComputeTriplanarUVs(worldPos, worldNormal, scale, blendSharpness, bw, ux, uy, uz);
    vec3 cX = texture(tex, ux).rgb;
    vec3 cY = texture(tex, uy).rgb;
    vec3 cZ = texture(tex, uz).rgb;
    return cX * bw.x + cY * bw.y + cZ * bw.z;
}

float SampleTriplanarGray(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    vec3 bw; vec2 ux, uy, uz;
    ComputeTriplanarUVs(worldPos, worldNormal, scale, blendSharpness, bw, ux, uy, uz);
    float vX = texture(tex, ux).r;
    float vY = texture(tex, uy).r;
    float vZ = texture(tex, uz).r;
    return vX * bw.x + vY * bw.y + vZ * bw.z;
}

vec3 SampleTriplanarNormalMap(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness)
{
    // Similar to SampleTriplanar but handles normal map decoding and tangent->world for each projection
    vec3 bw; vec2 ux, uy, uz;
    ComputeTriplanarUVs(worldPos, worldNormal, scale, blendSharpness, bw, ux, uy, uz);

    vec3 nX = texture(tex, ux).xyz * 2.0 - 1.0;
    vec3 nY = texture(tex, uy).xyz * 2.0 - 1.0;
    vec3 nZ = texture(tex, uz).xyz * 2.0 - 1.0;

    nX.y = -nX.y; nY.y = -nY.y; nZ.y = -nZ.y;

    // Transform per-projection tangent-space normals to world-like space (matching SampleTriplanar)
    vec3 worldNormalX = vec3(nX.z, nX.x, nX.y);
    vec3 worldNormalY = vec3(nY.x, nY.z, nY.y);
    vec3 worldNormalZ = vec3(nZ.x, nZ.y, nZ.z);
    worldNormalX.x = -worldNormalX.x;

    vec3 blended = worldNormalX * bw.x + worldNormalY * bw.y + worldNormalZ * bw.z;
    return blended;
}

/// <summary>
/// Decode normal from normal map texture (tangent space)
/// </summary>
vec3 decodeWaterNormalMap(sampler2D normalTex, vec2 uv, float strength)
{
    vec3 normalMap = texture(normalTex, uv).rgb;
    normalMap = normalMap * 2.0 - 1.0; // Convert from [0,1] to [-1,1]
    normalMap.xy *= strength;
    normalMap.z = sqrt(max(0.0, 1.0 - dot(normalMap.xy, normalMap.xy)));
    return normalize(normalMap);
}

/// <summary>
/// Generate physically-based caustics using water surface normals
/// Simulates light refraction through animated water surface above this position
/// </summary>
vec3 generateCausticsFromNormals(vec2 worldPos, float time)
{
    // Sample animated water normals at this position (simulating water surface above this point)
    vec2 uv1 = worldPos * u_WaterCausticsScale + vec2(time * u_WaterCausticsSpeed * 0.3, time * u_WaterCausticsSpeed * 0.2);
    vec2 uv2 = worldPos * u_WaterCausticsScale * 1.3 - vec2(time * u_WaterCausticsSpeed * 0.4, -time * u_WaterCausticsSpeed * 0.3);

    // Sample normals from both layers (these represent the water surface)
    vec3 normal1 = decodeWaterNormalMap(u_WaterNormalTex, uv1, u_WaterNormalStrength);
    vec3 normal2 = decodeWaterNormalMap(u_WaterNormalTex2, uv2, u_WaterNormalStrength2);
    vec3 waterNormal = normalize(mix(normal1, normal2, u_WaterNormalBlend));

    // Calculate light refraction direction based on water normal
    // Snell's law simplification: water normal tilts refract light rays
    vec2 refractOffset = waterNormal.xy * u_WaterCausticsDistortion;

    // === CHROMATIC ABERRATION ===
    // Different wavelengths refract differently (RGB split)
    float split = u_WaterCausticsSplit;
    vec2 offsetR = refractOffset * (1.0 + split);  // Red refracts less
    vec2 offsetG = refractOffset;                   // Green (middle wavelength)
    vec2 offsetB = refractOffset * (1.0 - split);  // Blue refracts more

    // === CALCULATE DIVERGENCE FOR CAUSTICS INTENSITY ===
    // Areas where light rays converge = bright caustics
    float epsilon = 0.02;

    // Sample neighboring normals for RED channel
    vec3 nR_x = decodeWaterNormalMap(u_WaterNormalTex, uv1 + vec2(epsilon, 0), u_WaterNormalStrength);
    vec3 nR_y = decodeWaterNormalMap(u_WaterNormalTex, uv1 + vec2(0, epsilon), u_WaterNormalStrength);

    // Sample neighboring normals for GREEN channel
    vec3 nG_x = decodeWaterNormalMap(u_WaterNormalTex, uv2 + vec2(epsilon, 0), u_WaterNormalStrength);
    vec3 nG_y = decodeWaterNormalMap(u_WaterNormalTex, uv2 + vec2(0, epsilon), u_WaterNormalStrength);

    // Calculate divergence (gradient magnitude) for each color channel
    float divR = length(normal1 - nR_x) + length(normal1 - nR_y);
    float divG = length(waterNormal - nG_x) + length(waterNormal - nG_y);
    float divB = length(normal2 - nG_x) + length(normal2 - nG_y);

    // Convert divergence to caustics brightness
    // High divergence = light rays converging = bright spot
    vec3 caustics;
    caustics.r = pow(saturate(divR * 15.0), 2.5);
    caustics.g = pow(saturate(divG * 15.0), 2.5);
    caustics.b = pow(saturate(divB * 15.0), 2.5);

    // Add high-frequency detail to make caustics look more realistic
    float detail = sin(uv1.x * 10.0 + time) * sin(uv1.y * 10.0 - time * 0.7) * 0.1 + 0.9;
    caustics *= detail;

    return caustics * u_WaterCausticsColor;
}

void main(){
    // Alpha clipping - early discard for performance (before expensive PBR calculations)
    // CRITICAL: This must happen BEFORE any other sampling for best performance
    if (u_AlphaClippingEnabled == 1) {
        // Sample alpha from albedo texture
        vec4 albedoSample;
        if (u_UseTriplanar == 1) {
            // Triplanar sampling for RGBA
            vec3 bw = abs(normalize(vNormal));
            bw = pow(bw, vec3(u_TriplanarBlendSharpness));
            bw /= (bw.x + bw.y + bw.z);
            vec2 uvX = vWorldPos.yz * u_TriplanarScale;
            vec2 uvY = vWorldPos.xz * u_TriplanarScale;
            vec2 uvZ = vWorldPos.xy * u_TriplanarScale;
            vec4 sampX = texture(u_AlbedoTex, uvX);
            vec4 sampY = texture(u_AlbedoTex, uvY);
            vec4 sampZ = texture(u_AlbedoTex, uvZ);
            albedoSample = sampX * bw.x + sampY * bw.y + sampZ * bw.z;
        } else {
            albedoSample = texture(u_AlbedoTex, vUV);
        }
        
        // Extract alpha channel from texture
        float textureAlpha = albedoSample.a;
        
        // Discard fragment if below threshold (don't multiply by u_AlbedoColor.a - that's for blending)
        if (textureAlpha < u_AlphaClipThreshold) {
            discard;
        }
    }
    
    // Handle triplanar mapping if enabled
    vec3 baseNormal = normalize(vNormal);
    vec3 sampledAlbedo;
    vec3 sampledNormal;
    vec2 effectiveUV;

    // PERFORMANCE: Use uniform flags instead of textureSize() calls (saves ~200 GPU cycles per fragment)
    bool hasMetallicRoughnessTex = u_HasMetallicRoughnessTex != 0;
    bool hasMetallicTex = !hasMetallicRoughnessTex && u_HasMetallicTex != 0;
    bool hasRoughnessTex = !hasMetallicRoughnessTex && u_HasRoughnessTex != 0;
    bool hasOcclusionTex = u_HasOcclusionTex != 0;
    bool hasEmissiveTex = u_HasEmissiveTex != 0;
    bool hasHeightTex = u_HasHeightTex != 0;
    bool hasDetailMask = u_HasDetailMask != 0;
    bool hasDetailAlbedo = u_HasDetailAlbedo != 0;
    bool hasDetailNormal = u_HasDetailNormal != 0;

    if (u_UseTriplanar == 1)
    {
        // Use triplanar mapping - world-space projection for main maps
        sampledAlbedo = SampleTriplanarColor(u_AlbedoTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness);
        sampledNormal = SampleTriplanarNormalMap(u_NormalTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness) * u_NormalStrength;
        effectiveUV = vUV; // kept for non-triplanar fallbacks / detail blending

        // Sample PBR scalar/combined textures via triplanar when present
        // Metallic / Roughness
        if (hasMetallicRoughnessTex) {
            vec3 mr = SampleTriplanarColor(u_MetallicRoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness);
            // GLTF convention: G = roughness, B = metallic
            // Store temporarily in user-defined variables by writing to effectiveUV.x/y? We'll set later when computing material properties.
            // To avoid scope issues, we'll set globals via local variables below where needed.
        }

        // Occlusion and height and emissive / detail textures can also be sampled triplanar
        // We'll sample them lazily later when used (e.g., AO applied to ambient, emissive added at end)
    }
    else
    {
        // Standard UV mapping
        sampledAlbedo = texture(u_AlbedoTex, vUV).rgb;
        sampledNormal = sampleNormalMap(u_NormalTex, vUV, u_NormalStrength, baseNormal);
        effectiveUV = vUV;
    }

    // Create material properties manually to use triplanar-sampled albedo/normal
    MaterialProperties material;
    material.baseColor = sampledAlbedo * u_AlbedoColor.rgb;
    material.normal = (u_UseTriplanar == 1) ? normalize(baseNormal + sampledNormal * 0.1) : sampledNormal;

    // Sample metallic and roughness from textures if available (always use UV for these)
    float metallic = u_Metallic;
    float roughness = smoothnessToRoughness(u_Smoothness);
    // If triplanar is enabled, prefer triplanar sampling for scalar/combined textures
    // PERFORMANCE: Use uniform flags instead of textureSize() calls
    if (hasMetallicRoughnessTex) {
        vec3 metallicRoughness = (u_UseTriplanar == 1) ? SampleTriplanarColor(u_MetallicRoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                                           : texture(u_MetallicRoughnessTex, effectiveUV).rgb;
        roughness = metallicRoughness.g;
        metallic = metallicRoughness.b;
    } else {
        if (hasMetallicTex) {
            metallic = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_MetallicTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                             : texture(u_MetallicTex, effectiveUV).r;
        }
        if (hasRoughnessTex) {
            roughness = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_RoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                               : texture(u_RoughnessTex, effectiveUV).r;
        } else {
            roughness = smoothnessToRoughness(u_Smoothness);
        }
    }

    // Optional detail albedo overlay (if provided)
    if (hasDetailMask && hasDetailAlbedo) {
        float mask = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_DetailMaskTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                           : texture(u_DetailMaskTex, effectiveUV).r;
        vec3 detailCol = (u_UseTriplanar == 1) ? SampleTriplanarColor(u_DetailAlbedoTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                               : texture(u_DetailAlbedoTex, effectiveUV).rgb;
        material.baseColor = mix(material.baseColor, detailCol, clamp(mask, 0.0, 1.0));
    }

    // Optional detail normal blending
    if (hasDetailNormal) {
        vec3 detailN = (u_UseTriplanar == 1) ? SampleTriplanarNormalMap(u_DetailNormalTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                             : sampleNormalMap(u_DetailNormalTex, effectiveUV, 1.0, baseNormal);
        // Add subtle detail normal contribution
        material.normal = normalize(material.normal + detailN * 0.5);
    }

    material.roughness = saturate(roughness);
    material.metallic = saturate(metallic);

    // === WEATHER EFFECTS ===
    // === ENHANCED SNOW SYSTEM ===
    if (u_SnowAccumulation > 0.0)
    {
        // Calculate snow placement using OBJECT SPACE normal (vObjectNormal from vertex shader)
        // This bases snow on the model's local geometry, not its world orientation
        // Result: snow "sticks" to the same surface areas regardless of model rotation
        // In object space, "up" is (0,1,0) relative to the model's local coordinates
        vec3 objectNormal = normalize(vObjectNormal);
        vec3 objectUp = vec3(0, 1, 0);
        
        float dotProduct = dot(objectNormal, objectUp);
        float angleRad = acos(clamp(dotProduct, -1.0, 1.0));
        float angleDeg = degrees(angleRad);
        
        // Calculate snow placement based on surface angle in object space
        float slopeMin = u_SnowSlopeMin;
        float slopeMax = u_SnowSlopeMax;
        float fadeRange = 20.0; // 20° fade for smooth transitions
        
        float fadeIn = smoothstep(slopeMin, slopeMin + fadeRange, angleDeg);
        float fadeOut = 1.0 - smoothstep(slopeMax - fadeRange, slopeMax, angleDeg);
        float snowPlacement = clamp(fadeIn * fadeOut, 0.0, 1.0);

        // Final snow amount = accumulation * placement (NOT clamped - can exceed 1.0)
        float snowAmount = u_SnowAccumulation * snowPlacement;

        if (snowAmount > 0.01)
        {
            // Sample snow material textures with tiling
            vec2 snowUV = vWorldPos.xz * u_SnowTextureTiling;
            vec3 snowAlbedo = texture(u_SnowAlbedoTex, snowUV).rgb * u_SnowAlbedoColor.rgb;
            vec3 snowNormalMap = texture(u_SnowNormalTex, snowUV).rgb * 2.0 - 1.0; // Unpack normal map
            vec2 snowMetallicRoughness = texture(u_SnowMetallicRoughnessTex, snowUV).rg;

            float snowMetallic = snowMetallicRoughness.r * u_SnowMetallic;
            float snowRoughness = snowMetallicRoughness.g * u_SnowRoughness;

            // Blend snow normal with surface normal using proper normal blending
            // Scale by NormalStrength parameter for artist control
            vec3 snowNormal = normalize(material.normal + snowNormalMap * u_SnowNormalStrength);

            // Calculate sparkle effect (more sparkle with thick snow)
            vec3 V = normalize(uCameraPos - vWorldPos);
            float sparkle = calculateSnowSparkle(vWorldPos, snowNormal, V, u_SnowSparkle);
            sparkle *= min(snowAmount, 1.0); // Sparkle saturates at accumulation = 1.0

            // Add sparkle to snow albedo (brightens snow based on viewing angle)
            snowAlbedo += vec3(sparkle * 0.5);

            // Soft saturation for blending (accumulation can exceed 1.0 but blend saturates smoothly)
            // Use a curve that reaches near-white at high accumulation
            float blendFactor = 1.0 - exp(-snowAmount * 1.5); // Exponential saturation

            // Blend snow with underlying surface
            material.baseColor = mix(material.baseColor, snowAlbedo, blendFactor);
            material.normal = mix(material.normal, snowNormal, blendFactor);
            material.roughness = mix(material.roughness, snowRoughness, blendFactor);
            material.metallic = mix(material.metallic, snowMetallic, blendFactor);
        }
    }

    // Rain wetness (makes surfaces darker and more reflective)
    if (u_Wetness > 0.0) {
        // Darken surfaces when wet
        float darken = 1.0 - (u_Wetness * 0.2);
        material.baseColor *= darken;
        // Increase smoothness (reduce roughness) when wet
        material.roughness = mix(material.roughness, material.roughness * 0.5, u_Wetness);
    }

    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);

    // Debug overrides: let caller visualize albedo or normal sampling directly
    if (u_DebugShowAlbedo != 0) {
        outColor = vec4(material.baseColor, 1.0);
        outId = u_ObjectId;
        return;
    }

    if (u_DebugShowNormals != 0) {
        // visualize normal in 0..1 range
        vec3 nvis = normalize(material.normal) * 0.5 + 0.5;
        outColor = vec4(nvis, 1.0);
        outId = u_ObjectId;
        return;
    }

    if (u_DebugShowAO != 0 && hasOcclusionTex) {
        // visualize AO texture as grayscale
        float ao = texture(u_OcclusionTex, vUV).r;
        outColor = vec4(ao, ao, ao, 1.0);
        outId = u_ObjectId;
        return;
    }

    // Calculate lighting
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 N = material.normal;

    // Accumulate lighting contributions
    vec3 Lo = vec3(0.0);

    // Directional light
    // Directional light with simple shadow mapping
    vec3 dirLighting = calculateDirectionalLight(N, V, material);

    // Calculate shadow factor with CSM support (viewPos = worldPos - cameraPos for distance calc)
    vec3 viewPos = vWorldPos - uCameraPos;
    // Compute light vector for bias calculation
    vec3 L = normalize(-uDirLightDirection);
    float shadowFactor = calculateShadowWithNL(vWorldPos, viewPos, N, L);

    // Note: Shadow debug visualization blends later so normal shading remains visible.
    
    Lo += dirLighting * shadowFactor;

    // Point lights
    Lo += calculatePointLights(vWorldPos, N, V, material);

    // Spot lights
    Lo += calculateSpotLights(vWorldPos, N, V, material);

    // Ambient lighting with SSAO and AO texture
    vec3 ambient;
    if (u_TransparencyMode == 0) {
        // Opaque materials: apply SSAO
        ambient = calculateAmbientLightingWithSSAO(material, vWorldPos, gl_FragCoord.xy, u_ScreenSize,
                                                   u_SSAOTexture, u_SSAOEnabled, u_SSAOStrength);

        // Apply baked ambient occlusion texture (only if a real texture is bound, not placeholder)
        if (hasOcclusionTex) {
            float ao = (u_UseTriplanar == 1) ? SampleTriplanarGray(u_OcclusionTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                              : texture(u_OcclusionTex, vUV).r;
            ambient *= mix(1.0, ao, u_OcclusionStrength);
        }
    } else {
        // Transparent materials: no SSAO
        ambient = calculateAmbientLighting(material, vWorldPos);
    }

    // CRITICAL FIX: Apply shadows to ambient IBL too!
    // Shadowed areas receive less indirect light from the sky
    // mix(0.3, 1.0, shadowFactor) means: 30% ambient in full shadow, 100% in full light
    ambient *= mix(0.3, 1.0, shadowFactor);

    // DEBUG: Show SSAO texture as grayscale for testing (DISABLED)
    // if (u_SSAOEnabled != 0 && u_TransparencyMode == 0) {
    //     vec2 ssaoUV = gl_FragCoord.xy / u_ScreenSize;
    //     float ssaoValue = texture(u_SSAOTexture, ssaoUV).r;
    //     // Show SSAO texture directly as grayscale for debugging
    //     outColor = vec4(ssaoValue, ssaoValue, ssaoValue, 1.0);
    //     outId = u_ObjectId;
    //     return;
    // }

    vec3 color = ambient + Lo;

    // Apply water caustics if enabled (for objects below water surface)
    if (u_WaterCausticsEnabled == 1) {
        vec3 caustics = generateCausticsFromNormals(vWorldPos.xz, u_Time);
        color += caustics * u_WaterCausticsStrength;
    }

    // Apply fog
    color = processFog(color, vWorldPos);

    // Apply stylization effects
    color = adjustSaturation(color, u_Saturation);
    color = adjustBrightness(color, u_Brightness);
    color = adjustContrast(color, u_Contrast);
    color = adjustHue(color, u_Hue);

    // Add emissive (texture-based with color tint + emission strength)
    // PERFORMANCE: Use uniform flag instead of textureSize() call
    vec3 emissiveTex = vec3(0.0);
    if (hasEmissiveTex) {
        emissiveTex = (u_UseTriplanar == 1) ? SampleTriplanarColor(u_EmissiveTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                                            : texture(u_EmissiveTex, vUV).rgb;
    }
    vec3 emissive = emissiveTex * u_EmissiveColor * u_Emission;
    color += emissive;

    // Apply CSM cascade debug visualization if enabled
    float viewSpaceZ = -(u_ViewMatrix * vec4(vWorldPos, 1.0)).z;
    color = ApplyCascadeDebug(color, viewSpaceZ);

    // Shadows now working correctly - no debug visualization needed!

    // Handle transparency
    float outAlpha = 1.0;
    if (u_TransparencyMode != 0) {
        // If an albedo texture is present, use its alpha channel multiplied by the albedo color alpha.
        float texAlpha = texture(u_AlbedoTex, vUV).a;
        outAlpha = saturate(texAlpha * u_AlbedoColor.a);
    }

    // If shadow debug visualization is enabled, blend a diagnostic color on top
    // of the lit result so the scene is still visible while we inspect sampling.
    if (u_DebugShowShadows != 0) {
        vec4 lightSpacePos = u_ShadowMatrix * vec4(vWorldPos, 1.0);
        vec3 lp = lightSpacePos.xyz / lightSpacePos.w;
        vec3 projCoords = lp * 0.5 + 0.5; // NDC -> [0,1]
        // For sampler2DShadow, we must provide vec3(uv, compareDepth); the texture() call returns comparison result (0 or 1).
        // Since we want the stored depth value for debug vis, we read .r from a manual textureLod or use a texelFetch.
        // However, sampler2DShadow doesn't support .r access. As a workaround for debug vis, just use projCoords.z as proxy.
        float sampledDepth = projCoords.z; // Approximate visualization (cannot read raw depth from sampler2DShadow)

        bool inside = projCoords.x >= 0.0 && projCoords.x <= 1.0 &&
                      projCoords.y >= 0.0 && projCoords.y <= 1.0 &&
                      projCoords.z >= 0.0 && projCoords.z <= 1.0;

        vec3 debugRgb;
        if (inside) {
            debugRgb = vec3(sampledDepth, projCoords.z, 1.0 - projCoords.z);
        } else {
            debugRgb = vec3(0.20, 0.20, 0.20);
        }

        // Only blend when inside the shadow map projection and use a subtle tint
        // so the underlying shading remains visible. Adjust dbgFactor for
        // stronger/weaker visualization.
        float dbgFactor = 0.12; // subtle by default
        if (inside) {
            color = mix(color, debugRgb, dbgFactor);
        }
    }

    // === LOD TRANSITION DITHERING (Unreal-style) ===
    // Apply dithered alpha test for smooth LOD transitions
    // Uses Bayer matrix pattern for temporal stability
    // NOTE: Only apply dithering when u_LodTransition is explicitly < 1.0 AND > 0.0
    // Default value of 0.0 means "not set" - treat as fully opaque (no dithering)
    if (u_LodTransition > 0.0 && u_LodTransition < 1.0) {
        vec2 screenUV = gl_FragCoord.xy;

        // 4x4 Bayer matrix dithering (used by Unreal/Unity for LOD transitions)
        float dither4x4[16] = float[](
            0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
            12.0/16.0, 4.0/16.0, 14.0/16.0,  6.0/16.0,
            3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
            15.0/16.0, 7.0/16.0, 13.0/16.0,  5.0/16.0
        );

        int x = int(mod(screenUV.x, 4.0));
        int y = int(mod(screenUV.y, 4.0));
        float ditherThreshold = dither4x4[y * 4 + x];

        // Discard fragments based on transition factor
        // transition=0 -> all discarded, transition=1 -> none discarded
        if (u_LodTransition < ditherThreshold) {
            discard;
        }
    }

    outColor = vec4(color, outAlpha);
    outId    = u_ObjectId;
}
