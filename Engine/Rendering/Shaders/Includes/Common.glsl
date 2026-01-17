// Common.glsl - Common constants, uniforms, structures and utility functions

// Mathematical constants
#define PI 3.14159265359
#define EPSILON 1e-6

// Global uniform block (shared across all shaders)
// IMPORTANT: binding=0 ensures this UBO is bound to binding point 0
// NOTE: binding qualifier requires GLSL 4.20+ or GL_ARB_shading_language_420pack
layout(std140, binding=0) uniform Global {
    // === CAMERA & TRANSFORMS ===
    mat4 uView; mat4 uProj; mat4 uViewProj;
    vec3 uCameraPos; float _pad1;

    // === DIRECTIONAL LIGHT (Sun/Moon) ===
    vec3 uDirLightDirection; float _pad2;
    vec3 uDirLightColor; float uDirLightIntensity;

    // === POINT LIGHTS ===
    int uPointLightCount; float _pad3; float _pad4; float _pad5;
    vec4 uPointLightPos0; vec4 uPointLightColor0;
    vec4 uPointLightPos1; vec4 uPointLightColor1;
    vec4 uPointLightPos2; vec4 uPointLightColor2;
    vec4 uPointLightPos3; vec4 uPointLightColor3;

    // === SPOT LIGHTS ===
    int uSpotLightCount; float _pad6; float _pad7; float _pad8;
    vec4 uSpotLightPos0; vec4 uSpotLightDir0; vec4 uSpotLightColor0; float uSpotLightAngle0; float uSpotLightInnerAngle0; float _pad9; float _pad10;
    vec4 uSpotLightPos1; vec4 uSpotLightDir1; vec4 uSpotLightColor1; float uSpotLightAngle1; float uSpotLightInnerAngle1; float _pad11; float _pad12;

    // === AMBIENT & SKYBOX ===
    vec3 uAmbientColor; float uAmbientIntensity;
    vec3 uSkyboxTint; float uSkyboxExposure;

    // === FOG (from WeatherComponent) ===
    int uFogEnabled; float uFogDensity; float uFogOpacity; float uFogNoiseScale;
    vec3 uFogColor; float uFogStart;
    float uFogEnd; float uFogNoiseSpeed; float uFogLayerHeight; float uFogThickness;
    int uFogFBMOctaves; float uFogFBMLacunarity; float uFogFBMGain; float uFogScattering;
    int uFogColorMode; float _fogpad1; float _fogpad2; float _fogpad3; // 0=Custom, 1=Ambient, 2=Skybox, 3=IBL

    // === CLIP PLANE ===
    float uClipPlaneEnabled; float _pad16; float _pad17; float _pad18;
    vec4 uClipPlane; // plane equation: normal.xyz, d

    // === TIME & WEATHER SYSTEM ===

    // Time
    float uTime;            // Game time for animations (seconds since start)
    float uTimeOfDay;       // 0-24 hours (12.0 = noon)
    float uDayNightBlend;   // 0 = night, 1 = day (for convenience)
    float uGoldenHourBlend; // 0 = no golden hour, 1 = peak golden hour

    // Wind parameters (for vegetation, water, particles)
    vec2 uWindDirection;    // XZ normalized direction
    float uWindStrength;    // 0-1 overall intensity
    float uWindSpeed;       // Animation speed multiplier
    float uWindGustiness;   // 0-1 turbulence factor
    float _pad19; float _pad20; float _pad21; // Padding to align next vec4

    // Advanced wind (vegetation specific)
    float uBranchAmplitude; // Branch sway amplitude
    float uBranchSpeed;     // Branch oscillation speed
    float uBranchTurbulence;// Branch noise intensity
    float uTrunkStiffness;  // Trunk rigidity (0=flexible, 1=rigid)
    float uTrunkBendAmount; // How much trunk bends
    float uLeafFlutter;     // Leaf flutter intensity
    float uLeafFlutterSpeed;// Leaf flutter speed
    float _pad22;           // Padding

    // Precipitation
    float uRainIntensity;   // 0-1 rain intensity
    float uSnowAccumulation;// 0+ accumulated snow depth
    float uSnowIntensity;   // 0-1 snowfall rate
    float uWetness;         // 0-1 surface wetness

    // Snow parameters
    float uSnowSlopeMin;    // Min slope for snow (degrees)
    float uSnowSlopeMax;    // Max slope for snow (degrees)
    float uSnowSparkle;     // Snow sparkle/glitter intensity
    float uSnowDisplacement;// Snow height displacement (world units)
};

// Runtime debug: flip normal Y component for imported normal maps / conventions
uniform int u_FlipNormalY; // 0 = no, 1 = flip

// Material properties structure
struct MaterialProperties {
    vec3 baseColor;
    float roughness;
    float metallic;
    vec3 F0;
    vec3 normal;
};

// SSAO uniforms (optional, only used when SSAO is enabled)
// Note: These uniforms are always set by the renderer, even when SSAO is disabled
// u_SSAOEnabled = 0 when disabled, 1 when enabled

// Utility functions
float saturate(float x) {
    return clamp(x, 0.0, 1.0);
}

vec3 saturate(vec3 x) {
    return clamp(x, 0.0, 1.0);
}

// Sample and process normal map
vec3 sampleNormalMap(sampler2D normalTex, vec2 uv, float strength, vec3 worldNormal) {
    vec3 normalMap = texture(normalTex, uv).rgb * 2.0 - 1.0;
    // Respect runtime flip flag (DX vs GL) - only flip when requested
    if (u_FlipNormalY == 1) normalMap.y = -normalMap.y;
    normalMap.xy *= strength;

    // Simple normal blending - match PBR shader behavior
    vec3 baseNormal = normalize(worldNormal);
    // Use same factor as PBR shader (0.1) for consistent specular highlights
    return normalize(baseNormal + normalMap * 0.1);
}

// Convert smoothness to roughness
float smoothnessToRoughness(float smoothness) {
    return clamp(1.0 - smoothness, 0.01, 0.99);
}

// Setup material properties from uniforms and textures
MaterialProperties setupMaterialProperties(sampler2D albedoTex, sampler2D normalTex, vec2 uv,
                                          vec4 albedoColor, float normalStrength,
                                          float metallic, float smoothness, vec3 worldNormal) {
    MaterialProperties material;

    // Sample albedo texture
    material.baseColor = texture(albedoTex, uv).rgb * albedoColor.rgb;
    
    // Use uniform values for metallic/roughness (texture sampling will be added in ForwardBase.frag)
    material.roughness = smoothnessToRoughness(saturate(smoothness));
    material.metallic = saturate(metallic);
    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);
    material.normal = sampleNormalMap(normalTex, uv, normalStrength, worldNormal);

    return material;
}

// Enhanced setup with PBR texture support
MaterialProperties setupMaterialPropertiesPBR(
    sampler2D albedoTex, sampler2D normalTex, vec2 uv,
    vec4 albedoColor, float normalStrength,
    float metallicParam, float smoothnessParam, vec3 worldNormal,
    sampler2D metallicTex, sampler2D roughnessTex, sampler2D metallicRoughnessTex,
    bool useMetallicTex, bool useRoughnessTex, bool useMetallicRoughnessTex) {
    
    MaterialProperties material;

    // Sample albedo
    material.baseColor = texture(albedoTex, uv).rgb * albedoColor.rgb;
    
    // Sample metallic and roughness from textures if available
    float metallic = metallicParam;
    float roughness = smoothnessToRoughness(smoothnessParam);
    
    if (useMetallicRoughnessTex) {
        // GLTF 2.0 combined texture: G=roughness, B=metallic
        vec3 metallicRoughness = texture(metallicRoughnessTex, uv).rgb;
        roughness = metallicRoughness.g;  // Green channel = roughness
        metallic = metallicRoughness.b;   // Blue channel = metallic
    } else {
        // Separate textures
        if (useMetallicTex) {
            metallic = texture(metallicTex, uv).r;  // Red channel = metallic
        }
        if (useRoughnessTex) {
            roughness = texture(roughnessTex, uv).r;  // Red channel = roughness
        } else {
            // If no roughness texture, use smoothness parameter
            roughness = smoothnessToRoughness(smoothnessParam);
        }
    }
    
    material.roughness = saturate(roughness);
    material.metallic = saturate(metallic);
    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);
    material.normal = sampleNormalMap(normalTex, uv, normalStrength, worldNormal);

    return material;
}
