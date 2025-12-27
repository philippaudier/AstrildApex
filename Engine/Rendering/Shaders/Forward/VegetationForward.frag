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
in vec2 vUV;
in float vWindFactor;
in vec3 vInstancePos;
in float vDistanceToCamera;

// === BASE TEXTURES ===
uniform sampler2D u_AlbedoTex;
uniform sampler2D u_NormalTex;

// === PBR TEXTURES ===
uniform sampler2D u_EmissiveTex;
uniform sampler2D u_MetallicTex;
uniform sampler2D u_RoughnessTex;
uniform sampler2D u_MetallicRoughnessTex;
uniform sampler2D u_OcclusionTex;

// === MATERIAL PARAMETERS ===
uniform vec4  u_AlbedoColor;
uniform float u_NormalStrength;
uniform uint  u_ObjectId;

// === PBR PARAMETERS ===
uniform float u_Metallic;
uniform float u_Smoothness;
uniform float u_OcclusionStrength;
uniform vec3  u_EmissiveColor;

// Texture tiling and offset
uniform vec2 u_TextureTiling;
uniform vec2 u_TextureOffset;

// Transparency mode
uniform int u_TransparencyMode;

// Triplanar mapping settings
uniform int u_UseTriplanar;
uniform float u_TriplanarScale;
uniform float u_TriplanarBlendSharpness;

// Stylization parameters
uniform float u_Saturation;
uniform float u_Brightness;
uniform float u_Contrast;
uniform float u_Hue;
uniform float u_Emission;

// Alpha clipping
uniform int u_AlphaClippingEnabled;
uniform float u_AlphaClipThreshold;

// Weather parameters
uniform float u_RainIntensity;
uniform float u_SnowAccumulation;  // Accumulated snow (can exceed 1.0)
uniform float u_SnowIntensity;     // Snowfall rate (for future use)
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
uniform float u_SnowNormalStrength; // Normal map strength for snow

// LOD & Distance Culling removed: vegetation is always rendered by shader.

// === SNOW UTILITY FUNCTIONS ===
// Calculate snow placement factor based on surface normal and slope constraints
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

// === STYLIZATION UTILITY FUNCTIONS ===
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
    vec3 hsv = rgb2hsv(color);
    hsv.x = fract(hsv.x + hue * 0.5);
    return hsv2rgb(hsv);
}

// === TRIPLANAR SAMPLING ===
vec3 SampleTriplanarColor(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness) {
    vec3 blendWeights = abs(worldNormal);
    blendWeights = pow(blendWeights, vec3(blendSharpness));
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-6);
    
    vec2 uvX = worldPos.yz * scale;
    vec2 uvY = worldPos.xz * scale;
    vec2 uvZ = worldPos.xy * scale;
    
    vec3 cX = texture(tex, uvX).rgb;
    vec3 cY = texture(tex, uvY).rgb;
    vec3 cZ = texture(tex, uvZ).rgb;
    
    return cX * blendWeights.x + cY * blendWeights.y + cZ * blendWeights.z;
}

vec4 SampleTriplanarRGBA(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness) {
    vec3 blendWeights = abs(worldNormal);
    blendWeights = pow(blendWeights, vec3(blendSharpness));
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-6);
    
    vec2 uvX = worldPos.yz * scale;
    vec2 uvY = worldPos.xz * scale;
    vec2 uvZ = worldPos.xy * scale;
    
    vec4 cX = texture(tex, uvX);
    vec4 cY = texture(tex, uvY);
    vec4 cZ = texture(tex, uvZ);
    
    return cX * blendWeights.x + cY * blendWeights.y + cZ * blendWeights.z;
}

float SampleTriplanarGray(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness) {
    vec3 blendWeights = abs(worldNormal);
    blendWeights = pow(blendWeights, vec3(blendSharpness));
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-6);
    
    vec2 uvX = worldPos.yz * scale;
    vec2 uvY = worldPos.xz * scale;
    vec2 uvZ = worldPos.xy * scale;
    
    float vX = texture(tex, uvX).r;
    float vY = texture(tex, uvY).r;
    float vZ = texture(tex, uvZ).r;
    
    return vX * blendWeights.x + vY * blendWeights.y + vZ * blendWeights.z;
}

vec3 SampleTriplanarNormalMap(sampler2D tex, vec3 worldPos, vec3 worldNormal, float scale, float blendSharpness) {
    vec3 bw = abs(worldNormal);
    bw = pow(bw, vec3(blendSharpness));
    bw /= (bw.x + bw.y + bw.z + 1e-6);

    vec2 ux = worldPos.yz * scale;
    vec2 uy = worldPos.xz * scale;
    vec2 uz = worldPos.xy * scale;

    vec3 nX = texture(tex, ux).xyz * 2.0 - 1.0;
    vec3 nY = texture(tex, uy).xyz * 2.0 - 1.0;
    vec3 nZ = texture(tex, uz).xyz * 2.0 - 1.0;

    nX.y = -nX.y; 
    nY.y = -nY.y; 
    nZ.y = -nZ.y;

    vec3 worldNormalX = vec3(nX.z, nX.x, nX.y);
    vec3 worldNormalY = vec3(nY.x, nY.z, nY.y);
    vec3 worldNormalZ = vec3(nZ.x, nZ.y, nZ.z);
    worldNormalX.x = -worldNormalX.x;

    vec3 blended = worldNormalX * bw.x + worldNormalY * bw.y + worldNormalZ * bw.z;
    return blended;
}

void main() {
    // Alpha clipping - early discard for performance (EXACTLY like ForwardBase)
    if (u_AlphaClippingEnabled == 1) {
        vec4 albedoSample;
        if (u_UseTriplanar == 1) {
            albedoSample = SampleTriplanarRGBA(u_AlbedoTex, vWorldPos, normalize(vNormal), u_TriplanarScale, u_TriplanarBlendSharpness);
        } else {
            albedoSample = texture(u_AlbedoTex, vUV);  // vUV already has tiling/offset from vertex shader
        }

        float textureAlpha = albedoSample.a;
        if (textureAlpha < u_AlphaClipThreshold) {
            discard;
        }
    }
    
    // Handle triplanar mapping if enabled (EXACTLY like ForwardBase)
    vec3 baseNormal = normalize(vNormal);
    vec4 sampledAlbedoAlpha;
    vec3 sampledNormal;
    vec2 effectiveUV;

    // Detect which optional textures are present
    bool hasMetallicRoughnessTex = textureSize(u_MetallicRoughnessTex, 0) != ivec2(1, 1);
    bool hasMetallicTex = !hasMetallicRoughnessTex && textureSize(u_MetallicTex, 0) != ivec2(1, 1);
    bool hasRoughnessTex = !hasMetallicRoughnessTex && textureSize(u_RoughnessTex, 0) != ivec2(1, 1);
    bool hasOcclusionTex = textureSize(u_OcclusionTex, 0) != ivec2(1, 1);
    bool hasEmissiveTex = textureSize(u_EmissiveTex, 0) != ivec2(1, 1);

    if (u_UseTriplanar == 1) {
        sampledAlbedoAlpha = SampleTriplanarRGBA(u_AlbedoTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness);
        sampledNormal = SampleTriplanarNormalMap(u_NormalTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness) * u_NormalStrength;
        effectiveUV = vUV;
    } else {
        sampledAlbedoAlpha = texture(u_AlbedoTex, vUV);  // vUV already has tiling/offset from vertex shader
        sampledNormal = sampleNormalMap(u_NormalTex, vUV, u_NormalStrength, baseNormal);
        effectiveUV = vUV;
    }

    // Create material properties manually
    MaterialProperties material;
    material.baseColor = sampledAlbedoAlpha.rgb * u_AlbedoColor.rgb;
    material.normal = (u_UseTriplanar == 1) ? normalize(baseNormal + sampledNormal * 0.1) : sampledNormal;

    // Sample metallic and roughness (EXACTLY like ForwardBase)
    float metallic = u_Metallic;
    float roughness = smoothnessToRoughness(u_Smoothness);
    
    if (textureSize(u_MetallicRoughnessTex, 0) != ivec2(1, 1)) {
        vec3 metallicRoughness = (u_UseTriplanar == 1) 
            ? SampleTriplanarColor(u_MetallicRoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
            : texture(u_MetallicRoughnessTex, effectiveUV).rgb;
        roughness = metallicRoughness.g;
        metallic = metallicRoughness.b;
    } else {
        if (textureSize(u_MetallicTex, 0) != ivec2(1, 1)) {
            metallic = (u_UseTriplanar == 1) 
                ? SampleTriplanarGray(u_MetallicTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                : texture(u_MetallicTex, effectiveUV).r;
        }
        if (textureSize(u_RoughnessTex, 0) != ivec2(1, 1)) {
            roughness = (u_UseTriplanar == 1) 
                ? SampleTriplanarGray(u_RoughnessTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                : texture(u_RoughnessTex, effectiveUV).r;
        } else {
            roughness = smoothnessToRoughness(u_Smoothness);
        }
    }

    material.roughness = saturate(roughness);
    material.metallic = saturate(metallic);

    // === WEATHER EFFECTS ===
    // === ENHANCED SNOW SYSTEM ===
    if (u_SnowAccumulation > 0.0)
    {
        // Calculate snow placement based on surface angle using advanced slope controls
        float snowPlacement = calculateSnowPlacement(material.normal, u_SnowSlopeMin, u_SnowSlopeMax);

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

            // Blend snow normal with surface normal for realistic bumps
            // Use material's NormalStrength parameter to control intensity
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

    if (u_Wetness > 0.0) {
        float darken = 1.0 - (u_Wetness * 0.2);
        material.baseColor *= darken;
        material.roughness = mix(material.roughness, material.roughness * 0.5, u_Wetness);
    }

    material.F0 = mix(vec3(0.04), material.baseColor, material.metallic);

    // Calculate lighting (USING LIGHTING.GLSL FUNCTIONS LIKE FORWARDBASE)
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 N = material.normal;

    // Accumulate lighting contributions
    vec3 Lo = vec3(0.0);

    // Directional light with shadows
    vec3 dirLighting = calculateDirectionalLight(N, V, material);
    vec3 viewPos = vWorldPos - uCameraPos;
    vec3 L = normalize(-uDirLightDirection);
    float shadowFactor = calculateShadowWithNL(vWorldPos, viewPos, N, L);
    Lo += dirLighting * shadowFactor;

    // Point lights
    Lo += calculatePointLights(vWorldPos, N, V, material);

    // Spot lights
    Lo += calculateSpotLights(vWorldPos, N, V, material);

    // Ambient lighting
    vec3 ambient;
    if (u_TransparencyMode == 0) {
        // Opaque materials: use calculateAmbientLighting (includes IBL)
        ambient = calculateAmbientLighting(material, vWorldPos);

        // Apply baked ambient occlusion texture
        if (hasOcclusionTex) {
            float ao = (u_UseTriplanar == 1)
                ? SampleTriplanarGray(u_OcclusionTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
                : texture(u_OcclusionTex, effectiveUV).r;
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

    vec3 color = ambient + Lo;

    // Apply fog
    color = processFog(color, vWorldPos);

    // Apply stylization effects
    color = adjustSaturation(color, u_Saturation);
    color = adjustBrightness(color, u_Brightness);
    color = adjustContrast(color, u_Contrast);
    color = adjustHue(color, u_Hue);

    // Add emissive
    vec3 emissiveTex = vec3(0.0);
    if (textureSize(u_EmissiveTex, 0) != ivec2(1,1)) {
        emissiveTex = (u_UseTriplanar == 1) 
            ? SampleTriplanarColor(u_EmissiveTex, vWorldPos, baseNormal, u_TriplanarScale, u_TriplanarBlendSharpness)
            : texture(u_EmissiveTex, effectiveUV).rgb;
    }
    vec3 emissive = emissiveTex * u_EmissiveColor * u_Emission;
    color += emissive;

    // Handle transparency
    float outAlpha = 1.0;
    if (u_TransparencyMode != 0) {
        // Use the alpha we already sampled earlier (supports both triplanar and standard UVs)
        outAlpha = saturate(sampledAlbedoAlpha.a * u_AlbedoColor.a);
    }

    outColor = vec4(color, outAlpha);
    outId = u_ObjectId;
}
